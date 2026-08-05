---
name: feedback_commit_push_policy
description: Commit freely without asking (build must pass, zero warnings preferred); push sparingly — only to test against CI or once stable, not every commit
metadata:
  type: feedback
---

**Commit freely, no need to ask first.** This overrides the global CLAUDE.md default
("never commit unless the user explicitly asks") for this project specifically. Every
commit must build successfully first — optimally zero warnings, per [[feedback_engineering_principles]]'s
zero-warnings policy, but a passing build is the hard requirement.

**Push sparingly.** Only push when: (a) explicitly testing something against CI, or
(b) the work is stable/complete. Do not push on every commit — each push triggers all
three GitHub Actions workflows (ci.yml, ui-tests-windows.yml, ui-tests-android.yml), and
the user is on a limited/metered internet connection. Local commits accumulate freely;
batch them into a push when there's an actual reason to.

**Why:** stated directly 2026-08-05, after a stretch of rapid pushes (5+ in under an hour)
for CI-diagnosis work — each one legitimately needed CI feedback at the time, but the user
wants the *default* to be local-commit-only, with pushing reserved for when CI feedback is
actually the goal.

**How to apply:** after any bounded chunk of work (a fix, a doc update, a test change),
commit it locally with a proper body (see [[feedback_commit_descriptions]]) without asking.
Only run `git push` when: the user asks, the change needs CI validation to know if it
worked (e.g. a CI-config fix), or a logical unit of work is finished and stable enough to
share. Combine with [[feedback_branch_proactively]] — branch for anything PR-worthy, commit
freely on that branch, push when there's a reason to.

## Related

- [[feedback_branch_proactively]] — when to create a branch in the first place
- [[feedback_branch_merge_policy]] — merge is a separate, more careful gate than push
- [[feedback_commit_descriptions]] — every commit needs a body, not just a subject
