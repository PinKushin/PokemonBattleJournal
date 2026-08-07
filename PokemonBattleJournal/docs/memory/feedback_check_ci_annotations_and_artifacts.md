---
name: feedback_check_ci_annotations_and_artifacts
description: "A green CI tick is not a clean run. Read annotations AND artifact sizes on every run before reporting CI as passing — the user asked for this explicitly, and it has already caught a workflow that had never once succeeded."
metadata:
  type: feedback
---

**The user, 2026-08-07:** *"yea the ci's artifacts and annotations especially should always be checked, i think i added that to the global claude earlier."*

They had. The rule is in the global `CLAUDE.md` under "CI annotations count as build output", and it names the exact failure mode: `actions/upload-artifact` reporting "No files were found with the provided path" is a **warning**, so a workflow that has stopped publishing its output still shows a green tick.

## Why this is a standing instruction and not a nicety

It was already violated in this repo, twice over, in the same session it was quoted back:

- **The CodeQL workflow had never once succeeded.** Added 2026-08-07, failed on every run since at the SARIF upload step (`CodeQL analyses from advanced configurations cannot be processed when the default setup is enabled`). Nobody looked, so the repo had a security scanner that produced nothing while appearing configured. Not even a green-with-warnings case — a plain red X that went unread. See [[project_sentry_privacy_audit]] for the session it surfaced in.
- **A "success" run carried a CA1416 warning.** `scraper-monitor` run 31114820173, conclusion `success`, one annotation. That one turned out to be stale (it predated `GlobalSuppressions.cs` by ~16 hours) — but staleness was only establishable by *reading* it.

## What to actually do

Not "eyeball the log". Two API calls, on every run being reported as green:

```bash
# annotations — hold at zero, like compiler warnings
gh run view <run-id> --json jobs --jq '.jobs[].databaseId'
gh api repos/{owner}/{repo}/check-runs/{job-id}/annotations --jq 'length'

# artifacts — a 0-byte or missing artifact means a step silently did nothing
gh api repos/{owner}/{repo}/actions/runs/<run-id>/artifacts \
  --jq '.artifacts[] | "\(.name) \(.size_in_bytes)"'
```

Rules that follow:

- **Treat every annotation as a defect until shown otherwise.** Prove staleness with a date comparison against the commit that should have fixed it; do not assume.
- **Check artifact SIZE, not just presence.** The dangerous artifact is the one that uploaded successfully with nothing in it.
- **A run still in progress reports zero annotations.** That zero is meaningless — confirm `conclusion` is non-empty before believing any count.
- **After editing a workflow, verify the NEXT run**, not just its status. Moving or inserting a job is exactly the edit that reattaches steps to the wrong job, and that mistake passes.

## Related

- [[feedback_dont_poll_ci]] — check properly, but do not hammer the API doing it; one blocking `gh run watch` beats a polling loop
- [[feedback_dont_churn_stable_ci]] — reading CI carefully is the alternative to adding retry layers on top of it
- [[project_ci_workflows]] — the workflow layout these checks apply to
