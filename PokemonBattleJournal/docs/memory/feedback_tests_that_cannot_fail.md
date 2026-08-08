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

## The framing that organises all of this — the user, 2026-08-07

*"think about tests as scientific experiments if it helps, the measurements need to be measuring
the right variable."*

Map it and the shapes below stop being a list to memorise:

| Experiment | Test |
|---|---|
| Manipulation of the independent variable | the mutation / the sabotage |
| Measurement | the assertion |
| Experimental condition | the input |
| Control group | a second subject that must be unaffected |

**A test that cannot fail is an experiment whose measurement is insensitive to the
manipulation.** There are three distinct ways to get there, and they need different fixes:

1. **Wrong instrument** — measuring a proxy that is not faithful to the variable. The Sentry
   leak test searched serialized JSON BYTES when the variable was CONTENT; escaping decoupled
   them. Fix the measurement: parse and walk the document.
2. **Wrong condition** — an input for which both hypotheses predict the SAME observation.
   `SuffixStrippingIgnoresCase` used `EX` and `tera`, which the regex alternation lists
   explicitly, so flag-present and flag-absent agree. `Compute_OnlyOneSideOverTheBound` shared
   no lines, so a real diff and the whole-block fallback agree. Fix the INPUT — `gx`, a shared
   line.
3. **No control** — one subject, so "affected everything" and "affected the target" are
   indistinguishable. The trainer deletion cascade with a single trainer. Fix by adding a
   bystander that must survive.
4. **Effect size below resolution** — the condition is too small for the difference to show.
   NoteDiff's ten survivors were this: every case was two or three lines, where a broken LCS
   table and a correct one produce identical output. The manipulation was real and the assertion
   was fine; the condition could not resolve it. Fix by ENLARGING the input — eight interleaved
   lines, a block between a matching head and tail, repeated lines.

**Predict exact values.** `ShouldBe("raging_bolt.png")` is a prediction; `ShouldNotBeNullOrEmpty()`
reports that *an* effect occurred while staying blind to magnitude and direction. The one test
in this repo still known not to discriminate — the ImagePath2 backfill case — uses exactly that
weak form, and it may be concealing a real defect: when URL resolution fails,
`TryResolveLocalSprite` falls back to the deck NAME, which would give a dual-icon deck the same
sprite twice. Unverified as of writing; an exact assertion would settle it.

**UI tests are the one place probability is legitimate, and only in measurement ACQUISITION.**
The app is deterministic and so are the values; only WHEN a measurement can be taken varies.
So synchronise on the condition, never the clock, and never retry a failure into a pass — see
[[feedback_no_sleeps_in_tests]] and [[project_ci_retry_on_flake]], which reached the same
conclusion from the other direction when six "flaky" Android failures turned out to be six real
bugs.

**The correction worth keeping: the instinct is to strengthen the assertion, and twice out of
three that was the wrong move.** A stronger measurement cannot rescue a condition where both
hypotheses agree. Ask first "is there an input where correct and broken differ?", and only then
"does my assertion detect that difference?"

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

## Three more shapes, found by mutation-testing Core on 2026-08-07

The first four came from the Scraper. These came from the app's own logic and are the ones that
recur here.

5. **A branch exercised in ONE DIRECTION only.** This was the single biggest source of
   NoCoverage in Core, and every instance had a passing test on top of it.
   - `RestoreBackupAsync`'s guards: the CONDITIONS were killed because every test evaluates
     them, while the BODIES were untouched because every test passes a valid file. Covered as
     false, never as true.
   - `TrainerOperations.DeleteAsync`: `DeleteAsync_AfterSave_RemovesTrainer` deletes a trainer
     with NO matches, archetypes or tags, so all three `foreach` bodies are skipped and
     `if (match.Game1Id.HasValue)` is never evaluated. 85 uncovered mutants behind a green test.
   - `GetByTrainerIdAsync`: only ever asked to INCLUDE, never to exclude.

   **Ask of every branch: is there a test that takes the other leg?**

6. **Single-subject blindness.** With one trainer in the database, "delete everything" and
   "delete this trainer's data" produce identical results. Removing
   `.Where(m => m.TrainerId == trainer.Id)` — so `DeleteAsync` wipes EVERY trainer's matches,
   games and tag links — failed exactly one test and left five green, including the one named
   "removes the trainer's matches". A data-loss bug no amount of care writing more
   single-subject tests would find. **Any filter needs a second subject that must survive.**

7. **A return value nobody reads.** `TagOperations.DeleteAsync` had
   `affected += relationshipsDeleted` survive mutation to a subtraction — the number it returns
   could have meant anything.

## Your OWN new tests are not exempt

Three tests written during that same session failed their sabotage check:

- A leak test searched serialized JSON bytes for `Ash's Pikachu Deck`; `Utf8JsonWriter` escapes
  an apostrophe to `'`, so the value was fully present in the payload and absent as a
  literal substring. It passed on the exact leak it was written to catch.
- `SuffixStrippingIgnoresCase` used inputs (`EX`, `tera`) that the regex alternation already
  lists explicitly, so they strip with or without the flag. Deleting `RegexOptions.IgnoreCase`
  left it green.
- A backfill test still does not discriminate and is documented as such rather than counted.

**Writing the test is not the verification step. Breaking the code is.**

## Two things that are NOT holes — do not chase them

- **Equivalent mutants from redundant code.** `TagOperations` deletes TagGame rows, then a
  verification block detects leftovers and deletes them again. Neutering either is masked by the
  other. A test that distinguished them would be testing the implementation.
- **Change-detector tests.** Nine string mutants sit in a hardcoded offline deck list. Pinning
  every name would kill them and make the suite fail on any edit to the list. The two properties
  that matter — the catch-all exists, the dual-icon default survives — are asserted instead.

Killing a mutant is not the goal. Noticing a real change is.

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
