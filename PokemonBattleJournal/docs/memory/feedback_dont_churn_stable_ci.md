---
name: feedback_dont_churn_stable_ci
description: The UI suites are reliable now and the user is wary of touching CI/test infra. Change it only for a demonstrated problem, fixed at its source, and verify on CI before merging.
metadata:
  type: feedback
---

**Do not churn CI or test infrastructure without a demonstrated, specific problem.** Stated
2026-08-05: *"i think im a little afraid of touching the ci tests anymore because we got them
all working pretty reliably now."*

**Why:** getting both platforms green took an enormous amount of work — six stacked root
causes on Android alone ([[project_android_ci_gpu_flake]]), the long-lived-driver session
degradation that forced the per-fixture matrix ([[project_ci_workflows]]), and the
absent-element timeout costs ([[project_game3tab_ci_flake_recurring]]). That reliability is
hard-won and worth more than any incremental cleanup.

**How to apply:**

- **Never** refactor CI workflows or test helpers for tidiness, consistency, or style alone.
- Change them when there is a measured failure or cost, and say what the measurement is.
- Prefer fixing the *source* of a failure over adding a retry/tolerance layer on top. A
  targeted fix cannot mask a real regression; a blanket retry can. This is why the job-level
  retry plan was shelved in favor of a driver-creation backoff — see
  [[project_ci_retry_on_flake]].
- Verify on CI before merging, not just locally. Workflow changes cannot be tested any other
  way, and "push to test against CI" is an explicitly sanctioned reason to push
  ([[feedback_commit_push_policy]]).
- When a CI failure appears, first determine whether it is infrastructure or a real
  regression before changing anything. Read the timing/setup logs — a job that failed with
  zero tests executed is not a test problem.

**Not a freeze.** Real problems still get fixed; the bar is evidence, not permission. The
cache-contention fix and the WinAppDriver backoff both landed the same day this was stated,
because both had measurements behind them.
