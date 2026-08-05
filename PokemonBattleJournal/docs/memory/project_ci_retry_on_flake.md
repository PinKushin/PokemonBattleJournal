---
name: project_ci_retry_on_flake
description: Plan to add a single automatic retry to CI UI test jobs so a one-off runner flake doesn't need a manual gh run rerun
metadata:
  type: project
---

**Status: proposed, not yet implemented (2026-08-05).**

Confirmed on `master` this session: a Windows UI Tests run (`30972824616`) failed hard
(cascading `NoSuchElementException`, 44s finds) with zero code diff from the previous
green run — pure runner-side flake. `gh run rerun` came back 10/10 green. Manually
re-running works but is a human-in-the-loop step; want CI to self-heal for exactly this
case (one bad runner instance), while still failing loudly on a real regression.

## Plan

- **Windows** (`ui-tests-windows.yml`): wrap the `dotnet test` step with
  `nick-fields/retry@v3`, `max_attempts: 2`. Straightforward — it's a plain shell step.
- **Android** (`ui-tests-android.yml`): `reactivecircus/android-emulator-runner` is a
  composite action that boots the emulator AND runs the script — can't wrap the whole
  step in `nick-fields/retry` without re-booting the emulator per attempt (~1 min extra,
  acceptable). Simpler alternative: retry only `dotnet test` inside the `script:` shell
  line itself (a small `for i in 1 2; do dotnet test ... && break; done` — keep the
  existing single-line semicolon-chained logcat capture working, see
  [[project_android_ci_gpu_flake]] for why that step is a single line).
- Only retry the whole test run once — a real regression should still fail on attempt 2,
  not mask forever. This is NOT a substitute for fixing genuine flaky tests (e.g.
  [[feedback_android_flaky_tap_retry]]) — it targets one-off bad-runner-instance noise
  only.

## Related

- [[project_android_ci_gpu_flake]] — the single-line script constraint this has to respect
- [[project_ci_workflows]] — workflow structure
