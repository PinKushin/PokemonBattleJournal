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
