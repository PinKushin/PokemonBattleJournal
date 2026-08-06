---
name: project_android_session_poisoning
description: "MEASURED 2026-08-06: an Android Appium session created while the machine is loaded fails every test for its whole life, even after the load stops. AndroidDriver creation time is the leading indicator — ~20-30s healthy, ~69s doomed."
metadata:
  type: project
---

**Never start the Android UI suite while the Windows UI suite (or anything heavy) is running
on this machine.** Not a style preference — measured, with a control.

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

**Worth building:** a threshold check in `AppiumSetup` that fails loudly when driver creation
runs long, instead of proceeding into 79 tests that are all going to fail. A 10-minute run
producing 79 red tests with product-looking error messages is strictly worse than one
immediate "the machine was too busy to create a usable session". Not built yet — see
[[feedback_no_silent_guards]] for why the current behaviour is the bad kind of quiet.

## Direction: Windows breaks Android, not the reverse

Recorded because the intuition points the other way and was written down that way first. In
the concurrent run **the Windows suite passed 80/80 and was not measurably slowed at all** —
`Shell ready` was 244ms while the Android emulator was cold-booting on the same CPU.

The recollection in [[project_self_hosted_runners]] was that Android broke Windows, since
Windows needs its window frontmost and the emulator takes the foreground. Measurement says the
opposite: the focus-sensitive suite was fine, and the emulator-based one collapsed.

## Bonus negative result: load is not a CI simulator

The concurrent run was also an attempt to reproduce [[project_windows_mainpage_click_flake]]
by making this machine slow enough to resemble CI. It does not work. Under full contention
Windows still posted `Shell ready` at 244ms against CI's 8,798ms — roughly 36x faster while
loaded. Simulating CI hardware needs real throttling, not a busy CPU.

## Related

- [[project_windows_mainpage_click_flake]] — the CI flake this was trying to reproduce
- [[feedback_android_flaky_tap_retry]] — the intermittent version of the same click symptom
- [[project_self_hosted_runners]] — why two runners on one desktop was a bad idea
