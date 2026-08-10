---
name: project_measurement_boxes_reminder
description: "REMIND THE USER these exist. Two Oracle boxes run Stryker and fuzzing on cron with nothing watching them; the user expects to forget. Run build/check-measurement-boxes.ps1 and report unprompted when a session touches tests, Stryker, fuzzing or CI."
metadata:
  type: project
---

**Set up 2026-08-10. The user's own words: "im so going to just completely forget about these
lol make a note to remind me."** That is an instruction to surface this without being asked.

## What exists

**Quote local time to the user, not UTC.** The boxes run UTC and their crontabs must stay UTC,
but the user is US Eastern and asked directly: "you put them in utc and documented them in utc so
i have no idea what they are my time."

| Box | Job | UTC | Local (EDT) | Local (EST) |
|---|---|---|---|---|
| `mutation-box` | `stryker-core`, ~38 min | 03:30 daily | **11:30 PM, previous evening** | 10:30 PM |
| `mutation-box` | `stryker-core`, ~38 min | 15:30 daily | **11:30 AM** | 10:30 AM |
| `mutation-box` | `stryker-scraper`, ~4 min | 08:30 Sunday | **4:30 AM Sunday** | 3:30 AM Sunday |
| `fuzz-box` | `fuzz 7200`, 2 hours | 04:00 daily | **12:00 AM - 2:00 AM** | 11:00 PM - 1:00 AM |

`mutation-box` is 3 OCPU / 18 GB, `fuzz-box` is 1 OCPU / 6 GB.

The 03:30 UTC row is the one that misleads: locally it lands the **previous evening**, so it is
late Tuesday night rather than Wednesday morning. Say which side of midnight it falls on.

The 8:07 PM check therefore reports on the 11:30 AM run, roughly 8.5 hours old — well inside the
36-hour stale threshold, in both halves of the year.

`ssh mutation-box` / `ssh fuzz-box`, key `~/.ssh/oci-measure`. Results land in
`~/measurements/<stamp>-<sha>-<mode>/` (pruned to 30) and `~/cron-measure.log`.

## The check

```powershell
./build/check-measurement-boxes.ps1
```

Reports cron entry count, uptime, disk, the last run's age and its headline number. It checks
**recency**, not just reachability — a box that answers SSH but last ran four days ago is the
interesting case.

## Why a reminder is warranted rather than fussy

Both failure modes here are **silent**, which is exactly the class of problem this project keeps
running into:

1. **Oracle reclaims Always Free compute that stays idle.** The box does not break, it ceases to
   exist, and the first symptom is an SSH timeout. The twice-daily cadence exists to clear that
   threshold — see the global CLAUDE.md arithmetic — but it only works if the jobs keep running.
2. **A cron job that fails the same way every night produces no signal at all.** Already happened
   once before the schedule was even live: the Scraper's `break: 90` with `low: 70` made Stryker
   refuse to start, so the weekly gate would have failed silently forever. Found by running the
   job, not by reading the config.

## A scheduled task already pushes this — do not create a second one

`measurement-box-check`, created 2026-08-10, runs **daily at 8pm local** and sends one
`PushNotification` with the result. 8pm because 10am is during the user's work hours; they said
so explicitly. It observes only — it must never change the boxes, the repo or the schedules.

Task file: `~/.claude/scheduled-tasks/measurement-box-check/SKILL.md`. `list_scheduled_tasks` to
check it still exists. Scheduled tasks only run while the app is open; a missed run fires on next
launch.

## When to bring it up

A session that touches tests, Stryker, fuzzing, CI or coverage should run the check and mention
the result in passing. If a score has moved, say so with the number — that is the entire point of
paying for the cadence. Do not make it a ritual on unrelated work.

## Related

- [[project_stryker_mutation_testing]] — scores, thresholds, and the `--solution` requirement
- [[project_fuzzing]] — read `ft:`, not `cov:`
