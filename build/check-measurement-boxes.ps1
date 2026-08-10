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
    [int] $StaleHours = 36
)

$ErrorActionPreference = 'Stop'

# Deliberately not a single ssh call per box with everything crammed in: when a box is gone,
# the failure needs to be attributable to that box rather than to a parse error downstream.
# Schedules are stated in LOCAL time with UTC in brackets, not the other way round. The
# crontabs on the boxes are UTC and must stay UTC, but a UTC-only readout is unreadable to
# whoever is running this — and 03:30 UTC is the PREVIOUS EVENING locally, which is exactly
# the kind of thing a mental conversion gets wrong.
[hashtable[]] $boxes = @(
    @{ Alias = 'mutation-box'; Cron = @{ 'stryker-core' = @(3, 30), @(15, 30); 'stryker-scraper (Sun)' = , @(8, 30) } },
    @{ Alias = 'fuzz-box';     Cron = @{ 'fuzz 7200 (2h)' = , @(4, 0) } }
)

function Format-LocalSchedule {
    <#
        Converts a UTC hour/minute to the local equivalent for TODAY, so the printed time
        follows daylight saving instead of being pinned to whatever offset was true when this
        script was written. Flags the day shift explicitly rather than leaving a bare clock
        time that silently belongs to yesterday.
    #>
    param([hashtable] $Cron)

    [string[]] $parts = @()
    foreach ($job in ($Cron.Keys | Sort-Object)) {
        [string[]] $times = @()
        foreach ($hm in $Cron[$job]) {
            [datetime] $utc = [datetime]::SpecifyKind(
                [datetime]::UtcNow.Date.AddHours($hm[0]).AddMinutes($hm[1]), 'Utc')
            [datetime] $local = $utc.ToLocalTime()
            [string] $stamp = $local.ToString('h:mm tt')
            if ($local.Date -lt $utc.Date) { $stamp += ' prev evening' }
            $times += "$stamp ($($utc.ToString('HH:mm')) UTC)"
        }
        $parts += "$job $($times -join ', ')"
    }
    return $parts -join '; '
}

foreach ($box in $boxes) {
    [string] $alias = $box.Alias
    Write-Host ''
    Write-Host "=== $alias ===" -ForegroundColor Cyan
    Write-Host "    expected: $(Format-LocalSchedule -Cron $box.Cron)" -ForegroundColor DarkGray

    # BatchMode so a missing or passphrase-locked key fails immediately instead of hanging on
    # an interactive prompt this script cannot answer.
    [string] $remote = @'
echo "cron:  $(crontab -l 2>/dev/null | grep -c pbj-measurements) entries"
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

Write-Host ''
Write-Host 'Full detail: ssh mutation-box "tail -40 ~/cron-measure.log"' -ForegroundColor DarkGray
