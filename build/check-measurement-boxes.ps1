<#
.SYNOPSIS
    One command to answer "are the measurement boxes still alive and still measuring?"

.DESCRIPTION
    The boxes run unattended on cron, which is the point of them and also the danger: a run
    that stops happening produces no output, no error and no notification. Nothing tells you.
    Two failure modes matter, and neither announces itself.

      1. Oracle RECLAIMS Always Free compute that stays idle. The box does not break, it
         ceases to exist, and the first sign is an SSH timeout.
      2. A cron job fails every night in the same way and the log just stops growing. The
         Stryker threshold bug caught on 2026-08-10 was exactly this shape: the gate refused
         to start and would have failed silently every Sunday.

    So this checks for RECENCY, not just reachability. A box that answers SSH but whose last
    run is four days old is the interesting case.

.EXAMPLE
    ./build/check-measurement-boxes.ps1
#>
[CmdletBinding()]
param(
    # A run older than this is reported stale. Default 36h: mutation-box runs twice daily and
    # fuzz-box nightly, so anything past this has missed at least one scheduled slot.
    [int] $StaleHours = 36,

    # Weekly review mode. Dumps the material a scheduling decision needs and cannot be
    # eyeballed from the daily check: the FULL crontab from each box (the only authority on
    # what is really booked), the last runs per project with their scores and wall times, and
    # disk. The point is drift — a runtime that has grown into its neighbour's slot does not
    # announce itself, it just starts losing runs to a lock that refuses rather than queues.
    [switch] $Review
)

$ErrorActionPreference = 'Stop'

# Deliberately not a single ssh call per box with everything crammed in: when a box is gone,
# the failure needs to be attributable to that box rather than to a parse error downstream.
# Schedules are stated in plain local time, with no conversion, because there is no longer
# any conversion to do: the boxes are set to America/New_York so their crontabs ARE local.
# That was the fix for a UTC crontab being unable to express "7am local" across daylight
# saving — a fixed UTC entry silently becomes 6am when the offset changes.
#
# These strings must match build/install-measurement-cron.sh. They are documentation, not
# the source of truth; the box's own `crontab -l` is, which is why the check reports the
# ENTRY COUNT from the box rather than trusting this list.
[hashtable[]] $boxes = @(
    @{ Alias = 'mutation-box'; Expect = 'stryker-core 7:00 AM and 7:00 PM daily; stryker-scraper 8:15 AM Sunday' },
    @{ Alias = 'fuzz-box';     Expect = 'fuzz 7200 at 7:00 AM daily (runs until ~9:00 AM)' }
)

foreach ($box in $boxes) {
    [string] $alias = $box.Alias
    Write-Host ''
    Write-Host "=== $alias ===" -ForegroundColor Cyan
    Write-Host "    expected: $($box.Expect) (box local time)" -ForegroundColor DarkGray

    # BatchMode so a missing or passphrase-locked key fails immediately instead of hanging on
    # an interactive prompt this script cannot answer.
    [string] $remote = @'
# Count per project, not just PBJ. These boxes are shared — tf2demosalvage joined on
# 2026-08-10 — and a check that only ever counts its own entries would report a healthy
# box while another project's schedule had silently vanished.
echo "cron:  $(crontab -l 2>/dev/null | grep -c -- '-measurements') entries total$(crontab -l 2>/dev/null | grep -oE '# [a-z0-9]+-measurements' | sort | uniq -c | awk '{printf " | %s x%s", $3, $1}')"
echo "tz:    $(timedatectl show -p Timezone --value) - now $(date '+%a %H:%M %Z')"
echo "up:    $(uptime -p)"
echo "disk:  $(df -h / | awk 'NR==2 {print $4" free of "$2}')"
last=$(ls -1dt ~/measurements/*/ 2>/dev/null | head -1)
if [ -z "$last" ]; then
  echo "last:  NONE - no run has ever completed"
else
  echo "last:  $(basename "$last")"
  echo "age_h: $(( ( $(date +%s) - $(stat -c %Y "$last") ) / 3600 ))"
  grep -hE "final mutation score|new_units_added" "$last"/*.log 2>/dev/null | tail -2
fi
if [ -f ~/cron-measure.log ]; then
  echo "log:   $(stat -c %y ~/cron-measure.log | cut -d. -f1) ($(wc -l < ~/cron-measure.log) lines)"
else
  echo "log:   NONE - cron has never produced output"
fi
'@

    [string[]] $out = & ssh -o BatchMode=yes -o ConnectTimeout=15 $alias $remote 2>&1
    if ($LASTEXITCODE -ne 0) {
        # Unreachable is the loud case. An Always Free instance that was reclaimed looks
        # exactly like this, so do not soften it into a warning.
        Write-Host "    UNREACHABLE (ssh exit $LASTEXITCODE)" -ForegroundColor Red
        Write-Host "    $($out -join "`n    ")" -ForegroundColor DarkRed
        Write-Host "    Check the box still EXISTS in the Oracle console before debugging ssh." -ForegroundColor Red
        continue
    }

    foreach ($line in $out) {
        if ($line -match '^age_h:\s*(\d+)') {
            [int] $ageHours = [int] $Matches[1]
            if ($ageHours -gt $StaleHours) {
                Write-Host "    STALE: last run was $ageHours h ago (threshold $StaleHours h)" -ForegroundColor Red
                Write-Host "    Read ~/cron-measure.log on the box — a job that fails the same way" -ForegroundColor Red
                Write-Host "    every night is silent by design." -ForegroundColor Red
            }
            else {
                Write-Host "    fresh: last run $ageHours h ago" -ForegroundColor Green
            }
            continue
        }
        if ($line -match 'NONE') { Write-Host "    $line" -ForegroundColor Red; continue }
        Write-Host "    $line"
    }
}

if ($Review) {
    foreach ($box in $boxes) {
        [string] $alias = $box.Alias
        Write-Host ''
        Write-Host "=== WEEKLY REVIEW: $alias ===" -ForegroundColor Yellow

        # Wall time per run comes from the directory mtime minus its name stamp: the name is
        # stamped at START (date -u) and the directory is last written at the END, so the
        # difference is the real duration including build and git work. Stryker's own
        # "Time Elapsed" excludes those, which is why it is not the number a SLOT needs.
        [string] $remote = @'
echo "--- crontab (the authority on what is booked) ---"
crontab -l 2>/dev/null | grep -- '-measurements' || echo "(no measurement entries)"
echo
echo "--- last 8 runs: name, wall minutes, headline ---"
for d in $(ls -1dt ~/measurements/*/ 2>/dev/null | head -8); do
  n=$(basename "$d")
  stamp=${n%%-*}
  s=$(date -u -d "${stamp:0:4}-${stamp:4:2}-${stamp:6:2} ${stamp:9:2}:${stamp:11:2}:${stamp:13:2}" +%s 2>/dev/null)
  e=$(stat -c %Y "$d")
  if [ -n "$s" ]; then mins=$(( (e - s) / 60 )); else mins="?"; fi
  head=$(grep -hoE "final mutation score is [0-9.]+ %|new_units_added: +[0-9]+" "$d"/*.log 2>/dev/null | tail -1)
  printf "  %-46s %5s min   %s
" "$n" "$mins" "${head:-no headline}"
done
echo
echo "--- disk ---"
df -h / | awk 'NR==2 {print "  "$4" free of "$2" ("$5" used)"}'
'@
        [string[]] $out = & ssh -o BatchMode=yes -o ConnectTimeout=15 $alias $remote 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Host "    UNREACHABLE" -ForegroundColor Red
            continue
        }
        $out | ForEach-Object { Write-Host "  $_" }
    }

    Write-Host ''
    Write-Host 'Compare the crontab above against ~/measurement-schedule.md.' -ForegroundColor Yellow
    Write-Host 'Drift to look for: a run whose wall minutes now reach into the next slot;' -ForegroundColor Yellow
    Write-Host 'an entry in the doc that is missing from the crontab, or the reverse.' -ForegroundColor Yellow
}

Write-Host ''
Write-Host 'Full detail: ssh mutation-box "tail -40 ~/cron-measure.log"' -ForegroundColor DarkGray
