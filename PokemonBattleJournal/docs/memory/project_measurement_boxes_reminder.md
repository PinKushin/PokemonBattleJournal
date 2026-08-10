---
name: project_measurement_boxes_reminder
description: "REMIND THE USER these exist. Two Oracle boxes run Stryker and fuzzing on cron with nothing watching them; the user expects to forget. Run build/check-measurement-boxes.ps1 and report unprompted when a session touches tests, Stryker, fuzzing or CI."
metadata:
  type: project
---

**Set up 2026-08-10. The user's own words: "im so going to just completely forget about these
lol make a note to remind me."** That is an instruction to surface this without being asked.

## What exists

| Alias | Shape | Schedule (UTC) |
|---|---|---|
| `mutation-box` | 3 OCPU / 18 GB | `stryker-core` 03:30 and 15:30 daily; `stryker-scraper` 08:30 Sunday |
| `fuzz-box` | 1 OCPU / 6 GB | `fuzz 7200` 04:00 daily |

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

## When to bring it up

A session that touches tests, Stryker, fuzzing, CI or coverage should run the check and mention
the result in passing. If a score has moved, say so with the number — that is the entire point of
paying for the cadence. Do not make it a ritual on unrelated work.

## Related

- [[project_stryker_mutation_testing]] — scores, thresholds, and the `--solution` requirement
- [[project_fuzzing]] — read `ft:`, not `cov:`
