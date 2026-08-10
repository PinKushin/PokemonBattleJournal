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
| 20:00 Sunday | `corpus` | Tf2DemoSalvage | **being measured** | **blocked** |

**Rows marked "not yet" are reservations, not reality — `crontab -l` will not show them.** They
are listed so nobody books over them, and they get installed once their runtime is measured.

**`core` is 33 minutes, not the 11-15 first assumed.** That estimate came from comparing against
a run of a different suite. It matters: at 33 min a `cli` job at 09:20 would land inside `core`'s
window and be refused by the lock — silently skipped, every single day. Hence 09:45.

**`corpus` is blocked on a measurement, deliberately.** The local figure is 1h25m and the box has
measured ~8x slower than local on `core`, which puts corpus near 12 hours — longer than the
**11-hour** window between PBJ's 19:40 finish and its 07:00 start. If it does not fit, no start
time fits, and booking it would silently skip a PBJ run every week. A bounded timing run
(hard-stopped at 11 hours, since exceeding that answers the question by itself) settles it before
anything is installed.

### `fuzz-box`

| Time | Job | Project | Measured |
|---|---|---|---|
| 07:00 daily | `fuzz 7200` | PokemonBattleJournal | 2 h by budget |

**Free and sensible:** `fuzz-box` 19:00-21:00. `mutation-box` has room mid-afternoon and
overnight, but leave margin after a neighbour rather than butting against it.

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

   What is safe, verified on 2026-08-10: LFS objects live in `.git/lfs/objects` and
   `git reset --hard` does not touch them, so `git lfs pull` on an up-to-date clone downloads
   **nothing**. All 8 objects on `mutation-box` carry the mtime of the original clone; a later
   run's pull wrote none. Daily and weekly jobs are therefore free. New bandwidth is spent only
   when new corpus files are actually added.

   If a clone ever does need replacing, copy `.git/lfs/objects` aside first and put it back
   before the first `git lfs pull`.

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
