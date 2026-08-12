# Measurement box schedule and house rules

Canonical copy lives in the PokemonBattleJournal repo at `build/measurement-schedule.md` and is
deployed to `~/measurement-schedule.md` on both boxes by `build/install-measurement-cron.sh`.
Edit the repo copy, never the box copy — the box copy is overwritten.

**Read this before booking anything on either box.** It is shared infrastructure: several
projects run here, and the failure mode of getting it wrong is silent.

## Authority

- **`crontab -l` on the box is the truth about TIMES.** This document can go stale; the crontab
  cannot. If they disagree, the crontab is right and this file needs fixing.
- **This document is the truth about POLICY** — who owns scheduling, what a runner must do, which
  slots are spoken for.
- **Settled decisions only.** Anything still being worked out — a blocker, a slot request, a
  measurement in flight, the reasoning behind a change — goes in the cross-agent log at
  `C:\Users\pinku\source\repos\PinKushin\MEASUREMENT-BOX-LOG.md` on the owner's machine, newest
  entry at the top. It is deliberately not in any repo and not on the boxes; every agent working
  on these projects can reach it without SSH, and it survives a box being reclaimed.

  Keeping them apart matters: this file is read by someone about to book a slot and has to stay
  short and current, and a thread of discussion inside it buries the live rules. When something
  in the log becomes settled, promote it here — the log has no git history, this file does.

## Weekly review — Monday 12:05 local

```powershell
pwsh -NoProfile -File build/check-measurement-boxes.ps1 -Review
```

Prints each box's full crontab, the last 8 runs with **wall-clock minutes and commits behind**,
and disk. Run by the `measurement-weekly-review` scheduled task, which reports and recommends
but changes nothing.

Monday noon so every daily job has just run — PBJ core ~5 h old, the scraper ~4 h, tf2 core ~3 h.
**Moves to ~20:00 Monday when the owner returns to work**, which loses nothing: PBJ's 19:00 run
ends 19:38, so an evening review reads a `stryker-core` under an hour old.

**Compare wall minutes against the gap to the next slot, not Stryker's "Time Elapsed".** Elapsed
excludes git, LFS and build work; a slot has to cover all of it. Flag anything using more than
about 70% of its gap.

**Two different questions, two different metrics — do not use one for the other.**

| Question | Metric | Why |
|---|---|---|
| Is the job still running at all? | **hours since last run** | A job that stopped firing is a cron/lock problem, and time is the only thing that reveals it. |
| Is the result still valid? | **commits behind** | A score measured on a SHA that is still HEAD is current no matter how old. One measured two hours ago with five commits on top is already wrong. |

The review prints `N behind (M code)` per run — commits from the measured SHA to that repo's
upstream, and how many of those touched anything that can move the score. Age in hours says only
that the clock moved. As the owner put it: *"if im asleep no code is changing, if im away and
messing with you guys the code is changing."*

**Read the `code` number, not the raw one.** Docs, memory files, build scripts and workflows are
neither mutated nor tested, so counting them makes a current score look stale — a single day of
doc commits did exactly that here. Worked example from 2026-08-10:

```
stryker-scraper   18 behind (0 code)   100.00 %   <- current; none of the 18 touched Scraper
tf2-core          18 behind (8 code)    76.59 %   <- genuinely out of date
```

Both numbers stay on screen deliberately. The filtered one is the signal; the raw one is the
control, and a large gap between them means the exclusion list needs checking. That list is an
EXCLUSION list (`*.md`, `docs/`, `build/`, `.github/`) rather than an inclusion list, so a new
source directory still counts — an inclusion list would silently miss it, which fails in the
dangerous direction.

The review exists because slot overrun is silent. The lock refuses rather than queues, so a job
that grows into its neighbour's start time does not error — the neighbour is just skipped, that
day and every day after. It has already happened once: `tf2 core` was booked at 09:00 with `cli`
at 09:20 on an 11-15 minute estimate and then measured at 33 minutes, which would have skipped
`cli` daily.

Also check for doc/crontab disagreement in **both** directions. An entry here but not in the
crontab means a job quietly stopped being scheduled; the reverse means someone booked without
going through the schedule owner.

## Ownership

**One agent owns all crontab edits on both boxes: the PokemonBattleJournal agent.** Not
territorial — a shared machine with several agents editing the same crontab has no single view of
what is booked, and the lock refuses rather than queues, so a double-booking silently skips runs
instead of erroring. Send a request; it gets wired.

A request needs: which box, which command, how long it takes **measured on the box** (not
locally — the box is roughly 2.8x slower than a modern Windows desktop), and how often.

## The boxes

| Alias | Shape | Purpose |
|---|---|---|
| `mutation-box` | 3 OCPU / 18 GB | Stryker mutation testing |
| `fuzz-box` | 1 OCPU / 6 GB | SharpFuzz + libFuzzer |

Both are Oracle Always Free ARM64, set to **`America/New_York`**, so crontab entries are already
in the owner's local time. Do not reintroduce UTC conversion — a fixed UTC entry cannot express
"7am local" and drifts an hour at every daylight saving change.

## Slots (local time)

### `mutation-box`

| Time | Job | Project | Measured | Installed? |
|---|---|---|---|---|
| 07:00 daily | `stryker-core` | PokemonBattleJournal | 38 min | yes |
| 09:00 daily | `core` | Tf2DemoSalvage | **33 min** (2026-08-10) | not yet |
| 09:45 daily | `cli` | Tf2DemoSalvage | unmeasured on box | not yet |
| 19:00 daily | `stryker-core` | PokemonBattleJournal | 38 min | yes |
| 08:15 Sunday | `stryker-scraper` | PokemonBattleJournal | 4 min | yes |
| ~~20:00 Sunday~~ | `corpus` | Tf2DemoSalvage | **18 h 07 m** (2026-08-11) | **withdrawn** |

**Rows marked "not yet" are reservations, not reality — `crontab -l` will not show them.** They
are listed so nobody books over them, and they get installed once their runtime is measured.

**`core` is 33 minutes, not the 11-15 first assumed.** That estimate came from comparing against
a run of a different suite. It matters: at 33 min a `cli` job at 09:20 would land inside `core`'s
window and be refused by the lock — silently skipped, every single day. Hence 09:45.

**`corpus` was blocked on a measurement, and the measurement withdrew it.** The estimate was ~12
hours against an 11-hour window; the run on 2026-08-11 took **18 h 07 m**. No start time fits, so
nothing is booked. That was the whole point of measuring before installing: booking it would have
silently skipped a PBJ run every week, because the lock refuses rather than queues.

**The runtime is a symptom, not a size.** That run scored 100 % — from **1142 timeouts against
183 real kills**. Stryker counts a timeout as a kill, so the score is 86 % manufactured, and at
roughly 57 s per timeout the timeouts account for essentially the entire 18 hours. A mutant that
hangs instead of failing is usually a loop bound or termination condition mutated in code that
reads a stream until exhausted, which is exactly the shape of demo parsing. So the next step is
not a longer window, it is finding what spins — and if a mutated bound can spin, a corrupt demo
probably can too. Re-measure after that; the number will change by an order of magnitude and the
slot question reopens from scratch.

### `fuzz-box`

| Time | Job | Project | Measured | Installed? |
|---|---|---|---|---|
| 07:00 daily | `fuzz 7200` | PokemonBattleJournal | 2 h by budget | yes |
| 19:00 daily | fuzz — `container` + `bitreader` | Tf2DemoSalvage | **budget not yet stated** | **reserved** |

**Tf2DemoSalvage's daily fuzz slot is 19:00, approved by the owner 2026-08-11.** It is a
reservation until the runner names its budget — `crontab -l` will not show it yet. A fuzz run is
bounded by the budget it is given rather than by how long the work takes, so "measured runtime" is
the wrong question here; what is needed is the total across both targets.

**Run both targets every night. Do not alternate them by day.** An earlier version of this file
suggested alternating past ~4 h, and that was mutation-run logic applied to fuzzing, where it does
not hold. A mutation run is a fixed unit of work competing for a window, so squeezing it matters. A
fuzz run is elastic and, more importantly, **cumulative**: libFuzzer grows a corpus across runs, so
alternating does not cost half the schedule, it costs half the exploration rate on each target,
permanently. There is nothing to gain it back with — the window from 19:00 to PBJ's 07:00 is **12
hours**, and both targets at 4 h each would still finish by 03:00. The box is otherwise idle
09:00-19:00 and 23:00-07:00.

More runtime is also the direction the idle rule wants. Oracle reclaims on a 7-day 95th-percentile
AND across CPU, network and memory, so time spent busy is margin, not cost.

Ad-hoc runs outside a booked slot are fine on either box when nothing is scheduled — also the
owner's call, same date. They still take `/tmp/measurement-box.lock`, which is what makes them
safe; the lock is the rule, the schedule is only the plan.

**Room remaining after that:** `fuzz-box` is clear 09:00-19:00 and 23:00-07:00. `mutation-box` has
room mid-afternoon and overnight, but leave margin after a neighbour rather than butting against
it.

**Never schedule a trigger at 02:xx.** That minute does not exist on spring-forward and happens
twice on fall-back, so the job is skipped or run twice. A job that *runs through* 02:00 is fine —
the constraint is on start times only.

## Rules for any runner script on these boxes

1. **`LOCK="/tmp/measurement-box.lock"`** — that exact path. The lock is named for the BOX because
   the machine is what is being serialised. A per-project lock name looks like tidy namespacing
   and means both projects run at once, reported as a plausible mutation score rather than an
   error.
2. **Never `rm` the lock file.** It does not free the lock (flock is on the open file
   description) but it *unlinks the inode*, so the next run creates a fresh file and takes a fresh
   lock while the old holder is still working. That is no lock at all. `ls /tmp/*.lock` showing
   nothing during an active run is the signature.
3. **`9>&-` on every long command.** An inherited fd 9 holds the lock for as long as any
   descendant lives, and .NET leaves descendants alive on purpose: MSBuild node-reuse daemons and
   the Roslyn `VBCSCompiler` outlive the build that spawned them. A finished run held the lock 12
   minutes later and the next launch was refused.
4. **`export MSBUILDDISABLENODEREUSE=1`.** Same problem, fixed at the source.
5. **Do not swallow the workload's exit code.** `cmd | tee log || true` turns a Stryker threshold
   violation *and* a refusal-to-start into exit 0, and the cron log then reads as success. Use
   `set -o pipefail` and check `PIPESTATUS`. This is not hypothetical: a `break: 90` against a
   default `low: 70` makes Stryker refuse to start entirely, and that failed silently until
   someone ran the job by hand.
6. **No unescaped `%` in a crontab command.** cron treats it as a newline and feeds the remainder
   to the job as stdin, so a redirect after one never happens and the job looks like it never
   fired. Escape as `\%` or avoid it (`date -Is`).
7. **cron's environment is nearly empty** and `~/.bashrc` returns early when non-interactive, so
   `DOTNET_ROOT` and `PATH` do not survive. Export them inside the runner. Test with
   `env -i HOME=/home/ubuntu SHELL=/bin/sh PATH=/usr/bin:/bin bash <runner> <mode>`.
8. **Prune your own output.** `~/measurements/` grows without bound; keep the last 30. The boot
   volume is 45 GB and is the entire disk.
9. **Never re-clone a repo that uses Git LFS. Update in place.** `git fetch` plus
   `git reset --hard` is enough and costs nothing; a fresh clone re-downloads every LFS object
   and that bandwidth is metered against the owner's GitHub account, not the box.

   Tf2DemoSalvage's corpus is **305 MB in LFS**, against a **1 GB/month** free-tier bandwidth
   allowance — so one careless re-clone is roughly **30% of the month**, and four would exhaust
   it. There is no budget to raise the cap ([[user_no_signing_budget]] applies to paid dev
   services generally).

   **The metered thing is the corpus, not the repo.** Tf2DemoSalvage's source is a few MB and
   costs nothing to clone as many times as you like. The 305 MB is entirely LFS objects — the
   demos. So re-cloning is the hazard *only because a plain `git clone` fetches them
   automatically*: the LFS smudge filter runs during checkout and pulls every object without being
   asked. That is the trap, and it is worth stating in that order, because "don't re-clone" filed
   under the wrong reason invites someone to conclude a clone is safe once they have skipped
   `git lfs pull`.

   If a clone is genuinely needed, `GIT_LFS_SKIP_SMUDGE=1 git clone …` gets the repo for
   approximately nothing and leaves the objects unfetched.

   **Status as of 2026-08-11: the allowance was hit once and is not drained.** Treat it as a
   one-time accident with a standing lesson, not an ongoing constraint — nothing is throttled and
   nothing needs rationing. The owner's instruction after it is that **the fuzz corpus does not
   come from LFS on the box at all**: demos live on the owner's local machine and are uploaded by
   hand. Tf2's `f910e8b`, "Seed the container target from local demos, not box-side LFS pulls," is
   that change. So a nightly fuzz slot costs no LFS bandwidth at any budget — because its corpus
   never travels through LFS, not because clones are exempt.

   What is safe, verified on 2026-08-10: LFS objects live in `.git/lfs/objects` and
   `git reset --hard` does not touch them, so `git lfs pull` on an up-to-date clone downloads
   **nothing**. All 8 objects on `mutation-box` carry the mtime of the original clone; a later
   run's pull wrote none. Daily and weekly jobs are therefore free. New bandwidth is spent only
   when new corpus files are actually added.

   If a clone ever does need replacing, copy `.git/lfs/objects` aside first and put it back
   before the first `git lfs pull`.
10. **Verify accounted mutants against planned before believing a score.** A truncated Stryker run
    (killed process, SIGINT deadline, interrupted lock holder) still prints *"All mutants have been
    tested"* and a percentage that is internally consistent with whatever subset it held — nothing
    in the normal output flags the shortfall. Print `killed+timeout+survived+nocov` against the
    planned total and treat a gap as a signal, not silence: a small gap can be RuntimeError mutants
    (which count toward planned but appear in no status line the reporter writes), a large one is a
    truncated run. Caught for real on 2026-08-10: a run reporting 37.74% had accounted 218 of 957
    planned mutants — the replacement run at the same commit, uninterrupted, scored 76.59%.

## Why there is a cadence at all

Oracle reclaims Always Free compute that stays idle: the rule is an AND over three 95th-percentile
metrics across 7 days — CPU, network and memory all under 20%. Clearing a 95th percentile means
being busy more than 5% of the week, which is **8.4 hours**. A single 38-minute nightly job is 4.4
hours a week and does not clear it.

Current total on `mutation-box` is roughly 14.6 h/week across both projects, so there is margin.
**Do not add "skip if nothing changed" logic** without redoing this arithmetic — an optimisation
that reduces busy time can hand the box back to Oracle.

An instance that gets reclaimed does not break. It stops existing, and the first symptom is an SSH
timeout.

## What must NOT move here

- **Anything needing an interactive desktop.** WinAppDriver UI tests need a real Windows session
  and there is no Windows licence on the free tier.
- **Benchmarks.** Shared cloud vCPUs are too noisy for BenchmarkDotNet. A benchmark's value comes
  from stable hardware.
- **Anything triggered by a webhook.** These are deliberately not GitHub runners: a public repo
  with `pull_request` triggers would mean stranger-authored workflow code executing here. The box
  pulls; nothing pushes to it.
