---
name: project_game3tab_ci_flake_recurring
description: "Game3Tab RESOLVED — TWO separate fixes, and the FlaUI one is the bigger half. (1) FlaUI UIA3 direct-tree lookup (a475e33) replaced the flaky WinAppDriver query. (2) Optional lookups inheriting the 5s ambient ImplicitWait cost ~6.8s each; fixed with TimeSpan.Zero."
metadata:
  type: project
---

**Status: RESOLVED 2026-08-05.** Root-caused by measurement, fixed, and verified green on
both platforms.

## Read this first: there were TWO fixes, not one

**Corrected 2026-08-08.** This entry used to describe only the ImplicitWait half, which made it
read as though timeouts were the whole story. They were not, and the omission actively misled a
later session into crediting the picker's keyboard-nav rewrite for Game3Tab's speed.

| Commit | Change | What it fixed |
|---|---|---|
| `a475e33` | FlaUI UIA3 **direct-tree** fallback | the flaky Windows Game3Tab *lookup* itself |
| `bab8053` | real invokable controls, walk up for a pattern | zero-coordinate clicks |
| (this entry) | `TimeSpan.Zero` on optional lookups | ~6.8s per ABSENT element |

`df081b9` (keyboard nav: click, first letter, Tab) is the picker **selection** path and has
nothing to do with the Game3Tab lookup — see [[project_windows_picker_ci]]. Do not conflate
them.

**The general lesson, which outlived the specific bug:** every Windows UI fix that has stuck
went *toward* FlaUI/UIA and away from WinAppDriver. The reverse direction produced the
2026-08-08 CI failure, where `App.FindElements` threw `The specified element ID is either null
or the empty string` from inside Appium's response parser — a malformed driver response with
nothing wrong in the app. WinAppDriver is unmaintained (last release 2023). Presence checks now
route through FlaUI via `IsElementPresentCore`.

## Result after the fix

| Test | Before | After |
|------|--------|-------|
| `Game3Tab_ShowsGamePanel` | 20438ms | **7498ms** |
| `Game3Tab_ShowsWhenGame1IsTie` | 12734ms | **6249ms** |
| `DualIconDeck_ShowsBothIcons` | 19795ms | **11331ms** |
| `CloseWindowsPickers` (absent) | 6798ms | **186ms** |

Full Windows UI suite (73 tests, all 5 fixtures) now runs in **1m28s** — previously
`MainPageTests` alone took 1m20s. Verified: unit 488/488, integration 115/115, Windows UI
73/73, Android UI 72/72 in 8m55s (matching its 8m44s pre-change baseline, so the 10s→5s
ambient change caused no flakiness).

**The fix:** optional-element lookups run at `TimeSpan.Zero` via the new
`TestBase.WithImplicitWait` helper (`CloseWindowsPickers`, `ScrollPageToTop`,
`ClearUserNoteInput`); Windows `TryClickIfPresent` pins its own `timeoutMs` as the implicit
wait as Android's override always did; the ambient is now the single
`TestBase.AmbientImplicitWait` constant (5s, both platforms). See [[feedback_uitest_timeouts]].

## Still open, same family — Android-side lookup cost

Not addressed by this fix (`CloseWindowsPickers` early-returns on non-Windows, so Android's
Game3 timings are unchanged at ~20-24s). From the pre-change CI log, several *single-assert*
Android tests land at a uniform ~10.0-10.3s — `WentFirstLabel_Displayed`,
`ResultPicker_Displayed`, `FirstCheck_Displayed`. That is the three-stage lookup shape:
stage 1 exhausting its 5s deadline, stage 2 ~0.5s, then stage 3 scrolling. These are
below-the-fold elements that should cost ~200ms. Biggest remaining Android win; wants a
scroll-first strategy or a cheaper stage 1.

Also open (Windows, small): `DismissArchetypePopup` retries a Cancel click that already
succeeded — 5.6s of `DualIconDeck`'s remaining 11.3s. A zero-wait presence check before the
click would recover most of it, but it changes dismissal behavior, so it wants its own pass.

---

## Original diagnosis (kept — this is the evidence trail)

Diagnosis below is from local instrumented runs, not inference.

## The measurement

Every lookup for an element that is **absent** costs ~6.8 seconds:
**5000ms ambient `ImplicitWait` + ~1.8s UIA descendant walk.** The same call against an
element that **exists** costs ~215ms. 32x, same line of code.

Proof (local Windows run, both tests PASSED — this is latency, not failure):

```
CloseWindowsPickers('PossibleResultsPicker'):  absent, cost 6798ms
CloseWindowsPickers('PossibleResultsPicker2'): absent, cost 6775ms
END MainPage_Game3Tab_ShowsGamePanel [Passed] 20260ms

CloseWindowsPickers('PossibleResultsPicker'):  absent, cost 6806ms
CloseWindowsPickers('PossibleResultsPicker2'): escaped in 215ms
END MainPage_Game3Tab_ShowsWhenGame1IsTie [Passed] 12905ms
```

13.57s of ShowsGamePanel's 20.26s — 67% — is two lookups for pickers that are deliberately
not in the tree. The actual work (ClickTab Game3 + all five Game-3 panel asserts) took 1.3s.

Cost is stable across machines: 6745/6766ms local DualIcon, 6798/6775/6806ms local Game3,
7207ms on GitHub CI. **Reproduces locally — this was never CI-specific.**

## Why ShowsGamePanel is slower than ShowsWhenGame1IsTie

Purely the number of doomed lookups in the `finally` block. ShowsGamePanel ends with Game 3
selected, so BOTH `PossibleResultsPicker` and `PossibleResultsPicker2` are `IsVisible=false`
(two misses). ShowsWhenGame1IsTie ends with Game 2 selected, so picker2 is still present
(one miss, one 215ms hit). 20.3s vs 12.9s.

## Where the pattern lives (all the historically flaky spots)

- `CloseWindowsPickers` (`MainPageTests.cs`) — raw `App.FindElement` per id at ambient 5s.
  The Game3 tests guarantee both misses on every single run. Biggest offender.
- `TryClickIfPresent` (Windows `BaseTest.cs`) — sets no wait of its own, inherits 5s. Note
  `DismissArchetypePopup` sets `ImplicitWait = Zero` on the line *after* it calls this, so
  the protection misses the expensive call. ~6.7s each time Cancel is already gone.
- `ScrollPageToTop`, `ClearUserNoteInput`, `ResetBOSwitch` — same shape; cheap today only
  because their targets usually exist.
- `WaitUntilText` — sets 200ms per iteration, but `EnsureBO3On` calls it with a 3000ms
  budget; one ambient-rate miss would blow the whole budget. Latent, not currently firing.

**The connection to flakiness:** a doomed lookup is charged full retry time. When a runner
is slow enough that 6.8s becomes 12s+, some *other* deadline (Windows `FindUIElement`'s hard
30s) trips and the test fails. Same root cause as the "recurring Windows CI flake" this file
originally documented — the flake and the slowness are one bug, not two.

## Theories ruled out by measurement

- NOT WinAppDriver element-tree caching (the original a475e33 assumption).
- NOT the picker interaction — `SelectWindowsPickerItem` is ~330ms total
  (`click 225ms, letter 33ms, tab 58ms, re-anchor 19ms`).
- NOT `ClickTab` or the panel asserts — none exceeded a 750ms log threshold.
- NOT `FindUIElement`'s poll loop — zero `UIA check` lines appear during the stall windows,
  meaning fewer than 4 iterations ran. Corollary: **the FlaUI fallback from a475e33 is
  near-dead code**, since it only fires on iteration 4.

## The fix (designed, not applied)

Optional-element lookups must run at `ImplicitWait = Zero` — the rule already written in
[[feedback_cleanup_helper_timeout]], just not followed by these helpers. Applies to
`CloseWindowsPickers`, Windows `TryClickIfPresent`, `ScrollPageToTop`, `ClearUserNoteInput`.
Expected saving: ~13.5s on ShowsGamePanel, ~6.8s on ShowsWhenGame1IsTie, ~13.5s on
DualIconDeck, per fixture run, on both platforms.

## Separate latent bug found while reading

Shared `TestBase` hardcodes its `ImplicitWait` restore to **5s** (the Windows value) in
`WaitUntilText`, `IsElementPresent`, `WaitUntilRemoved`, `WaitUntilGone`. Android's
`AppiumSetup` sets 10s and Android's own `BaseTest` helpers restore 10s — so on Android the
ambient silently flips between 5s and 10s depending on which helper ran last. Makes Android
faster, so it has never bitten, but the documented "10s Android ambient"
([[feedback_uitest_timeouts]]) is not reliably in effect.

## Related

- [[feedback_cleanup_helper_timeout]] — the 0ms-for-optional-elements rule these helpers break
- [[feedback_bo3_state_idempotent]] — EnsureBO3On, confirmed fast (350-490ms), not implicated
- [[project_game3tab_test_bug]] — DIFFERENT, already-resolved Android content-desc issue
- [[project_ci_workflows]] — console mirroring that first surfaced the polling detail
