---
name: feedback_commit_descriptions
description: Every commit must include a body summarizing what was learned and what the commit tests or validates
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-07-30T07:53:11.171Z
---

Every commit must have a description body (not just a subject line) that covers:
1. What was learned or discovered during this work (root causes, platform quirks, non-obvious behavior)
2. What the commit is testing or validating (which scenarios, which edge cases, which regressions it guards)

**Why:** Commits serve as a searchable knowledge base. At the start of any session, `git log --oneline` gives the subject; `git log -p` or `git show <hash>` surfaces the body. A future AI instance hitting the same platform quirk, cascade failure, or flaky test can grep commit bodies and find "we saw this before in commit X, root cause was Y." Without bodies, the history is a list of what changed with no record of what was understood. With bodies, it's a permanent learning log that spans every session.

**How to apply:** After the one-line subject, always add a blank line then a paragraph or bullet list covering the two points above. Apply to every commit, including small fixes — even a one-liner fix may have a non-obvious root cause worth recording.

Example format:
```
fix: assert ShouldNotContain instead of ShouldBeNullOrEmpty after archetype save

Android empty Entry returns placeholder text as .Text, not null/empty string.
ShouldBeNullOrEmpty() always failed on Android even after a successful save.
ShouldNotContain(typedValue) is the correct cross-platform assertion.

Tests: OptionsPage_SaveArchetype_WithName_ClearsInput — guards the clear-on-save
path and catches regressions where the VM early-returns without clearing the field.
```
