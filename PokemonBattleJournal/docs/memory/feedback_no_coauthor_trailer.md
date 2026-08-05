---
name: feedback_no_coauthor_trailer
description: Do NOT add a Co-Authored-By trailer to commits in this repo. User asked to stop 2026-08-05 — the model name it records is frequently wrong by the time anyone reads it.
metadata:
  type: feedback
---

**Do not append `Co-Authored-By: Claude …` to commit messages in this repo.** User instruction
2026-08-05: *"you can stop crediting yourself in the bottom its wrong a lot of the time
anyway."*

**Why:** the trailer hardcodes a model name into permanent git history. Model versions change
between and even within sessions, so the recorded attribution is often inaccurate — it claims
precision it does not have. The user owns the repo and its commit conventions.

**How to apply:** end commit messages with the body and nothing else. The same applies to PR
descriptions and any other generated git metadata. This overrides the general default of
adding attribution trailers.

Everything else about the commit style stays — see [[feedback_commit_descriptions]]: every
commit needs a real body explaining what was learned (root cause, platform quirk, non-obvious
constraint) and what the change tests or validates. Dropping the trailer is not permission to
drop the reasoning.
