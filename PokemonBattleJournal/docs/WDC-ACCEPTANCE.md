# WindowsDriverCore — acceptance spec, from PokemonBattleJournal

What PBJ requires of WindowsDriverCore before its Windows UI suite is switched over.

PBJ is the dogfood consumer, so this is written from the consumer's side: every criterion below is
a **defect PBJ currently works around**, with the evidence it was real and the test that proves it
is gone. Nothing here is speculative — each one cost debugging time and has a scar in this repo.

**How to use it.** Each criterion has a *defect*, an *acceptance test*, and *what gets deleted*
when it passes. The deletions are the point: if a workaround survives the swap, nobody learns
whether WDC fixed the thing it exists for.

---

## A1 — Pattern activation is the default path; the mouse is a bounded fallback

**The defect.** WinAppDriver's `.Click()` is synthesized mouse input at the element's centre in
**screen** coordinates, carrying no UIA pattern. A target laid out below the window is clicked at
whatever screen position that resolves to. Locally that launched Visual Studio and the Epic Games
store off the taskbar; on CI it landed on empty desktop, returned in ~1s having done nothing, and
left find-only tests passing. **Six tests failing on CI for two days.**

**Revised twice on 2026-08-11, and the second revision is the important one.**

First pass: a click has to be real mouse input — WinAppDriver's own suite tests for that and Appium
compatibility requires it — so "do not use coordinates" was never achievable, and the question
became how the mouse path is bounded.

Second pass, from WDC: **"click should almost never be needed. We can select and click an element
without the mouse, and that is the default path."** That changes the shape of this criterion
entirely. WinAppDriver conflated *activate* with *move the mouse and press*; if WDC separates them
and activates through UIA patterns by default, then PBJ's ladder is not compensating for anything —
it is doing the driver's job a second time.

**Two cases, wanting opposite outcomes. Test both.**

| Case | Required behaviour |
|---|---|
| Element exposes a pattern (Invoke, Toggle, SelectionItem, ExpandCollapse) | Activates **regardless of position** — no scroll, no mouse, no error, even when laid out below the window. Patterns carry no coordinates, so window bounds are irrelevant. |
| Element exposes no pattern, so the mouse fallback engages | Bounds apply. Must **refuse with an error naming the element and the bounds** — never clamp. |

**Clamping is worse than the original bug and is the thing to check first.** The old failure clicked
empty desktop and did nothing: inert and silent. A clamped coordinate activates a DIFFERENT element
and the test proceeds against the wrong state: wrong and silent. Assert a bystander did not change
state, because that is the only observation that separates a clamp from a refusal.

**Acceptance test.** At CI geometry (`754x512` at `85,78`):

1. Activate a `Button` laid out below the window bottom, **without scrolling it into view**. It must
   succeed via its pattern, and the window must not scroll as a side effect.
2. Activate a pattern-less element below the window bottom. It must throw, name the element and the
   bounds, and leave every other element untouched.

```powershell
UITEST_WINDOW_SIZE=754x512 UITEST_WINDOW_POS=85,78 dotnet test PokemonBattleJournal.UITests/UITests.Windows/UITests.Windows.csproj
```

Both env vars, not just the size. Size alone pins the window to `(0,0)`, where screen space and
window-relative space coincide — exactly how this bug hid locally while failing on CI.

**Deleted on pass — and this is now most of the file.** If case 1 holds, `TestBase.ClickElement`
loses its whole reason to exist: the ScrollItem step (only ever there to get the target under the
mouse), the Invoke/Toggle/SelectionItem/ExpandCollapse ladder (the driver's job now), the Edit/Document
focus special case, and the containment guard. What remains is a thin call into the driver.

**The one thing to confirm before deleting.** PBJ's ladder covers a specific set —
ScrollItem, Invoke, Toggle, SelectionItem, ExpandCollapse, `Focus()` for `Edit`/`Document`, then
three levels of ancestor. Whatever WDC's default path does NOT cover has to stay behind. The
`Focus()` case is the likeliest gap: an `Edit` exposes no activation pattern because there is no
action to activate, and clicking a text field *means* focusing it and placing the caret.

---

## Verified against WDC's source, 2026-08-11

The owner noted they could not confirm the implementation matched their description. Read
directly instead, at `WindowsDriverCore/src/WindowsDriverCore.Automation/Uia/UiaElementInteractor.cs`.

| Claim | Status |
|---|---|
| Out-of-window mouse target is **refused, not clamped** | **CONFIRMED.** `if (!inside) return ElementAction.Failed(NotInteractable)` — *"Refused, loudly, rather than dispatched."* This was the one item where the wrong answer would have been worse than WinAppDriver. |
| Pattern activation is the **default**, mouse is a fallback | **CONFIRMED.** `ClickElementOrAncestor` runs ScrollIntoView, foregrounds, tries the pattern ladder, then the ancestor walk, and only then *"Last rung: real mouse input, guarded."* |
| Ancestor walk (A2) is implemented | **CONFIRMED.** Commented *"The rung that fixed the CollectionView."* |
| `Focus()` for `Edit`/`Document` — the gap this spec predicted | **NOT A GAP.** Handled explicitly, and gated on control type: *"a blanket SetFocus() fallback is how the previous implementation reported success for doing nothing."* |

**A difference that changes the deletion argument.** The two ladders disagree on ORDER:

```
PBJ ClickElement : Invoke -> Toggle -> SelectionItem -> ExpandCollapse
WDC ClickOne     : Toggle -> SelectionItem -> Invoke  -> ExpandCollapse
```

WDC puts the state-bearing patterns first, and measured the reason rather than assuming it: charmap's
Win32 checkbox advertises Toggle **and** Invoke, so an Invoke-first ladder makes the Toggle rung
unreachable on every classic checkbox, and 9 of 22 Settings ListItems advertise Invoke alongside
SelectionItem. Providers over-advertise Invoke.

**PBJ's ladder is Invoke-first — the ordering WDC measured as wrong.** So keeping it after the swap
does not merely duplicate work, it *overrides* the better ordering with the worse one. Delete it.

**One thing to watch on the mouse rung.** It refuses a zero-size bounding rectangle, while PBJ's own
`ClickElement` carries the comment *"Do NOT reach for the bounding rectangle here. These buttons
report 0x0 to Appium."* Pattern-capable elements never reach that rung, so this only bites a
pattern-less element — which PBJ has largely eliminated by converting tappable `Border`s to
`Button`s for accessibility. Worth a check rather than an assumption.

---

## A2 — Activation must work on an element whose pattern lives on an ancestor

**The defect.** A `CollectionView` row carries its `AutomationId` on a `Border` **inside** the item
container, and the container holds `SelectionItemPattern`. So the id names a child with no pattern
while its parent is perfectly selectable. PBJ walks up three levels to find it.

**Acceptance test.** Activate a `CollectionView` item by the `AutomationId` on its inner `Border`
(the archetype popup items, `ArchetypeItem_{Name}`). Selection must occur.

**Status: WDC reports this already fixed (2026-08-11).** So this is a regression test rather than
an open requirement — keep it in the suite, and the three-level ancestor walk in `ClickElement` is
the deletion candidate once a run confirms it.

---

## A3 — Session creation must not race a listener

**The defect.** The WinAppDriver process starts and reports itself up **before binding its HTTP
listener**, so session creation fails with `connect ECONNREFUSED 127.0.0.1:4725`. There is no
element to sync on, so the only signal available is another connection attempt. CI run
`31032240413` (ReadJournalPageTests) burned all three retries inside ~20s and failed the job before
a single test ran.

**Revised 2026-08-11 — the original criterion was wrong.** It asked for an in-process driver with
no listener to race. WDC has to BE a server: that is what WinAppDriver and Appium compatibility
means, so the race is structural rather than a defect it can design away. WDC also notes the fault
may sit on the Appium client side, which is worth knowing but does not change what the harness
needs.

**Restated: readiness must be observable.** The harness must be able to synchronise on a condition
instead of guessing with a delay. Either is sufficient:

- the driver does not report started until its listener accepts connections, or
- it exposes something pollable (a status endpoint, a named event, a ready file) that flips exactly
  when sessions can be created.

**Acceptance test.** 20 consecutive cold starts. The harness waits on the readiness signal, with no
`Task.Delay` and no retry loop, and creates a session first time in every one.

**Deleted on pass.** `retryDelaysMs = [5_000, 15_000, 30_000]` and the attempt loop in
`AppiumSetup`. This is the suite's only sanctioned `Task.Delay` — removing it restores the
no-sleeps rule with no exception left standing, which is the real prize here.

---

## A4 — Presence must mean one thing, and it must be the visible thing

**The defect, and the trap.** PBJ's presence check has been swapped twice and broke tests both
times, in opposite directions:

- **Tree existence** broke one test — MAUI keeps filtered `CollectionView` items realised, so they
  exist in the tree while not being present to the user.
- **`!IsOffscreen`** broke five — the BO3 panels report `IsOffscreen` while WinAppDriver considers
  them present.

**Acceptance test.** Both cases, explicitly: a filtered-out `CollectionView` item must report
**absent**; a BO3 panel that is scrolled out of view must report **present**. A driver that answers
either one differently from WinAppDriver changes the meaning of every `IsElementPresent` call in
the suite.

**Nothing is deleted here.** This is a compatibility requirement, not a defect — and it is the
criterion most likely to pass a targeted check while breaking the suite. See the whole-suite rule
below.

---

## A5 — Give the CALLER a way to say a lookup is optional

**The defect.** An absent element cost ~6.8s at the 5s ambient implicit wait, against ~215ms when
present. Across a suite full of "is this dialog showing?" checks that dominated the runtime; fixing
it took the Windows suite from 227s to 115s.

**Revised 2026-08-11 — this was misfiled as a driver defect.** WDC's objection is correct: the
driver cannot know whether a missing element is a failure or an expected absence. That is the
caller's knowledge, and today PBJ expresses it by mutating the ambient implicit wait to zero around
each optional call and restoring it afterwards — stateful, easy to get wrong, and it leaks if an
assertion throws in between.

**Restated as an API request, which WDC has already offered.** An explicit per-call way to say "not
finding this is a legal outcome" — a flag, an overload, or a `TryFind` returning null — using a
zero or near-zero timeout without touching ambient state.

**Acceptance test.** An optional lookup for an element that does not exist returns in well under
100 ms, and the ambient implicit wait is unchanged afterwards. Assert the second part: a leaked
wait is invisible until it slows a LATER test rather than failing this one.

**Nothing is deleted, and that is why it is listed.** The `TimeSpan.Zero` discipline took the
Windows suite from 227s to 115s. It must not be swept away as a driver workaround during the swap —
it should be REPLACED by the explicit API above, which does the same job without the ambient-state
juggling.

---

## The rule that governs the whole swap

**Run the full Windows suite at CI geometry before and after, and compare the SET of passing tests,
not the count.**

A swap that still reports 83/83 while changing *which* 83 is the failure this misses, and it has
happened here before: a presence check swapped backends, passed its own sabotage check correctly,
and had quietly changed the question from "is it visible" to "is it in the tree" — flipping a real
test for a reason no targeted check could see.

Per-test verification cannot detect a change in what a shared helper *means*. **When the driver
changes, the whole suite is the experiment.**

The 18 accessibility contract tests are the sharpest instrument available for this. They read the
live UIA tree and require every interactive element to expose both a Name and a control pattern, so
a driver that surfaces elements differently fails them loudly rather than subtly.

---

## What must NOT be attributed to the driver

These look like driver workarounds and are not. They survive the swap unchanged:

| | Why it stays |
|---|---|
| `Border`/`Grid` converted to `Button`/`ImageButton` | A genuine accessibility fix. A pattern-less element is unusable by a screen reader on **any** driver — `SemanticProperties` on it is announced correctly and impossible to activate. |
| Android click-verify-retry | A different platform and a different failure: Android taps are dispatched and the MAUI gesture handler never runs. Windows clicks land in the wrong *place*; Android clicks land *nowhere*. The fixes do not transfer. |
| `NavigateTo` in every test | Discovery order is non-deterministic; unrelated to the driver. |
| MainPage `[TearDown]` VM reset | The three data pages are DI singletons and hold state across navigations. |

---

## Related

- `docs/memory/project_windows_mainpage_click_flake.md` — the six-test CI failure and its cause
- `docs/memory/feedback_flaui_scroll_into_view.md` — the presence-check swaps
- `docs/memory/project_accessibility_contract_tests.md` — the 18 contract tests
- `docs/memory/feedback_uitest_timeouts.md` — the ambient-wait cost
- `docs/memory/project_pbj_is_the_dogfood_target.md` — why PBJ is the consumer
