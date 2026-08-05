---
name: feedback_no_silent_guards
description: A guard that declines to act must log why. Silent early-returns are swallowed errors — they cost a 5.75MB logcat dig to diagnose once already.
metadata:
  type: feedback
---

**A guard that declines to perform a user-requested action must log why.** User instruction
2026-08-05: *"add logging for that because i dont want swallowed errors like this."*

This extends the existing no-silent-`catch` rule to the case that has no exception at all. An
early `return` on incomplete input is the same failure mode: the app does nothing and says
nothing, and from the outside a broken interaction is indistinguishable from an app correctly
told to do nothing.

## What it cost

Android CI run `31034371316`: `OptionsPage_SaveTag_WithName_Saves` failed and cascaded into
seven more failures. Diagnosis needed 5.75 MB of logcat, and the decisive evidence was an
**absence** — the only `Tag saved` line in the whole run belonged to a different test, and
there was no `Tag not saved` either. `SaveTagAsync` had returned at
`if (TagInput is null || _trainer is null) return;` because the text never landed in the
field after a `scrollIntoView`. One log line would have made it obvious immediately.

## How to apply

- Every `[RelayCommand]` that can decline must log before returning.
- **Split compound guards** so the message names the actual missing input. `SaveTagAsync`
  distinguishes an empty tag name from a missing active trainer; `SaveArchetypeAsync`
  distinguishes name / icon / trainer. "Cannot save" is nearly useless in a log; *which*
  input was missing is the entire diagnostic value.
- **Warning, not Error.** Declining incomplete input is expected operation. Warning is
  visible in logs and Sentry breadcrumbs without raising an error event for an empty field.
  A test pins this so it does not drift.
- Legitimately-not-an-error paths (a cancelled file picker, `Shell.Current is null` in the
  unit-test environment, selecting the already-active trainer) do not need a warning — they
  are not the user asking for something that then failed to happen.

## Testing it

Use `RecordingLogger<T>` (`PokemonBattleJournal.Tests/Fixtures/RecordingLogger.cs`), not an
NSubstitute mock. `ILogger.Log` is generic over `TState` and the logging extension methods
pass an internal framework type, so `Arg.Is<object>(…)` never matches and `Arg.AnyType` can
only prove *something* was logged, not what it said. Message content is exactly what matters
here. See `OptionsPageViewModelGuardLoggingTests`.

## Still to do — surface it in the UI

Logging fixes diagnosis, not the user experience: a user who mistypes still sees nothing
happen. User wants validation feedback shown in-app (2026-08-05) — *"probably display a modal
or better just a text label with red text explaining the verification step failed"*, with a
label preferred over a modal. Deferred; tracked in [[project_roadmap]].

## Related

- [[feedback_engineering_principles]] — the no-silent-`catch` rule this extends
- [[project_options_vm_bugs_fixed]] — `NewDeckIcon` pre-initialised so its guard would not
  fire silently; the same class of problem, patched at one site instead of systematically
