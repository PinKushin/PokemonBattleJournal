# WindowsDriverCore — acceptance spec, from PokemonBattleJournal

What PBJ requires of WindowsDriverCore before its Windows UI suite is switched over.

PBJ is the dogfood consumer, so this is written from the consumer's side: every criterion below is
a **defect PBJ currently works around**, with the evidence it was real and the test that proves it
is gone. Nothing here is speculative — each one cost debugging time and has a scar in this repo.

**How to use it.** Each criterion has a *defect*, an *acceptance test*, and *what gets deleted*
when it passes. The deletions are the point: if a workaround survives the swap, nobody learns
whether WDC fixed the thing it exists for.

---

## A1 — An out-of-window target must FAIL LOUDLY, never be clamped into the window

**The defect.** WinAppDriver's `.Click()` is synthesized mouse input at the element's centre in
**screen** coordinates, carrying no UIA pattern. A target laid out below the window is clicked at
whatever screen position that resolves to. Locally that launched Visual Studio and the Epic Games
store off the taskbar; on CI it landed on empty desktop, returned in ~1s having done nothing, and
left find-only tests passing. **Six tests failing on CI for two days.**

**Revised 2026-08-11 after WDC feedback.** A click has to be real mouse input — WinAppDriver's own
suite tests for that specifically, and Appium compatibility requires it. So "do not use
coordinates" was never the achievable requirement. WDC now bounds the click to the application
window itself, which makes the real question **bounded how**, and the two answers are not close:

| Behaviour for an out-of-window target | Verdict |
|---|---|
| **Clamped** into the window | **WORSE than the original bug.** The old failure clicked empty desktop and did nothing; a clamped click activates a DIFFERENT element and the test continues against the wrong state. Silent-and-wrong beats silent-and-inert. |
| **Refused, with an error naming the element and the bounds** | Correct. The containment guard becomes redundant. |

**Acceptance test.** With the window at CI geometry (`754x512` at position `85,78`), attempt to
activate a control laid out below the window bottom **without scrolling it into view first**. It
must throw, and the message must identify the element and the window bounds. Assert positively that
no other element changed state — a clamped click is only distinguishable from a refusal by looking
at the bystander.

```powershell
UITEST_WINDOW_SIZE=754x512 UITEST_WINDOW_POS=85,78 dotnet test PokemonBattleJournal.UITests/UITests.Windows/UITests.Windows.csproj
```

Both env vars, not just the size. Size alone pins the window to `(0,0)`, where screen space and
window-relative space coincide — which is exactly how this bug hid locally while failing on CI.

**Deleted on pass.** The mouse-path containment guard that refuses a target measured outside the
window, since the driver now owns that check.

**NOT deleted, correcting the first draft of this spec.** The pattern ladder in
`TestBase.ClickElement` stays. Its `ScrollItem` step is doing real work — bringing the target
*into* the window so a real click can land — and that is required precisely BECAUSE the click is
coordinate-based. The Invoke/Toggle/SelectionItem steps also remain the only coordinate-free path
for elements the mouse cannot reach reliably.

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
