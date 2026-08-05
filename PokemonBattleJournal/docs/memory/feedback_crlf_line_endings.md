---
name: feedback_crlf_line_endings
description: Line endings follow .gitattributes — this repo is switching to LF everywhere (chore/lf-line-endings). Do NOT unix2dos files here once that lands.
metadata:
  type: feedback
---

**`.gitattributes` is the authority. Read it before writing files.** The goal is that git
never has to normalize anything, so it never prints `warning: LF will be replaced by CRLF`
(or the reverse). The user finds those warnings annoying — stated explicitly 2026-08-05.

## Status of this repo (2026-08-05)

The user approved switching this repo to **LF everywhere**, on its own branch
`chore/lf-line-endings`, to be done after the in-flight Android verification run.

- **Before that branch lands:** `.gitattributes` declares `* text=auto eol=crlf` plus a
  per-extension CRLF block. Every tool here writes LF by default, so a `unix2dos` pass is
  required after `Write` or `sed -i` before staging.
- **After it lands:** `.gitattributes` declares `* text=auto eol=lf`. **Stop converting.**
  Running `unix2dos` at that point is what *creates* the warning. Just write the file.

Check the file rather than trusting this note's timing — `git check-attr text eol -- <file>`
answers it definitively for any path.

## Why LF was chosen

LF is git's native storage format (the repo already stored LF regardless), matches the
Ubuntu runners the Android CI uses, and matches what every file-writing tool emits by
default — so it removes the conversion step entirely rather than automating it. Nothing in
this repo requires CRLF: the only script is `Save-CoverageResults.ps1`, and PowerShell reads
LF fine. There are no `.bat`/`.cmd` files, which are the only files that genuinely break
with LF (`.gitattributes` keeps a guard entry for them anyway).

Cost accepted: one repo-wide renormalization commit, recorded in `.git-blame-ignore-revs`
so `git blame` skips it (GitHub honors that file; `blame.ignoreRevsFile` is set locally).

## Correctness overrides, both directions

`.bat` and `.cmd` require CRLF to execute. `.sh` requires LF to run on Linux. These are not
style preferences and they outrank any repo-wide default.

Also recorded in the global `~/.claude/CLAUDE.md` Best Practices — duplication is
intentional per [[feedback_ai_docs_duplication_ok]].
