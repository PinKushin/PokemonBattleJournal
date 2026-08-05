---
name: feedback_test_the_hypothesis_first
description: Before implementing a fix, make the suspected cause observable. Otherwise a still-failing run cannot distinguish "wrong theory" from "right theory, wrong fix".
metadata:
  type: feedback
---

**Confirm the hypothesis before writing the fix.** User, 2026-08-05: *"testing for our
hypothesis before hand might help sometimes … that way we know if the problem is the
hypothesis is wrong or the way we are doing it is wrong."*

Those two failure modes look identical from the outside — the test is still red — but they
need opposite responses. Without separating them, a failed fix sends you off improving an
implementation of a theory that was never correct.

## This is a refinement of the TDD rule, not a contradiction

The project's TDD rule ("write the failing test first") is about **new behaviour**. For a
*diagnosis*, the equivalent step is making the suspected cause observable before changing
anything. User was explicit that a bug already covered by a failing test does not need
another one: *"we already have the failing tests, we dont need more failing tests, unless
theres something specific we should test for because it is the problem — but we dont know
that before fixing the problem."*

So: do not manufacture ceremonial failing tests for a bug that is already red. Do add the
one observation that discriminates between candidate causes.

## Worked example — the ClickTab cascade (2026-08-05)

Symptom: `MainPage_Game3Tab_ShowsGamePanel` failed on CI after 38s, cascading into five more
failures. Two candidate causes, indistinguishable from the failure message alone:

1. The Game 2 panel opened, but the element was unfindable (a lookup problem).
2. The panel never opened at all (a click problem).

What discriminated them was **instrumentation, not an assertion** — a direct FlaUI/UIA query
answering `not found in 26ms`. Fast and authoritative: the element genuinely was not in the
tree, rather than being slow to locate. That eliminated cause 1 and made the click the
suspect, confirmed by the click round-trip logging at 1231ms/1790ms on CI versus under 750ms
locally. Only then was the fix (click-verify-retry) worth writing.

**The cheaper version worth reaching for next time:** add a bare assertion immediately after
the suspect action — here, "did the panel appear?" with no retry — and let one CI cycle
confirm the theory before any retry logic is written. Same confirmation, less code, and the
failure message names the real problem instead of surfacing as a lookup timeout 38 seconds
later.

## How to apply

- Ask what *else* could produce this symptom, then find the one cheap observation that rules
  the alternatives out.
- Prefer an independent measurement path over the one already failing — FlaUI's direct UIA
  query was trustworthy precisely because it did not share WinAppDriver's machinery.
- Timing is evidence. "1231ms here, under 750ms there" localised this bug faster than any
  stack trace.
- Say plainly when a failing-test-first is not possible. The ClickTab fix could not be
  TDD'd because the trigger is a slow CI runner that does not reproduce locally; the existing
  suite was the regression net and CI was the verification. Claiming otherwise would be
  theatre.

## Related

- [[project_game3tab_ci_flake_recurring]] — the instrumentation that made this diagnosis possible
- [[feedback_dont_churn_stable_ci]] — evidence before changing CI/test infrastructure
- [[feedback_android_flaky_tap_retry]] — the click-verify-retry pattern this confirmed for Windows
