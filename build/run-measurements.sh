#!/usr/bin/env bash
#
# Run one long measurement workload on the measurement box.
#
#   bash build/run-measurements.sh stryker-core     [--no-pull]
#   bash build/run-measurements.sh stryker-scraper  [--no-pull]
#   bash build/run-measurements.sh fuzz [seconds]   [--no-pull]
#
# One workload at a time, on purpose. Stryker rebuilds mutated copies continuously and
# holds obj/; anything else building at the same time kills the run and does it
# QUIETLY — a build failure mid-mutation looks like a surviving mutant, not an error.
# That is why this refuses to start when another run is already going.
set -euo pipefail

export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"

WORKDIR="${PBJ_DIR:-$HOME/pbj}"
LOCK="/tmp/pbj-measurement.lock"
MODE="${1:-}"
shift || true

PULL=1
ARG=""
for a in "$@"; do
  case "$a" in
    --no-pull) PULL=0 ;;
    *) ARG="$a" ;;
  esac
done

if [ -z "$MODE" ]; then
  echo "usage: $0 {stryker-core|stryker-scraper|fuzz [seconds]} [--no-pull]" >&2
  exit 2
fi

# Refuse rather than queue. A second concurrent run does not just take longer — it
# corrupts the first one's results.
exec 9>"$LOCK"
if ! flock -n 9; then
  echo "ERROR: another measurement run holds $LOCK. One at a time." >&2
  exit 1
fi

cd "$WORKDIR"

if [ "$PULL" -eq 1 ]; then
  git fetch --quiet origin
  git reset --quiet --hard origin/master
fi

SHA="$(git rev-parse --short HEAD)"
STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
OUT="$HOME/measurements/${STAMP}-${SHA}-${MODE}"
mkdir -p "$OUT"

echo "==> ${MODE} on ${SHA}, output -> ${OUT}"

case "$MODE" in
  stryker-core)
    dotnet tool restore
    # Core is where the app logic lives. The MAUI head cannot be mutated at all —
    # Stryker's internal recompile does not reproduce XAML codegen or the MVVM source
    # generators, and surfaces no CS error when it fails. That is why Core exists.
    # --solution is REQUIRED on Linux. Stryker builds the containing solution before
    # mutating, and PokemonBattleJournal.slnx cannot build here: UITests.Windows needs
    # Microsoft.WindowsDesktop.App (NETSDK1073) and the app head's net10.0-android TFM needs
    # the Android SDK (XA5300). The Linux solution omits both.
    dotnet stryker --config-file stryker-core.json --solution DO-NOT-OPEN-IN-VS.LinuxMeasurementBox.slnx 2>&1 | tee "${OUT}/stryker.log"
    cp -r StrykerOutput "${OUT}/" 2>/dev/null || true
    grep -E "final mutation score|Killed:|Survived:|Timeout:|NoCoverage" "${OUT}/stryker.log" | tail -8
    ;;

  stryker-scraper)
    dotnet tool restore
    # break: 90 in stryker-config.json is a RATCHET protecting a real 100% score, not a
    # reckless threshold. If this fails, the Scraper score genuinely dropped — do not
    # lower the threshold to make it pass.
    dotnet stryker --solution DO-NOT-OPEN-IN-VS.LinuxMeasurementBox.slnx 2>&1 | tee "${OUT}/stryker.log"
    cp -r StrykerOutput "${OUT}/" 2>/dev/null || true
    grep -E "final mutation score|Killed:|Survived:" "${OUT}/stryker.log" | tail -6
    ;;

  fuzz)
    SECONDS_BUDGET="${ARG:-300}"
    rm -rf "${HOME}/fuzz-out" && mkdir -p "${HOME}/findings"
    dotnet publish PokemonBattleJournal.Fuzz -c Release -o "${HOME}/fuzz-out" --nologo -v q
    # Instrument CORE, not the harness: coverage feedback has to come from the code
    # under test or the fuzzer cannot make progress.
    sharpfuzz "${HOME}/fuzz-out/PokemonBattleJournal.Core.dll"

    # Seeds, once per mode byte. The harness multiplexes on the first input byte, so a
    # seed's own first byte decides which of the four modes it reaches — prefixing each
    # seed with each mode byte is what makes all four start from sensible input.
    mkdir -p "${HOME}/corpus" "${HOME}/seeds"
    printf 'went first, drew Iono\000went second, drew Iono' > "${HOME}/seeds/note-edit"
    printf '4 Charizard ex\n3 Pidgeot ex\n2 Iono\0004 Charizard ex\n3 Pidgeot ex\n2 Judge' > "${HOME}/seeds/deck-list"
    printf 'a\nb\nc\nd\ne\nf\ng\nh\000a\nB\nc\nd\nX\ne\nf\nh' > "${HOME}/seeds/interleaved"
    printf 'donk\nmulligan\000mulligan\nprized' > "${HOME}/seeds/tags"
    printf 'TrainerName\000Ash Ketchum' > "${HOME}/seeds/redact-name"
    printf 'ValidationMessage\000Select a deck' > "${HOME}/seeds/redact-allowlisted"
    for seed in "${HOME}"/seeds/*; do
      n="$(basename "$seed")"
      for mode in 0 1 2 3; do
        printf "$(printf '\\%03o' "$mode")" | cat - "$seed" > "${HOME}/corpus/${n}-m${mode}.bin"
      done
    done

    "${HOME}/libfuzzer-dotnet" \
      --target_path="${HOME}/fuzz-out/PokemonBattleJournal.Fuzz" \
      -max_total_time="${SECONDS_BUDGET}" \
      -artifact_prefix="${HOME}/findings/" \
      -print_final_stats=1 \
      "${HOME}/corpus" 2>&1 | tee "${OUT}/fuzz.log"

    # A crash is written as the exact bytes that caused it — a regression fixture
    # rather than a bug report. Keep it.
    cp -r "${HOME}/findings" "${OUT}/" 2>/dev/null || true
    ls -1 "${HOME}/findings" 2>/dev/null | head || echo "no findings"
    ;;

  *)
    echo "unknown mode: $MODE" >&2
    exit 2
    ;;
esac

echo "==> done: ${OUT}"
