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

# Node reuse leaves MSBuild daemons alive for 15 minutes after the build that started them.
# On a box whose whole job is one workload at a time they are pure downside: they hold obj/,
# they hold memory, and — see the 9>&- note below — they held the lock for 12 minutes after
# the run that took it had exited.
export MSBUILDDISABLENODEREUSE=1

# Written into every run directory as `.owner`, and the only thing the prune at the bottom will
# delete on. Shared box: ~/measurements holds other projects' results too.
OWNER="pokemonbattlejournal"

WORKDIR="${PBJ_DIR:-$HOME/pbj}"
# NEVER `rm` THIS FILE, not even as cleanup between runs. Deleting it does not free the
# lock — flock is on the open file description — but it DOES unlink the inode, so the next
# run's `exec 9>` creates a fresh file and takes a fresh lock while the old holder is still
# working. That is not a stuck lock, it is NO lock: two workloads run at once and neither
# reports anything. Done for real on 2026-08-10 while renaming this path, and it let a
# tf2demosalvage Stryker run and a PokemonBattleJournal one overlap.
#
# BOX-WIDE, and deliberately NOT named after this project. Several repos share these
# boxes — tf2demosalvage is joining — and the thing being serialised is the BOX, not the
# repo. A per-project lock name would let two projects run at once, which is the exact
# corruption this guards: Stryker rebuilds mutated copies continuously, and a build
# failure caused by a competing job is reported as a SURVIVING MUTANT rather than an
# error. Any new project's runner must use this same path.
LOCK="/tmp/measurement-box.lock"
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
  echo "usage: $0 {stryker-core|stryker-scraper|fuzz [seconds]|fuzz-importrestore [seconds]} [--no-pull]" >&2
  exit 2
fi

# Refuse rather than queue. A second concurrent run does not just take longer — it
# corrupts the first one's results.
#
# EVERY long command below closes fd 9 with `9>&-`. An inherited fd keeps the lock held for
# as long as ANY descendant lives, and .NET leaves descendants behind on purpose: MSBuild
# node-reuse daemons and the Roslyn VBCSCompiler server both outlive the build. That is not
# theoretical — a finished run left `dotnet /nodemode:1` and VBCSCompiler holding this file
# 12 minutes later, and the next launch was refused by a workload that had already exited.
exec 9>"$LOCK"
if ! flock -n 9; then
  echo "ERROR: another measurement run holds $LOCK. One at a time." >&2
  # Name the holder. Without this the refusal is indistinguishable from a stale lock, and the
  # tempting fix — deleting the file — does nothing, because flock is on the open fd and not
  # on the path. Killing the PID is what actually frees it.
  command -v fuser >/dev/null && { echo "held by:" >&2; fuser -v "$LOCK" >&2 2>&1 || true; }
  exit 1
fi

# Rotate the shared cron log before appending to it. Nothing else writes here, so this is
# the honest place to do it — a logrotate config would be a second mechanism to keep in sync.
CRONLOG="${HOME}/cron-measure.log"
if [ -f "$CRONLOG" ] && [ "$(stat -c %s "$CRONLOG")" -gt 1048576 ]; then
  mv "$CRONLOG" "${CRONLOG}.1"
  echo "(rotated at $(date '+%F %H:%M:%S'), previous log in $(basename "${CRONLOG}").1)" > "$CRONLOG"
fi

cd "$WORKDIR"

if [ "$PULL" -eq 1 ]; then
  git fetch --quiet origin
  git reset --quiet --hard origin/master
fi

# Stryker defaults --concurrency to HALF the logical processors, and this box has 3, which
# integer-divides to 1. Every mutation run here was single threaded and nothing said so: the
# output is identical, only slower. Measured by Tf2DemoSalvage on their `core` suite 2026-08-12 —
# 1h35m40s to 22m33s, 4.2x, 3.01 s to 0.68 s per mutant.
#
# Set here rather than in stryker-core.json because that file is shared with local runs, where
# the conservative default is correct: a developer's machine has to stay usable while it runs.
CONCURRENCY="$(nproc)"

SHA="$(git rev-parse --short HEAD)"
STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
OUT="$HOME/measurements/${STAMP}-${SHA}-${MODE}"
mkdir -p "$OUT"

# Stamp the run with its owner. The prune at the bottom deletes only directories carrying THIS
# marker, so this script can never remove a neighbouring project's results. See the note there
# for why the obvious alternative — matching on the directory name — does not work.
printf '%s\n' "$OWNER" > "${OUT}/.owner"

echo "==> ${MODE} on ${SHA} at $(date '+%F %H:%M:%S %Z'), output -> ${OUT}"

case "$MODE" in
  stryker-core)
    dotnet tool restore 9>&-
    # Core is where the app logic lives. The MAUI head cannot be mutated at all —
    # Stryker's internal recompile does not reproduce XAML codegen or the MVVM source
    # generators, and surfaces no CS error when it fails. That is why Core exists.
    # --solution is REQUIRED on Linux. Stryker builds the containing solution before
    # mutating, and PokemonBattleJournal.slnx cannot build here: UITests.Windows needs
    # Microsoft.WindowsDesktop.App (NETSDK1073) and the app head's net10.0-android TFM needs
    # the Android SDK (XA5300). The Linux solution omits both.
    dotnet stryker --config-file stryker-core.json --concurrency "$CONCURRENCY" --solution DO-NOT-OPEN-IN-VS.LinuxMeasurementBox.slnx 2>&1 9>&- | tee "${OUT}/stryker.log"
    cp -r StrykerOutput "${OUT}/" 2>/dev/null || true
    grep -E "final mutation score|Killed:|Survived:|Timeout:|NoCoverage" "${OUT}/stryker.log" | tail -8
    ;;

  stryker-scraper)
    dotnet tool restore 9>&-
    # break: 90 in stryker-config.json is a RATCHET protecting a real 100% score, not a
    # reckless threshold. If this fails, the Scraper score genuinely dropped — do not
    # lower the threshold to make it pass.
    dotnet stryker --concurrency "$CONCURRENCY" --solution DO-NOT-OPEN-IN-VS.LinuxMeasurementBox.slnx 2>&1 9>&- | tee "${OUT}/stryker.log"
    cp -r StrykerOutput "${OUT}/" 2>/dev/null || true
    grep -E "final mutation score|Killed:|Survived:" "${OUT}/stryker.log" | tail -6
    ;;

  fuzz|fuzz-importrestore)
    SECONDS_BUDGET="${ARG:-300}"
    rm -rf "${HOME}/fuzz-out" && mkdir -p "${HOME}/findings"
    dotnet publish PokemonBattleJournal.Fuzz -c Release -o "${HOME}/fuzz-out" --nologo -v q 9>&-
    # Instrument CORE, not the harness: coverage feedback has to come from the code
    # under test or the fuzzer cannot make progress.
    sharpfuzz "${HOME}/fuzz-out/PokemonBattleJournal.Core.dll" 9>&-

    # The two suites share every step below EXCEPT their seeds and the suite selector, because
    # the slow SQLite-backed import/restore parsers must not share a budget with the fast pure
    # functions (~400 exec/s vs tens of thousands). One target binary, two suites, chosen here.
    rm -rf "${HOME}/corpus" "${HOME}/seeds"
    mkdir -p "${HOME}/corpus" "${HOME}/seeds"
    if [ "$MODE" = fuzz-importrestore ]; then
      export PBJFUZZ_SUITE=importrestore
      # First byte: even = import (a TrainerHill JSON array), odd = restore (a backup envelope).
      # The import result MUST be "win"/"loss"/"tie" — "W" does not parse, imports zero, and would
      # leave the count invariant comparing 0==0 (vacuous). Verified in WSL.
      printf '\000[{"result":"win","playing":"raging-bolt","against":"charizard-ex","game1":{"result":"win","turn":5}}]' > "${HOME}/corpus/import-bo1"
      printf '\000[{"result":"loss","playing":"gardevoir-ex","against":"lugia-vstar","game1":{"result":"loss","turn":8,"tags":["donk"],"notes":"bricked"}}]' > "${HOME}/corpus/import-tags"
      printf '\000not a json document at all' > "${HOME}/corpus/import-garbage"
      printf '\001{"version":1,"exportedUtc":"2026-01-01T00:00:00Z","archetypes":[],"trainers":[{"name":"Ash","matches":[]}]}' > "${HOME}/corpus/restore-empty"
      printf '\001{"version":999}' > "${HOME}/corpus/restore-newer"
      printf '\001garbage' > "${HOME}/corpus/restore-garbage"
    else
      # Fast suite: seeds once per mode byte. The harness multiplexes on the first input byte, so a
      # seed's own first byte decides which of the four modes it reaches — prefixing each seed with
      # each mode byte is what makes all four start from sensible input.
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
    fi

    # PROVE THE INSTRUMENT BEFORE TRUSTING THE MEASUREMENT.
    #
    # `-artifact_prefix` writes NOTHING through libfuzzer-dotnet: a managed exception aborts the
    # .NET child, the bridge dies with it, and libFuzzer's crash handler never runs. So a run
    # reports the defect in its log and loses the input — and an empty findings directory looks
    # identical to a clean run. PBJ carried that defect from the start and it stayed invisible
    # for exactly one reason: nothing had ever crashed, so the path was never exercised.
    #
    # PBJFUZZ_SELFTEST makes every input throw, so the preservation path runs on the first
    # execution. If no file appears, the whole night's fuzzing would have been unable to keep a
    # finding, and that is worth failing the run over rather than discovering later.
    export PBJFUZZ_CRASH_DIR="${HOME}/findings"
    SELFTEST_DIR="${HOME}/findings-selftest"
    rm -rf "$SELFTEST_DIR"
    echo "==> self-test: proving a crashing input is preserved"
    PBJFUZZ_SELFTEST=1 PBJFUZZ_CRASH_DIR="$SELFTEST_DIR" "${HOME}/libfuzzer-dotnet" \
      --target_path="${HOME}/fuzz-out/PokemonBattleJournal.Fuzz" \
      -runs=1 \
      "${HOME}/corpus" 9>&- > "${OUT}/selftest.log" 2>&1 || true

    if ! ls "$SELFTEST_DIR"/crash-*.bin >/dev/null 2>&1; then
      echo "ERROR: self-test produced no crash file in ${SELFTEST_DIR}." >&2
      echo "       Crash preservation is broken; a real finding would be lost silently." >&2
      tail -20 "${OUT}/selftest.log" >&2
      exit 1
    fi
    echo "    ok: $(ls -1 "$SELFTEST_DIR"/crash-*.bin | wc -l) crash file(s) written"
    rm -rf "$SELFTEST_DIR"

    # -artifact_prefix stays set even though it writes nothing here, so that if a future
    # toolchain does start producing artifacts they land beside ours rather than in $PWD.
    "${HOME}/libfuzzer-dotnet" \
      --target_path="${HOME}/fuzz-out/PokemonBattleJournal.Fuzz" \
      -max_total_time="${SECONDS_BUDGET}" \
      -artifact_prefix="${HOME}/findings/" \
      -print_final_stats=1 \
      "${HOME}/corpus" 2>&1 9>&- | tee "${OUT}/fuzz.log"

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

# Keep the last 30 of THIS PROJECT'S runs. On a cron cadence this directory grows without bound,
# and a Stryker HTML report is a few MB — the free tier's 50 GB boot volume is the whole disk.
# Findings are NOT pruned with the run that produced them: they are copied to ~/findings
# as well, which is the durable location.
#
# ~/measurements is shared by every project on the box. This used to glob `*/` and so pruned
# whatever it found, which is a per-project script reaching across a shared machine — the same
# failure family as naming a lock after the project instead of the box. PBJ alone makes about 15
# runs a week here, so a 30-slot window is about two weeks: this line would have silently deleted
# Tf2DemoSalvage's 18-hour corpus measurement, from a script with nothing to do with it. Caught
# 2026-08-12 with 16 directories present, so nothing was lost.
#
# **Matching on the directory NAME is not a fix, and looks like one.** Runs are named
# `<stamp>-<sha>-<mode>`, so the tempting glob for this project is `*-fuzz/`, `*-stryker-core/`
# and so on. But Tf2DemoSalvage's fuzz runs are named `<stamp>-<sha>-tf2-fuzz`, which *ends in*
# `-fuzz` and therefore matches `*-fuzz/` — the neighbour's directory is deleted by a glob written
# specifically to avoid deleting it. Their older runs (`-fuzz-container`, `-fuzz-bitreader`) carry
# no `-tf2-` infix at all, so the mirror-image exclusion fails too. Naming conventions drift
# independently in two repos; an ownership marker does not.
#
# A directory with no `.owner` file is left alone rather than pruned. That is deliberate: the
# conservative failure is an old directory surviving, and the disk has room (246 MB used of 45 GB
# when this was written). Deleting on absence would re-create exactly the bug being fixed for any
# project that has not adopted the marker yet.
mine=()
while IFS= read -r dir; do
  [ -f "${dir}.owner" ] || continue
  [ "$(cat "${dir}.owner")" = "$OWNER" ] || continue
  mine+=("$dir")
done < <(ls -1dt "${HOME}/measurements/"*/ 2>/dev/null)

# Newest first, so everything from index 30 on is surplus.
if [ "${#mine[@]}" -gt 30 ]; then
  for old in "${mine[@]:30}"; do
    echo "==> pruning own run: $(basename "$old")"
    rm -rf "$old"
  done
fi

echo "==> done: ${OUT}"
