---
name: project_android_session_poisoning
description: "MEASURED: an Appium session created while the machine is loaded is poisoned for its whole life, even after the load stops. Goes BOTH ways — Windows and Android. Driver/server creation time is the leading indicator: ~15-30s healthy, ~69s doomed."
metadata:
  type: project
---

**Never run a UI suite while anything heavy is running on this machine — in EITHER
direction.** Not a style preference; measured, with a control, in both directions.

## The measurement

Three runs of the identical 79-test Android suite, same commit, same day.

| Run | Emulator boot | Appium server | `AndroidDriver created` | Result |
|---|---|---|---|---|
| Alone, emulator pre-booted | (warm) | 21,790ms | 19,481ms | **79/79** |
| Alone, cold boot *(control)* | 39,616ms | 21,804ms | 30,022ms | **79/79** |
| Concurrent with Windows suite | 48,165ms | 21,790ms | **69,253ms** | **0/79** |

The control is the load-bearing part. The failing run cold-booted its emulator *and* had the
Windows suite running, so two things differed from the passing run at once. Running cold-boot
alone passes, which leaves contention as the cause.

Failure mode: every fixture dies in `OneTimeSetUp` with
`Navigation drawer did not open after 3 clicks`. Clicks dispatch and no handler runs — the
same shape as [[feedback_android_flaky_tap_retry]], but permanent rather than intermittent,
so the three-attempt retry cannot save it.

## The session is poisoned, not the moment

This is the part that makes it hard to recognise. **The failures continue after the load
stops.** Windows finished at 13:49:28; the Android driver was not created until 13:50:13 and
the tests ran from 13:50:15 to 13:53:11 on an otherwise idle machine — and still failed all 79.

So it is not "taps miss while something else is busy". Something about a session *created*
under contention stays broken for its whole life. Anyone debugging this by looking at what
else was running *at the moment of failure* will find nothing and conclude the app is broken.

## How to recognise it

`AndroidDriver created` in `%TEMP%\UITests.Android.setup.log`:

- **19,000-30,000ms** — healthy, across both warm and cold boots
- **~69,000ms** — the run is already lost; every test will fail

Emulator boot time is a much weaker signal (39.6s healthy vs 48.2s doomed). Driver creation is
the one that separates cleanly, at more than double.

**Worth building:** a threshold check in `AppiumSetup` — on BOTH platforms — that fails loudly
when server startup or driver creation runs long, instead of proceeding into a run that is
already lost (79 red Android tests, or a six-minute Windows hang producing nothing). A 10-minute run
producing 79 red tests with product-looking error messages is strictly worse than one
immediate "the machine was too busy to create a usable session". Not built yet — see
[[feedback_no_silent_guards]] for why the current behaviour is the bad kind of quiet.

## Direction: it goes BOTH ways (corrected 2026-08-08)

Recorded because the intuition points the other way and was written down that way first. In
the concurrent run **the Windows suite passed 80/80 and was not measurably slowed at all** —
`Shell ready` was 244ms while the Android emulator was cold-booting on the same CPU.

The recollection in [[project_self_hosted_runners]] was that Android broke Windows, since
Windows needs its window frontmost and the emulator takes the foreground. Measurement says the
opposite: the focus-sensitive suite was fine, and the emulator-based one collapsed.

### But do NOT read that as "Windows is immune" — it is not

**Corrected 2026-08-08, at the cost of a dead run.** The heading above used to read "Windows
breaks Android, not the reverse", and that phrasing invites exactly the wrong inference: that
loading the machine during a *Windows* run is safe. It is not. The 2026-08-06 measurement
tested one direction; it is evidence about that direction, not a guarantee about the other.

What happened: an emulator boot landed on Windows `AppiumSetup`, and an Android
`-t:Install` deploy ran through session creation. Result — `AppiumServer started (69652ms)`,
WinAppDriver then absent from the process list entirely, and the testhost hung for six minutes
with zero tests run and an empty output file.

Restarting on an idle machine, same commit, same code:

| Condition | `AppiumServer started` | Outcome |
|---|---|---|
| Emulator boot + Android deploy concurrent | **69,652ms** | hung, 0 tests, WinAppDriver dead |
| Idle machine *(control)* | **15,344ms** | ran normally |

**4.5x**, and note the doomed number is ~69s on BOTH platforms — the same figure that marks a
poisoned AndroidDriver. That is the number to watch whichever suite is running.

The 2026-08-06 run had Windows survive concurrency; this one did not. The difference is which
phase the load lands on. Windows tolerated load *during its test body*; it did not tolerate
load *during Appium server startup and session creation*. **Setup is the fragile window, on
both platforms.**

Practical rule, replacing the directional one: start a UI suite only on an idle machine, and
do not boot an emulator, deploy, build, or run Stryker until it has finished. Deploying and
booting AHEAD of the run is fine — it is overlap with setup that kills it.

## Bonus negative result: load is not a CI simulator

The concurrent run was also an attempt to reproduce [[project_windows_mainpage_click_flake]]
by making this machine slow enough to resemble CI. It does not work. Under full contention
Windows still posted `Shell ready` at 244ms against CI's 8,798ms — roughly 36x faster while
loaded. Simulating CI hardware needs real throttling, not a busy CPU.

## Related

- [[project_windows_mainpage_click_flake]] — the CI flake this was trying to reproduce
- [[feedback_android_flaky_tap_retry]] — the intermittent version of the same click symptom
- [[project_self_hosted_runners]] — why two runners on one desktop was a bad idea
