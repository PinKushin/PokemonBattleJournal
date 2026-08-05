---
name: feedback_fix_stale_docs_immediately
description: Fix stale docs the moment they are noticed — never queue them, never leave them. Standing instruction.
metadata:
  type: feedback
---

**When you notice a doc or memory entry is wrong, fix it immediately.** Do not add it to a
backlog, do not mention it and move on, do not batch it for later. Stated 2026-08-05: *"fix
stale docs immediately never leave them please."* Reinforced by an earlier statement the same
day: *"i hate docs being out of date."*

**Why:** these files are the context a future session loads instead of re-deriving. A wrong
entry is worse than a missing one — it actively sends the next session down a dead path.
Concrete example: [[project_windows_picker_ci]] described a `WindowHandles`-iterating picker
helper that had been deleted weeks earlier; anything reading it would have tried to reinstate
removed code. It sat wrong because it was noticed and queued instead of fixed.

**How to apply:**

- Correct it in the same turn you notice it, before continuing the task you were on.
- Fix **every** copy: the repo `docs/memory/` file, the `MEMORY.md` index line, the
  auto-memory directory, and any duplicate wording in `CLAUDE.md` / `AGENTS.md` /
  `AI-CONTEXT.md`. A corrected file with a stale index line is still stale.
- When something is superseded rather than simply wrong, say so at the top with the date and
  the commit that changed it, and keep the original below for context — the reasoning is
  often still useful even when the conclusion is not.
- Audit adjacent claims while you are in the file. Stale entries cluster: the build-command
  fix on 2026-08-05 turned up a wrong exe path, a project that never existed, and a
  description of a helper that was never written.

Applies to code comments too — a comment claiming behavior the code no longer has is the same
failure mode.
