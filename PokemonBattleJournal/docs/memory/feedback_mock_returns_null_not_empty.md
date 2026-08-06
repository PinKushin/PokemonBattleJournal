---
name: feedback_mock_returns_null_not_empty
description: "An unstubbed NSubstitute call returns null where the real operations layer returns an empty list. Stub the fixture; never add null-tolerance to production code for it. Hit three times in one day."
metadata:
  type: feedback
---

**Symptom:** a `NullReferenceException` deep inside a service, or — worse — a row that is
simply *missing* from a result with no error at all.

**Cause:** `Substitute.For<ISomeOperations>()` returns `null` for any method you did not stub.
Every operations class in this project returns an empty list instead: `ArchetypeOperations
.GetAllAsync` returns `[]` from its own catch, `MatchOperations.GetByTrainerIdAsync` returns a
real list. So the mock models a state the production code can never be in.

**Fix the fixture, not the service.** Adding a `?? []` or a null check to production code to
satisfy a mock bakes a lie into the app: it makes the code defend against something the ops
layer does not do, and hides the fact that the test was under-specified.

```csharp
_matches.GetByTrainerIdAsync(Arg.Any<uint>(), Arg.Any<bool>()).Returns([]);
_archetypes.GetAllAsync().Returns([]);
meta.GetTopDecksAsync(Arg.Any<int>()).Returns([]);
```

## Why this earns its own note: it hit three times in one day (2026-08-06)

Each time a service gained a new dependency, and each time the failure looked like something
else:

1. **`ExportService` started reading `Archetypes.GetAllAsync`.** Three export unit tests threw
   `NullReferenceException` from inside the service — looked like an export bug.
2. **`ExportServiceIntegrationTests` left `ILimitlessMetaService` unstubbed.** This is the nasty
   one: `ArchetypeOperations.GetAllAsync` faults on the null deck list, catches its own
   exception and returns `[]`. **No exception reaches the test.** The archetype was simply
   absent from the export, so the failure read as "the export does not write archetypes" — the
   exact feature under test. Recorded separately in
   [[project_integration_test_isolation]].
3. **`TrainerHillImportService` started indexing existing matches for dedupe.** Two limits
   tests threw from `MatchDuplicateKey.Index`, nowhere near the limits being tested.

## How to apply

- Adding a dependency to a service? Grep for its fixtures and stub the new call **in the same
  change**. The compiler will not tell you, and the failure will not point at the fixture.
- A test failing inside a service you did not touch, right after adding a dependency, is this
  until proven otherwise.
- Beware the silent variant. When the swallowed null happens *behind* another service that
  catches its own errors, there is no stack trace at all — just a missing element. If an
  assertion fails on data being absent and the code looks right, check what the fixture did
  not stub before doubting the code.

## Related

- [[project_integration_test_isolation]] — the `ArchetypeOperations` + metaService case
- [[feedback_no_silent_guards]] — the same disease in production: a catch that returns a
  plausible empty value hides the reason it was reached
