---
name: project_dotnet_test_filter_exits_zero
description: "`dotnet test --filter` matching NOTHING exits 0 with no summary line. Every CI job that filters by fixture name would go green while testing nothing if the fixture were renamed."
metadata:
  type: project
---

**A filter that matches no tests is not an error.** Verified 2026-08-06:

```
dotnet test PokemonBattleJournal.Tests/... --filter "FullyQualifiedName~NoSuchTestZZZ"
```

exits **0** and prints no `Failed: n, Passed: n` summary at all. Not a warning, not a
non-zero code — indistinguishable from success to anything reading the exit code.

## Why this matters here specifically

Both UI workflows are per-fixture matrices, and every job selects its tests by **name**:

```yaml
--filter "FullyQualifiedName~${{ matrix.fixture }}"
```

So if a fixture is renamed, moved, or dropped and the `fixture:` list in the workflow is not
updated in the same commit, **that job goes green having run zero tests.** The suite silently
stops being covered, and the signal that would tell you is the absence of output nobody reads
on a passing job. The same applies to the `Category!=LiveWeb` filter in CI if that category is
ever renamed.

This is latent, not currently firing — the fixture lists match today. It is recorded because
the failure is invisible by construction: a rename is exactly the kind of change that looks
safe and gets no review attention.

## What already defends against it

`build/ci-local.ps1` requires three things before calling a suite green: a zero exit code, a
parsed summary line, **and** a non-zero test total. It names which of the three was missed.
That was written after hitting this — the first version trusted the exit code alone and would
have reported PASS while testing nothing.

**The CI workflows have no such check.** Adding one — assert a minimum test count per job, or
diff the discovered fixture list against the matrix — is unbuilt work worth doing.

## The general shape

A tool that reports "nothing to do" the same way it reports "everything passed" turns a
missing-work bug into a green build. Whenever a build step selects work by a string that lives
somewhere else, the check that matters is *did it find anything*, not *did it fail*.

## Related

- [[feedback_no_silent_guards]] — same principle, applied to app code
- [[project_ci_workflows]] — the matrix definitions this would bite
