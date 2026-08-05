---
name: feedback_ai_docs_duplication_ok
description: DRY doesn't apply to AI-context docs (global CLAUDE.md vs project-local memory/CLAUDE.md) — duplication across them is expected and fine
metadata:
  type: feedback
---

The DRY principle in [[feedback_engineering_principles]] applies to application code, not
to AI-context/instruction docs. Global `~/.claude/CLAUDE.md` and project-local
`docs/memory/*.md` / project `CLAUDE.md` will legitimately overlap — that's fine, don't
try to eliminate the duplication or point one at the other as a single source of truth.

**Why:** the repo doesn't include the global CLAUDE.md (it lives outside any repo, at the
user's home directory), so anything that needs to survive "clone this repo fresh" or "a
different AI tool reads only the project" has to be written into the project's own docs
too, even if the same rule already lives globally. Confirmed 2026-08-05 after adding the
commit-freely/push-sparingly policy to both the global CLAUDE.md and a project-local
`feedback_commit_push_policy.md` — the user clarified the duplication itself is correct,
not something to clean up later.

**How to apply:** when a policy is genuinely universal (applies to every project), write
it in the global CLAUDE.md AND still record it in the current project's memory/CLAUDE.md
if it's relevant there — don't skip the local copy on DRY grounds.
