#!/usr/bin/env bash
# Does the prune delete only this project's runs?
#
# The block under test is EXTRACTED from the real script rather than retyped, so this cannot pass
# against a copy that has drifted from what runs on the box.
#
# The fixture deliberately contains the two names that defeat name-based scoping:
#   <sha>-tf2-fuzz         ends in -fuzz, so a `*-fuzz/` glob would delete it
#   <sha>-fuzz-container   carries no -tf2- infix, so an exclusion glob would miss it
# Both are bystanders here: they must survive untouched.
set -euo pipefail

SRC="${SRC:-$(cd "$(dirname "$0")" && pwd)/run-measurements.sh}"
# EXTRACT_FROM/EXTRACT_TO let the sabotage check point this at a deliberately broken copy.
EXTRACT_FROM="${EXTRACT_FROM:-/^mine=()/}"
EXTRACT_TO="${EXTRACT_TO:-/^fi\$/}"
GUARD="${GUARD:-mine+=}"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

export HOME="$WORK"
mkdir -p "$HOME/measurements"

OWNER="pokemonbattlejournal"

# --- fixture -------------------------------------------------------------------------------
# 35 of ours, oldest first so mtimes ascend with the index.
for i in $(seq -w 1 35); do
  d="$HOME/measurements/2026080${i:0:1}T${i}0000Z-abc${i}-stryker-core"
  mkdir -p "$d"
  printf '%s\n' "$OWNER" > "$d/.owner"
  # Ascending mtimes, so index 01 is oldest and 35 newest. Plain HH:MM avoids date-arithmetic
  # formats that differ between GNU touch and the one shipped with Git for Windows.
  touch -d "2026-07-01 $(printf '%02d:%02d' $((10#$i / 60)) $((10#$i % 60))):00" "$d"
done

# Bystanders: a neighbouring project's runs, no marker.
for name in 20260811T000000Z-2d5d940-tf2-fuzz \
            20260811T000001Z-f910e8b-fuzz-container \
            20260811T000002Z-0a2960c-fuzz-bitreader \
            20260811T000003Z-0acc791-tf2-corpus; do
  mkdir -p "$HOME/measurements/$name"
  touch -d "2026-07-01 00:00:00" "$HOME/measurements/$name"   # oldest, so first to go if unscoped
done

# A directory owned by someone else explicitly, to prove the marker is compared and not merely present.
other="$HOME/measurements/20260811T000004Z-deadbee-tf2-core"
mkdir -p "$other"
printf 'tf2demosalvage\n' > "$other/.owner"
touch -d "2026-07-01 00:00:00" "$other"

before_ours=$(ls -1d "$HOME/measurements/"*stryker-core/ | wc -l)
before_others=$(ls -1d "$HOME/measurements/"* | wc -l)
echo "fixture: ${before_ours} ours, $((before_others - before_ours)) belonging to others"

# --- extract and run the real block ---------------------------------------------------------
BLOCK="$WORK/prune-block.sh"
{
  echo 'set -euo pipefail'
  echo "OWNER=\"$OWNER\""
  sed -n "${EXTRACT_FROM},${EXTRACT_TO}p" "$SRC"
} > "$BLOCK"

if ! grep -q "$GUARD" "$BLOCK"; then
  echo "FAIL: could not extract the prune block from $SRC" >&2
  exit 1
fi

bash "$BLOCK"

# --- assertions -----------------------------------------------------------------------------
fail=0

ours_left=$(ls -1d "$HOME/measurements/"*stryker-core/ 2>/dev/null | wc -l)
[ "$ours_left" -eq 30 ] || { echo "FAIL: expected 30 of ours to remain, got $ours_left"; fail=1; }

for name in 20260811T000000Z-2d5d940-tf2-fuzz \
            20260811T000001Z-f910e8b-fuzz-container \
            20260811T000002Z-0a2960c-fuzz-bitreader \
            20260811T000003Z-0acc791-tf2-corpus \
            20260811T000004Z-deadbee-tf2-core; do
  [ -d "$HOME/measurements/$name" ] || { echo "FAIL: deleted a bystander: $name"; fail=1; }
done

# The five deleted must be the OLDEST five of ours, not an arbitrary five.
for i in 01 02 03 04 05; do
  d=$(ls -1d "$HOME/measurements/"*"-abc${i}-stryker-core"/ 2>/dev/null || true)
  [ -z "$d" ] || { echo "FAIL: oldest run abc${i} survived; wrong five were pruned"; fail=1; }
done
for i in 06 35; do
  ls -1d "$HOME/measurements/"*"-abc${i}-stryker-core"/ >/dev/null 2>&1 \
    || { echo "FAIL: abc${i} should have been kept"; fail=1; }
done

if [ "$fail" -eq 0 ]; then
  echo "PASS: 30 of ours kept, oldest 5 of ours pruned, all 5 bystanders untouched"
else
  echo "FAILURES ABOVE"; exit 1
fi
