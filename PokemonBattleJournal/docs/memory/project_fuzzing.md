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
