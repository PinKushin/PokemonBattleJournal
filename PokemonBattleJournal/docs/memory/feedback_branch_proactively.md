---
name: feedback-branch-proactively
description: User expects a feature branch to be created proactively whenever a task makes sense to branch — no need to ask first
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-08-04T15:50:06.651Z
---

Always create a feature branch before starting any task that would reasonably be its own PR: new features, responsive layout changes, multi-file refactors, bug fixes with multiple touches, or anything the user would want to review separately.

**Why:** User said explicitly "when I tell you to do something that probably should be a fork or even make just a little sense to be a fork, fork the repo, and don't wait for me to ask." The tags responsive layout change in session 2026-08-04 was done directly on master when it should have been branched.

**How to apply:** At task start, before editing any files, assess: is this a self-contained change that could be reviewed as a PR? If yes, `git checkout -b feat/<slug>` (or `fix/<slug>`). Don't ask — just branch. Announce the branch name in the first response. Merge/PR are still user-triggered.
