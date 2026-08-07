---
name: feedback_tests_that_cannot_fail
description: "Test suites decay into assertions that cannot fail — the user's stated reason for bringing in Stryker. A green suite is not evidence; an assertion that has never been red proves nothing. Verify tests bite, by sabotage or by mutation."
metadata:
  type: feedback
---

**The user's position, stated 2026-08-07:** *"exactly why i wanted it brought in i knew we had
tests that just couldnt fail, it happens over time."*

Confirmed immediately. Stryker's first run on the Scraper surfaced 14 mutants no test detected,
and **none of them was untested code** — every one had tests that passed. They were tests that
could not fail.

## The four shapes it found

Recognise these; they are how a suite rots without anyone doing anything wrong.

1. **Seed data that never reaches the branch.** Every parser test row had exactly one image, so
   `imgs.Length > 1` was never true — the dual-icon path, a shipped feature, had no coverage
   while looking fully covered.
2. **A branch that is a no-op for the data used.** The annotation branch rebuilds
   "base + annotation", which for tidy markup reproduces the anchor's own text. Deleting it
   changed nothing. It only does work on irregular whitespace — which is what the real scraped
   page contains, and what no fixture had.
3. **Asserting the type instead of the behaviour.** A factory test checked `Create()` returned
   the right type. That passes with every field null, because construction never dereferences
   them. Deleting the whole constructor body survived.
4. **`NullLogger` where the log IS the contract.** Eight mutants across sites that swallow a
   failure and return empty. The log is the only evidence the caller gets — see
   [[feedback_no_silent_guards]] — and no test looked at it.

## How to act on it

- **A green suite is not evidence that a change is safe.** It is evidence the tests ran.
- **Prove an assertion bites before trusting it.** Break the thing on purpose, watch it go red,
  put it back. Done repeatedly on 2026-08-06/07 — the win-rate invariant, the BO1 hidden-editor
  test, the accessibility contract, the note-binding test. Each one was pointed at the defect it
  claims to catch and confirmed to fail.
- **`dotnet stryker` is that, exhaustively.** Run it after adding tests to a mutable project, and
  read the survivors rather than the score.
- **Coverage and mutation score answer different questions.** Coverage says a line ran. Mutation
  says something would notice if it changed. All 14 survivors sat in covered code.

## The blind spot

Stryker cannot mutate the MAUI app project ([[project_stryker_mutation_testing]]), so this decay
is currently *invisible* in ViewModels and Services — the code with the most branching logic in
the repo. That is the strongest argument for extracting pure logic into a plain library, above
any design-tidiness reason.

Until then, the manual version is the only check there: when writing a test for app code, ask
what change it would fail on, and if the answer is not obvious, make the change and find out.

## Related

- [[feedback_test_the_hypothesis_first]] — same idea applied to diagnosis
- [[project_stryker_mutation_testing]] — the tool, its result, and its limits
- [[project_accessibility_contract_tests]] — verified to discriminate against a `Label`
