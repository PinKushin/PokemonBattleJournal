---
name: feedback_no_actions_cache_in_matrix
description: Never use actions/cache inside a matrix job — it adds a post-save to every job and they contend for one key. Use cache/restore everywhere + one explicit cache/save.
metadata:
  type: feedback
---

**`actions/cache` must not be used inside a matrix job.** It registers a post-step on *every*
job in the matrix. When those jobs share a cache key — which they normally do, since the key
is derived from the same files — only one can reserve it. The others still pay the full
compression cost, then fail.

## The rule

```yaml
# In every matrix job — restore only, no post-step:
- name: Restore <thing> cache
  id: my-cache
  uses: actions/cache/restore@v6
  with: { path: ..., key: ..., restore-keys: ... }

# In exactly ONE job — explicit save:
- name: Save <thing> cache
  if: always() && matrix.<dim> == '<one-value>' && steps.my-cache.outputs.cache-hit != 'true'
  uses: actions/cache/save@v6
  with: { path: ..., key: ... }
```

- `cache-hit != 'true'` — only save when the *primary* key missed. A `restore-keys` fallback
  reports `cache-hit: false`, which is what you want: it restored something usable but the
  exact key still needs writing.
- `always()` — a failing test should not cost you the cache.
- Which job saves is **arbitrary**. With one save step there is no race. Do not reason about
  which job is "fastest" — in a parallel matrix each job carries its own fixed setup cost and
  starts whenever a runner is allocated, so finishing order is not predictable anyway.
- Place the save right after the step that populates the path, not at job end, so a later
  failure or cancellation still leaves a usable cache.

## Why it matters (measured, 2026-08-05)

Windows run `31029641442`, five fixtures sharing `nuget-windows-<hash>`:

```
Post Cache NuGet packages   173s   x5 jobs
Failed to save: Unable to reserve cache with key nuget-windows-...,
                another job may be creating this cache.
```

Four jobs compressed ~`~/.nuget/packages` and discarded the result; the fifth hung in Post
Cache long enough that the user cancelled the job by hand — after its tests had already
passed. ~11 minutes of wasted runner time per run, on a free-tier account with usage limits
(see [[user_no_signing_budget]]).

## The trap that triggers it

This stays invisible while the key is stable — a cache hit means no save is attempted at all.
It only bites when the key **rotates**, and then it bites every job at once. Watch for keys
built from `hashFiles`, which hashes files *as checked out*: the LF line-ending switch changed
every `.csproj`'s bytes and rotated the key without a single meaningful source change. A key
derived from a workflow file rotates on any edit to that workflow.

## Related

- [[project_ci_workflows]] — where this is applied, plus the per-job setup cost still open
- [[feedback_crlf_line_endings]] — the LF switch that rotated the hash and exposed this
