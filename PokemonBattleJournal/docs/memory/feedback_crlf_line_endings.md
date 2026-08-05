---
name: feedback_crlf_line_endings
description: Write files with CRLF line endings on this (Windows) project — LF-written files spam git's "LF will be replaced by CRLF" warning on every commit
metadata:
  type: feedback
---

**Use CRLF line endings when creating or fully rewriting text files in this repo.** Every
file written with LF triggers git's `warning: LF will be replaced by CRLF` on each
add/commit that touches it (core.autocrlf on Windows). The user finds the constant
warnings annoying — stated explicitly 2026-08-05.

**How to apply:** after creating a file with the Write tool (which emits LF), convert it
(`unix2dos`, or PowerShell `-replace "\n","\r\n"`) before staging — or match the existing
file's endings when editing in place. Also recorded in the global `~/.claude/CLAUDE.md`
Best Practices (applies to all Windows projects; duplication here is intentional per
[[feedback_ai_docs_duplication_ok]]).
