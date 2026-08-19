---
name: project_fuzzing
description: "SharpFuzz + libFuzzer harness in PokemonBattleJournal.Fuzz, four modes on the first input byte. Runs on fuzz-box (ARM64). Read `ft:`, NOT `cov:` — cov stays at 8 forever and says nothing about .NET coverage."
metadata:
  type: project
---

**Added 2026-08-09**, moved to the Oracle `fuzz-box` on 2026-08-10. The other half of Stryker:
mutation testing grades the assertions against inputs somebody thought of, fuzzing finds the
inputs nobody thought of.

```bash
ssh fuzz-box
cd ~/pbj && bash build/run-measurements.sh fuzz 300     # seconds of fuzzing
```

## The harness

`PokemonBattleJournal.Fuzz/Program.cs` multiplexes on the **first input byte** into four modes,
with a NUL byte separating the two sides where a mode needs two strings. Seeds are therefore
written once per mode byte — a seed's own first byte would otherwise decide which mode it
reaches, and three of the four would start from nothing.

`SplitLines` is **deliberately duplicated** in the harness rather than called from Core. The
property under test is that a reconstruction agrees with the original; sharing the splitter
would make the two agree by construction, which is exactly the "test that cannot fail" shape in
[[feedback_tests_that_cannot_fail]].

The harness itself was wrong on the first run and the fuzzer falsified it in under 60 seconds
with `existing=["d","e","d","g"]`. It asserted that the merged tag list was globally distinct;
the actual contract is that Append introduces no NEW duplicate, since `ResolveTags` passes
`existing` through verbatim. **An assertion about the code is a claim, and the fuzzer checks the
claim, not the intent.**

## Read `ft:`, not `cov:` — measured 2026-08-10, not assumed

Every run prints `cov: 8`, and it stays 8 no matter what. That number is **not** .NET coverage:
libFuzzer reports `Loaded 1 modules (62 inline 8-bit counters)`, which is the native
`libfuzzer-dotnet` bridge's own code. SharpFuzz's IL instrumentation arrives as **features**.

Verified by manipulation — same corpus, same binary, only the instrumentation removed:

| | `cov` | `ft` | corpus | new units | exec/s |
|---|---|---|---|---|---|
| **instrumented** (`sharpfuzz Core.dll`) | 8 | **442** | 108 | **1108** | 18,683 |
| **control** (published, not instrumented) | 8 | **8** | 1 | 3 | 34,848 |

Without instrumentation the corpus collapses to a single input and the fuzzer is doing random
guessing. So: a run showing `ft:` in the hundreds and a growing corpus is working; a run showing
`ft: 8` has silently lost its instrumentation and is worthless. `cov: 8` in BOTH columns is why
it cannot be the health check.

The ~2x exec/s cost is the instrumentation, and it is the good kind of slow.

## Instrument Core, never the harness

`sharpfuzz "${HOME}/fuzz-out/PokemonBattleJournal.Core.dll"` — the feedback has to come from the
code under test. Instrumenting the harness would steer the fuzzer toward its own dispatch
switch.

## Baseline on fuzz-box (1 OCPU ARM64), 2026-08-10, commit b5562ba

```
Done 5623674 runs in 301 second(s)
stat::average_exec_per_sec:     18683
stat::new_units_added:          1108
stat::peak_rss_mb:              28
cov: 8 ft: 442 corp: 108/9910b
```

No findings. 28 MB peak means the 6 GB box is enormously over-provisioned for this — the
constraint is single-core throughput, so longer runs, not a bigger box.

## Findings are regression fixtures, not bug reports

A crash is written to `~/findings/` as the **exact bytes** that caused it. Copy it into the repo
as a test case; do not paraphrase it into a hand-written test.

## Not yet covered

`RestoreService` and `TrainerHillImportService` — both need a live SQLite database per
iteration, which at ~19k exec/s is not viable as written. Would need an in-memory connection
reused across iterations with a rollback, which is a real piece of work rather than another mode
byte.

## Related

- [[project_stryker_mutation_testing]] — the other half; assertions vs inputs
- [[feedback_tests_that_cannot_fail]] — why the splitter is duplicated

## Crash preservation — libFuzzer's own artifact writing NEVER RAN here

**Fixed 2026-08-12.** `-artifact_prefix` writes nothing through `libfuzzer-dotnet`. On a managed
exception SharpFuzz aborts the .NET child, the bridge dies with it, and libFuzzer's crash handler
never runs: **no `crash-*` file and no "Test unit written to" line, while the exception prints in
full.** A run therefore REPORTS the defect and silently loses the input needed to reproduce it.

Found by the Tf2DemoSalvage agent against a real crash; PBJ shared the defect and had simply never
crashed, so `~/findings` sat empty since 10 Aug looking like good news. **~148M executions a night
with no way to keep a reproducer.**

Verified independently on PBJ's harness with a control, which is the part worth copying: run the
self-test crash with `-artifact_prefix` set and `PBJFUZZ_CRASH_DIR` **unset**. libFuzzer wrote
**0 files**. With the env var set, 1 file. So the file is ours, not libFuzzer's.

**The corpus is not a fallback.** libFuzzer only adds coverage-increasing inputs, and a crashing
input is never added — replaying the corpus afterwards isolates nothing.

Two details carry the fix in `Program.cs`:

- **Copy the input before calling the target.** libFuzzer reuses its buffer, so the span is not
  valid once the exception is in flight.
- **Write from an exception FILTER, not a catch.** The filter runs while the exception is still
  propagating and always returns `false`, so the exception reaches SharpFuzz unchanged.
  Catch-and-rethrow would rewrite the stack trace, and the trace is the other half of a finding.

**`PBJFUZZ_SELFTEST=1` makes every input throw, and the runner uses it as a GATE** before the real
run — one execution, assert a crash file appeared, `exit 1` with the log if not. That is the
durable lesson: an empty findings directory is indistinguishable from a clean run, so the only way
to tell them apart is to crash on purpose. Prove the instrument, then trust the measurement. See
[[feedback_tests_that_cannot_fail]].

## The runner pulls ITSELF, so a runner change takes effect one run late

`run-measurements.sh` does `git reset --hard origin/master` near the top. Git replaces the file by
rename, so the running bash keeps reading the **old inode** and finishes executing the old body —
while the banner prints the NEW SHA, because that comes from `git rev-parse` after the pull.

Seen for real on 2026-08-12: a run announced `c17ea33` and produced neither the `.owner` file nor
the `selftest.log` that commit added. Nothing was wrong with the code; the next invocation was
correct. **When verifying a runner change on the box, run it twice, or pass `--no-pull` on a clone
that is already up to date.** Judging the change by the first run's output is judging the previous
version.

## Second suite: `fuzz-importrestore` — the file parsers (added 2026-08-18)

The four original targets are pure string functions and are **saturated** (ft flat ~446). The real
attack surface — `TrainerHillImportService` and `RestoreService`, which parse a stranger-supplied
import or backup file — was deliberately left out because each needs a SQLite connection per
iteration (~430 exec/s vs tens of thousands). It runs as a SEPARATE suite selected by
`PBJFUZZ_SUITE=importrestore` so it never shares a budget with the fast targets, and is booked on
fuzz-box at 09:30 daily, 3h. Unlike the pure targets it is NOT saturated — ft ~1150 and climbing.

Harness is `ImportRestoreFuzz.cs`; `Program.cs` dispatches on the env var and the selftest gate
lives in `Run` so both suites prove crash preservation. Key design points:

- **Fresh temp SQLite per iteration, closed and deleted.** libFuzzer replays a crash in isolation,
  so a crash depending on accumulated state would not reproduce — it would read as flaky. Verified
  zero leaked temp DBs over 17k iterations.
- **The invariant is `written == reported`, not "did not throw".** The services swallow parse
  failures and report them, so an escaping exception is already a finding the fuzzer catches. The
  added property is that the match count the parser SAYS it wrote equals the rows actually in the
  DB — silent miscounting no round-trip test over app-produced files can catch.
- **Import seeds MUST use result `win`/`loss`/`tie`, not `W`.** `W` does not parse, imports zero,
  and leaves the count invariant comparing 0==0 — vacuous. This was caught during verification: the
  first seed used `W` and the sabotage-invert did not fire until the seed was fixed. The runner's
  seed comment says so.

Manipulation-verified in WSL against the real libfuzzer-dotnet: sabotage-inverting the count check
FIRED on a successful import and preserved the exact input bytes; the positive control (correct
check + valid import) ran clean. See [[feedback_tests_that_cannot_fail]].

**A single fuzz target is one libFuzzer worker**, so cores do not speed it — fuzz-box's 1 core is
fine and mutation-box's 3 would not help. Do not move it there expecting a speedup.
