#!/usr/bin/env bash
#
# Install the nightly measurement schedule on a box.
#
#   bash build/install-measurement-cron.sh mutation
#   bash build/install-measurement-cron.sh fuzz
#   bash build/install-measurement-cron.sh show
#   bash build/install-measurement-cron.sh remove
#
# WHY CRON AND NOT A WEBHOOK
# These are not GitHub runners. A public repo with pull_request triggers would mean
# stranger-authored workflow code executing here, so nothing external is allowed to
# start a job. The box pulls; nothing pushes to it.
#
# WHY THE CADENCE IS WHAT IT IS
# Two reasons, and the second is the surprising one.
#
#   1. Mutation testing is worth having but too slow to gate a merge on. Nightly turns it
#      from a gate into a signal that arrives afterwards.
#   2. Oracle reclaims Always Free compute that stays idle. The published rule is an AND
#      over three 95th-percentile metrics across 7 days — CPU, network and memory all
#      under 20%. A 95th percentile above 20% means the box must be busy for more than 5%
#      of the week, which is 8.4 hours. A single 38-minute nightly run is 4.4 hours a week
#      and does NOT clear that bar. Hence TWICE daily on the mutation box (2 x 38 min =
#      8.9 h/week, just over) and a two-hour fuzz run on the fuzz box.
#
#      Verify the current rule in the live console rather than trusting this comment —
#      Oracle's docs pages have been observed stale, and the numbers above are the reason
#      for the schedule, not a guarantee about their policy.
#
# WHY THE CLOCK TIMES ARE WHAT THEY ARE
# 07:00 because the user wants results waiting when they wake, so a day off is a day they
# can act on the numbers. The rest follows from two constraints, not from taste:
#
#   * The flock is PER BOX. mutation-box and fuzz-box are separate machines with separate
#     /tmp locks, so both start at 07:00 with no interaction whatsoever.
#   * Same-box jobs must NOT overlap, because the lock REFUSES rather than queues. A
#     second job landing inside the first one's window is simply skipped that day. Hence
#     the Sunday scraper at 08:15, comfortably clear of a 07:00 core run that finishes
#     around 07:38.
#
# THE BOX IS SET TO THE USER'S TIMEZONE, and that is the point. With a UTC crontab the
# 07:00 they asked for silently becomes 06:00 when daylight saving ends — a fixed UTC
# entry cannot express "7am local". Run directories are unaffected: run-measurements.sh
# stamps them with `date -u`, so they stay UTC and stay sortable across the transition.
# The DST transition hour (02:00) is deliberately empty; a job scheduled there is either
# skipped or run twice on the changeover days.
set -euo pipefail

# IANA zone for the crontab below. Change this and the times move with it.
BOX_TZ="America/New_York"

ROLE="${1:-}"
SCRIPT="${HOME}/pbj/build/run-measurements.sh"
LOG="${HOME}/cron-measure.log"
MARK="# pbj-measurements"

case "$ROLE" in
  show)
    crontab -l 2>/dev/null | grep -F "$MARK" || echo "no pbj-measurements entries"
    exit 0
    ;;
  remove)
    crontab -l 2>/dev/null | grep -vF "$MARK" | crontab - || true
    echo "removed"
    exit 0
    ;;
  mutation|fuzz) ;;
  *)
    echo "usage: $0 {mutation|fuzz|show|remove}" >&2
    exit 2
    ;;
esac

if [ ! -f "$SCRIPT" ]; then
  echo "ERROR: $SCRIPT not found — clone the repo to ~/pbj first" >&2
  exit 1
fi

# Set the clock BEFORE writing the crontab, so the entries mean what they say from the
# first run onward. Idempotent — timedatectl is a no-op when the zone already matches.
if [ "$(timedatectl show -p Timezone --value)" != "$BOX_TZ" ]; then
  echo "==> setting box timezone to ${BOX_TZ} (was $(timedatectl show -p Timezone --value))"
  sudo timedatectl set-timezone "$BOX_TZ"
  # cron reads the zone at start; without this it keeps firing on the OLD offset until
  # something restarts it, which is a silent one-hour error rather than a visible failure.
  sudo systemctl restart cron
fi

# cron gives a near-empty environment and ~/.bashrc returns early when non-interactive,
# so neither DOTNET_ROOT nor PATH survives. run-measurements.sh exports both itself,
# which is why these lines can call bash directly rather than wrapping in `bash -lc`.
if [ "$ROLE" = "mutation" ]; then
  NEW=$(cat <<EOF
0 7 * * *   bash ${SCRIPT} stryker-core    >> ${LOG} 2>&1  ${MARK}
0 19 * * *  bash ${SCRIPT} stryker-core    >> ${LOG} 2>&1  ${MARK}
15 8 * * 0  bash ${SCRIPT} stryker-scraper >> ${LOG} 2>&1  ${MARK}
EOF
)
else
  # Two hours, so the week clears the idle threshold on its own. Fuzzing wants hours
  # anyway — 300 seconds was a smoke test, not a campaign. 07:00-09:00 local.
  NEW="0 7 * * * bash ${SCRIPT} fuzz 7200 >> ${LOG} 2>&1  ${MARK}"
fi

# Replace rather than append, so re-running this is idempotent.
{ crontab -l 2>/dev/null | grep -vF "$MARK" || true; echo "$NEW"; } | crontab -

# Deploy the shared schedule doc next to the crontabs it describes, so any agent with SSH
# can read the house rules without needing this repo checked out. Copied, never edited in
# place: the repo copy is canonical and this one is overwritten on every install.
DOC_SRC="$(cd "$(dirname "$0")" && pwd)/measurement-schedule.md"
if [ -f "$DOC_SRC" ]; then
  cp "$DOC_SRC" "${HOME}/measurement-schedule.md"
  echo "==> schedule doc -> ${HOME}/measurement-schedule.md"
fi

echo "==> installed (${ROLE}); times below are ${BOX_TZ} local, currently $(date '+%Z %H:%M')"
crontab -l | grep -F "$MARK"
echo "==> log: ${LOG}"
