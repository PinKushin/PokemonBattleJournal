---
name: project_codeql_setup
description: "CodeQL works as of 2026-08-07 and had never worked before that. Default setup silently blocks a workflow's SARIF upload; only one config can exist. 216 alerts triaged down to 7 — read this before touching codeql.yml or re-opening dismissed alerts."
metadata:
  type: project
---

## The failure that hid for a day

`codeql.yml` was added 2026-08-07 and **failed on every run until it was fixed the
same day**. The analysis was always fine — build succeeded, all 171 queries evaluated —
and then the upload was rejected:

```
Code Scanning could not process the submitted SARIF file:
CodeQL analyses from advanced configurations cannot be processed when the default setup is enabled
```

**Default setup and an advanced (workflow) configuration are mutually exclusive.** The user
had enabled default setup in the repo settings separately. Default setup is now
`not-configured` and `codeql.yml` is the single configuration. If CodeQL ever starts failing
at the upload step again, check that first:

```bash
gh api repos/{owner}/{repo}/code-scanning/default-setup --jq '.state'
```

Nobody read the red X for a day. See [[feedback_check_ci_annotations_and_artifacts]].

## Two languages, and why `actions` nearly got left out

The matrix analyses `csharp` (windows-latest, explicit build, ~10 min) and `actions`
(ubuntu-latest, no build, ~1 min).

`actions` was **missing from the first version**, because the language list was chosen by
reasoning about the *app's* untrusted inputs — TrainerHill JSON and the Limitless scrape,
both C#. That is the wrong model for a repo whose workflows are themselves code running on a
public repo with `pull_request` triggers. Every alert the repo had open at that point was in
the part that reasoning excluded. Default setup caught them only because it enumerates
languages rather than reasoning about them.

**A real build is what makes the C# leg worth anything.** Default setup's buildless extraction
reported **0 results**; the same code with `dotnet build` reported 213.

## Triage outcome: 216 → 7

| Disposition | Count | How |
|---|---|---|
| `cs/catch-of-all-exceptions` | 116 | **Excluded by rule id** in `codeql-config.yml` |
| Generated code | 82 | **Dismissed per alert**, "won't fix" |
| `cs/path-combine` | 6 | Dismissed, "false positive" |
| Fixed in code | 4 | 3 unpinned tags, 1 dead branch |
| `cs/constant-condition` (DB) | 1 | Dismissed, "false positive" |
| Genuinely open | 7 | 6 style notes + 1 fixed pending rescan |

### Exclude a rule vs dismiss the alerts — the rule of thumb

**Exclude by rule id only when it fires on a pattern this repo uses on purpose.** That is
true of `cs/catch-of-all-exceptions`: broad catch *is* the documented error policy here
([[project_error_handler_di]]), and the shape actually banned — the silent `catch { }` — is a
**different rule**, `cs/empty-catch-block`, left enabled. Verified before excluding that the
app and scraper contain zero empty catch blocks.

**Dismiss per alert for generated code.** All 48 `cs/useless-assignment-to-local` alerts were
in `*.g.cs` and **none** in hand-written code, so excluding the rule would have switched off
dead-store detection across the whole app to silence code nobody here wrote. Note the rule is
not about memory: a dead store is a value assigned and never read, which usually means the
wrong variable was assigned or a result was computed and forgotten.

`paths-ignore` does **not** exclude generated code for compiled languages — confirmed, 152
generated alerts passed straight through it.

## Do not re-open these dismissals

83 alerts are dismissed with written reasons. Two are load-bearing:

- **`SqliteConnectionFactory` `cs/constant-condition`** — double-checked locking. The type is
  a DI singleton, so a caller can pass the outer null check, win the semaphore, and finish
  table creation while another is suspended on `WaitAsync`. CodeQL reasons from the early
  return and does not model the concurrent write across the `await`. Deleting that check would
  build a second connection and re-run every `CreateTableAsync`. The reasoning is also a
  comment at the site.
- **The 6 `cs/path-combine`** — every second argument is a string literal or compile-time
  constant combined with a platform directory. A literal cannot be rooted, so the earlier
  argument can never be discarded. No user input reaches these paths.

## Dismissals are location-anchored — a file MOVE resurrects them

Confirmed 2026-08-07 by the Core extraction. Moving 46 files re-opened two alerts that had
been dismissed with written reasons, because a dismissal is bound to the alert instance and a
new path is a new instance:

- `cs/constant-condition` — the double-checked lock, back at
  `PokemonBattleJournal.Core/Services/SqliteConnectionFactory.cs`
- `cs/path-combine` — the same benign literal-second-argument pattern, in the newly created
  `MauiSqliteConnectionFactory`

Both were re-dismissed with the same reasoning. **After any refactor that moves files, check
the open alert list rather than assuming the dismissals held.** This is the same discipline as
[[feedback_check_ci_annotations_and_artifacts]]: verify the run after the change, not the run
before it. The check is cheap:

```bash
gh api --paginate "repos/{owner}/{repo}/code-scanning/alerts?state=open&per_page=100"   --jq '.[] | "\(.rule.id)  \(.most_recent_instance.location.path)"'
```

It is also why the reasoning for a load-bearing dismissal lives in a **code comment at the
site** and not only in the GitHub UI — the comment moves with the file, the dismissal does not.

## Actions are pinned, and Dependabot exists because of it

Third-party actions pin commit SHAs with the version in a trailing comment
(`actions/unpinned-tag`). A tag is a moveable pointer, so `@v2` means whatever that repo's
owner points it at on the morning CI next runs. `.github/dependabot.yml` covers
`github-actions` **only** — a pin that never moves never gets the security fix either.

**NuGet is deliberately excluded from Dependabot**: the SQLitePCLRaw overrides against
GHSA-2m69-gcr7-jv3q look like removable duplicates to a tool that has not read the reasoning,
and removing them reintroduces 14 instances of NU1903. See [[project_sqlite_security_pins]].
