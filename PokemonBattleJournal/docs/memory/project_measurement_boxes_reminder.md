---
name: project_measurement_boxes_reminder
description: "REMIND THE USER these exist. Two Oracle boxes run Stryker and fuzzing on cron with nothing watching them; the user expects to forget. Run build/check-measurement-boxes.ps1 and report unprompted when a session touches tests, Stryker, fuzzing or CI."
metadata:
  type: project
---

**Set up 2026-08-10. The user's own words: "im so going to just completely forget about these
lol make a note to remind me."** That is an instruction to surface this without being asked.

## What exists

**Both boxes are set to `America/New_York`, so their crontabs are already in the user's local
time.** Quote them as-is; do not convert to UTC. The user asked for this directly: "you put them
in utc and documented them in utc so i have no idea what they are my time."

| Box | Shape | Local time | Job |
|---|---|---|---|
| `mutation-box` | 3 OCPU / 18 GB | **7:00 AM and 7:00 PM daily** | `stryker-core`, ~38 min |
| `mutation-box` | | **8:15 AM Sunday** | `stryker-scraper`, ~4 min |
| `fuzz-box` | 1 OCPU / 6 GB | **7:00 AM daily, to ~9:00 AM** | `fuzz 7200`, 2 hours |

**7am is the user's choice, and the reason matters:** they are on workers' comp at the time of
writing, so results waiting in the morning mean a day off is a day they can act on the numbers.
The 8:07 PM notification then catches them on days they do work. Do not "tidy" these times.

The 8:07 PM check reports on the 7:00 PM `stryker-core`, which finishes around 7:38 PM — roughly
30 minutes old, the freshest it has ever been.

Two constraints, if these ever move again. Both boxes can share 07:00 because the `flock` is per
box. Same-box jobs cannot share a slot, because the lock REFUSES rather than queues — a collided
job is silently skipped for that day, which is why the Sunday scraper sits at 08:15 rather than
07:00.

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

`measurement-box-check`, created 2026-08-10, fires **twice daily — 9:30 AM and 8:30 PM local** —
and sends one `PushNotification` each time. Roughly half an hour after each batch of work
finishes; the user asked for both ("a notification about 30 min after each run is fine with me").
It observes only — it must never change the boxes, the repo or the schedules.

**One task with `cronExpression: "30 9,20 * * *"`, not two tasks.** Two would mean two copies of
the same prompt drifting apart. The UI renders only one time ("At 09:37 AM, every day"), which
looks like the list was collapsed — it was not. Verified by setting `30 20,4` and reading
`nextRunAt`: it returned 4:37 AM, the *second* element, so the scheduler evaluates the whole
list and only the human-readable string is a poor renderer.

The task's prompt has to say what "fresh" means at each firing, because it differs: in the
evening the newest fuzz result is ~11 hours old and that is CORRECT, not stale. Without that, the
evening report would flag a healthy box.

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
