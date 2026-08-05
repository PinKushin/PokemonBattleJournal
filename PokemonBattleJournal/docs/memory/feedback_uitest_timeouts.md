---
name: feedback_uitest_timeouts
description: "UI test implicit wait — 5s ambient on BOTH platforms via TestBase.AmbientImplicitWait; TimeSpan.Zero is mandatory for optional-element lookups"
metadata:
  type: feedback
---

15 seconds implicit wait for any element is a test failure, not a timeout. If an element
takes that long the test is broken.

## The ambient wait is 5s on both platforms (changed 2026-08-05)

`TestBase.AmbientImplicitWait` is the single source of truth. Every helper that changes
`ImplicitWait` restores **that constant** — never a literal.

Previously Windows `AppiumSetup` set 5s and Android set 10s, but shared `TestBase` helpers
hardcoded a 5s restore while Android's own `BaseTest` helpers restored 10s. The effective
Android ambient therefore flipped between 5s and 10s depending on which helper ran last —
nobody could say what the timeout actually was at any given line. Unified to 5s deliberately:
**everything on desktop and on the emulator renders effectively instantly**, so a lookup
still pending after 5s is a real failure, not a slow render.

Do not reintroduce a per-platform ambient. If Android ever genuinely needs longer, change
the one constant and measure, don't scatter literals.

**Exception — operational timeouts.** A wait that budgets an *action* rather than the
ambient may use its own value, but must be a named constant and must restore the ambient in
a `finally`. Only one exists: `Stage3ScrollWait` (10s) in Android `FindUIElement`, because
`scrollIntoView` may fling through a long page. That block previously had no restore at all,
which is how the 10s leaked into the rest of the session.

## Optional-element lookups MUST use TimeSpan.Zero

This is the expensive rule. Measured on Windows 2026-08-05: a lookup for an element that is
**absent** costs **~6.8s** (5s ambient + ~1.8s UIA descendant walk). The identical call for
an element that **is present** costs **~215ms**. 32x, same line of code.

Anywhere the code says "this element may or may not be there" — cleanup helpers, dismiss
paths, presence probes — the wait must be zero. Waiting cannot make an absent element
appear; you are buying nothing and paying full price on every run.

`MainPageTests.CloseWindowsPickers` was burning 13.5s of a 20.3s Game3Tab test on two
lookups for pickers that Game 3 mode deliberately removes from the tree, while the test's
actual work took 1.3s. See [[project_game3tab_ci_flake_recurring]] for the full measurement.

Use the shared helper rather than hand-rolling save/restore:

```csharp
WithImplicitWait(TimeSpan.Zero, () => { /* optional lookup */ });
```

**Bounded-budget variant:** a helper that takes a `timeoutMs` must pin that value as the
implicit wait, not leave it ambient. Otherwise a single `FindElement` overruns the budget
before the loop's deadline check ever runs — Windows `TryClickIfPresent` spent 6.7s against
a 2000ms budget for exactly this reason.

## Why it also caused flakiness, not just slowness

A doomed lookup is charged full retry time. On a slower runner 6.8s becomes 12s+, which
trips some *other* deadline (Windows `FindUIElement`'s hard 30s), and the test fails. The
long-running "Windows CI Game3Tab flake" and the local 20-second stall were one bug.

## Related

- [[feedback_cleanup_helper_timeout]] — the original 0ms-for-optional-elements rule
- [[project_game3tab_ci_flake_recurring]] — the measurement that forced this rewrite
- [[project_android_element_lookup]] — Android's three-stage lookup and its stage budgets
