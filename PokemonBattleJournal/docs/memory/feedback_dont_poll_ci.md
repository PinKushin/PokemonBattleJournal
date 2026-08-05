---
name: feedback_dont_poll_ci
description: "Don't poll `gh run list` on a short interval waiting for CI — it burns the GitHub API rate limit. Wait the run's expected duration first, then check a handful of times."
metadata:
  type: feedback
---

**Waiting for CI is not a reason to poll it hard.** User, 2026-08-05, on a loop checking
`gh run list` every 20 seconds for up to 30 minutes: *"dont poll that often"* / *"you are
going to hit the rate limit."*

That loop would have made ~90 API calls to learn one bit of information, and GitHub's REST
limit is shared with everything else `gh` does in the session — including the calls actually
needed to read logs and download artifacts afterwards. Exhausting it while idling means the
useful calls fail.

**Do this instead:** sleep for the run's expected duration *first*, then check a small number
of times with a few minutes between.

```bash
sleep 660 && for i in 1 2 3 4; do
  # gh run list ... ; exit 0 when nothing is in_progress/queued
  sleep 240
done
```

Four calls instead of ninety, and it usually resolves on the first one.

Current durations to size the initial sleep from ([[project_ci_workflows]]):
Windows UI ~9m30s–10m, Android UI ~11m, CI (unit + integration) shorter.

The same restraint applies to any external system the harness cannot notify us about. Harness
-tracked background work re-invokes automatically and needs no polling at all.

## Related

- [[feedback_commit_push_policy]] — the same underlying concern: CI runs and bandwidth cost
  the user something, so don't spend them idly
- [[feedback_dont_churn_stable_ci]] — don't touch working CI without a measured problem
