---
name: feedback-branch-merge-policy
description: "Auto-merge feature branches to master once done + all tests pass locally (incl. UI); never auto-PR to repos user doesn't own"
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-08-04T15:51:51.127Z
---

After completing a feature/fix branch:
1. Run unit tests, integration tests, AND UI tests (Windows + Android) locally.
2. If all pass, merge the branch to master automatically — no need to ask.
3. Never open a PR to a repo the user does not own.
4. No auto-PR at all — merges happen locally via `git merge`.

**Why:** User said "auto merge as soon as the feature or fix is done, and all tests pass locally including ui" and "no auto merge, don't PR to repos I do not own."

**How to apply:** Merge gate = unit + integration + both UI test suites green. Only then `git merge --no-ff feat/<slug>` into master and delete the branch. If any test suite fails, stop and report what failed — do not merge.
