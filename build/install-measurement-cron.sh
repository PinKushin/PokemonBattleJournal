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
#      and does NOT clear that bar. Hence twice daily on the mutation box and a two-hour
#      fuzz run on the fuzz box.
#
#      Verify the current rule in the live console rather than trusting this comment —
#      Oracle's docs pages have been observed stale, and the numbers above are the reason
#      for the schedule, not a guarantee about their policy.
set -euo pipefail

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

# cron gives a near-empty environment and ~/.bashrc returns early when non-interactive,
# so neither DOTNET_ROOT nor PATH survives. run-measurements.sh exports both itself,
# which is why these lines can call bash directly rather than wrapping in `bash -lc`.
if [ "$ROLE" = "mutation" ]; then
  NEW=$(cat <<EOF
30 3 * * *  bash ${SCRIPT} stryker-core    >> ${LOG} 2>&1  ${MARK}
30 15 * * * bash ${SCRIPT} stryker-core    >> ${LOG} 2>&1  ${MARK}
30 8 * * 0  bash ${SCRIPT} stryker-scraper >> ${LOG} 2>&1  ${MARK}
EOF
)
else
  # Two hours, so the week clears the idle threshold on its own. Fuzzing wants hours
  # anyway — 300 seconds was a smoke test, not a campaign.
  NEW="0 4 * * * bash ${SCRIPT} fuzz 7200 >> ${LOG} 2>&1  ${MARK}"
fi

# Replace rather than append, so re-running this is idempotent.
{ crontab -l 2>/dev/null | grep -vF "$MARK" || true; echo "$NEW"; } | crontab -

echo "==> installed (${ROLE}), times are UTC:"
crontab -l | grep -F "$MARK"
echo "==> log: ${LOG}"
