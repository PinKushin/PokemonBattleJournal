---
name: feedback_readjournal_test_toggle
description: NUnit alphabetical ordering causes ReadJournal row to collapse; use EnsureMatchDetailOpen() guard
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-08-03T20:53:11.456Z
---

NUnit runs tests alphabetically within a class. `BO3Match` runs before all `SelectMatch_*` tests. `BO3Match` clicks the first row (opens detail). Then each `SelectMatch_*` test re-clicks the same already-open row — which collapses it — so the detail elements aren't found.

**Fix:** `EnsureMatchDetailOpen()` checks `PlayingNameLabel` visibility with 0ms wait. Only clicks if not already open:
```csharp
private void EnsureMatchDetailOpen()
{
    App.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
    bool alreadyOpen = App.FindElements(MobileBy.AccessibilityId("PlayingNameLabel")).Count > 0;
    App.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);
    if (!alreadyOpen)
        FindFirstMatchRow().Click();
}
```
BO3 variant checks `Game2TagsView` instead. All `SelectMatch_*` tests call `EnsureMatchDetailOpen()` at the start.

Also: BO3 Loss is seeded with `DatePlayed = DateTime.UtcNow.AddDays(1)` so it's always row 0 (newest-first sort) regardless of what other tests add. `FindFirstMatchRow()` is always correct.

**How to apply:** Any ReadJournal test that needs the detail panel open must call `EnsureMatchDetailOpen()`, never click blindly.

## The other half: an open detail panel hides the match list on Android (2026-08-06)

The same alphabetical ordering has a second consequence, in the opposite direction.
`ReadJournalPage_HasSeededMatches` looks up rows with a bare
`resourceIdMatches("...MatchRow_.*")`. `MatchHistoryList` is a **virtualized**
`CollectionView`, so rows pushed out of the viewport leave the accessibility tree entirely and
that lookup finds nothing.

Both BO3 tests sort ahead of it and both leave a detail panel open above it. So a
**data-presence test depended on the height of a panel below it** — adding the game 2/3 note
editors (~400px) broke it, on a change that touched nothing about the match list.

**Signature: passes in isolation, fails in the fixture.** Run the single test with a
`FullyQualifiedName~` filter; if it goes green, stop debugging the app and look at what ran
before it.

**Fix: `[Order(1)]`.** It runs on a freshly loaded page, which is what the test means anyway —
"the journal lists seeded matches when you open it".

**Two compensating fixes were tried first and both failed. Do not reach for them again:**

- **Scrolling back to the top** (`UiScrollable.scrollToBeginning`) does not re-realize the
  rows. Worse, putting that scroll inside `FindFirstMatchRow` — which the EnsureMatchDetailOpen
  guards call — moved the detail panel out of view, so the guard's `IsElementPresent` read false
  and it clicked the row again, collapsing the panel. One failure became two.
- **`NavigateTo("Read Journal")` mid-fixture is a no-op.** It returns early when already on the
  page, so it resets nothing. It is not a way to get a clean slate.

The general lesson: when a UI test's setup is order-dependent, fix the order. Compensating for
the previous test's leftovers means every future layout change gets to break it again.
