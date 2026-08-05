---
name: project_readjournal_android_slow
description: ReadJournal SelectMatch_* tests take 50s+ each on Android — investigate STAGE1_MISS vs test bug
metadata:
  type: project
---

**Status:** open. Blocks fast local Android test iteration.

Android ReadJournalPage `SelectMatch_*` tests take 50s+ per test. User confirmed they all use the same already-open row and do NOT click anything between tests — so it's NOT re-open-row overhead.

Full Android suite becomes unbearable to run locally because of this cluster.

## Diagnostic path

Grep new PerfLog for a `ReadJournalPage_SelectMatch_*` test and inspect `FIND` lines:

- **If `STAGE1_MISS` fires on elements clearly visible on screen** → AutomationId propagation issue. The MAUI row/detail template may be re-rendering after each test, invalidating resource-id, forcing STAGE2 content-desc fallback + STAGE3 scrollIntoView (5s + 0.5s + 1-3s per lookup × 6-7 lookups = ~50s).
- **If `STAGE1_OK` fires fast but test still runs 50s** → test itself doing something slow (WebDriverWait somewhere, sleeps, redundant lookups).
- **If tests fail because element is at wrong position or state** → tests broken.

## Actual finding (2026-08-04 run 17:31)

Neither of the above. Every element lookup on ReadJournalPage hits `STAGE1_OK` — but takes **~20193 ms per lookup** (nearly identical across all tests). MainPage lookups on the same run: 30-100 ms typical. So FindUIElement itself is fine — the underlying `App.FindElement(MobileBy.AndroidUIAutomator("new UiSelector().resourceId(...)"))` call takes 20 s server-side.

`SelectMatch_ShowsArchetypeNames` (2 lookups × 20 s = 40 s + row-open + nav = 111 s) is the worst; single-lookup tests land at ~50 s.

Two likely causes:

1. **UIA tree too big on ReadJournalPage** — MatchHistoryList CollectionView renders every seeded match. Each row has many bound labels/icons + a nested detail card. The UiAutomator2 server serializes the whole subtree per resource-id query — big tree = slow query. **Fix:** reduce seeded match count for tests OR narrow the resource-id selector (className+resourceId) OR virtualize the list.
2. **App stalling on async render** — no loading indicator, so UIA server may be waiting for UI thread to be idle (`waitForIdle`). Chart rendering / image decode / Limitless fetch could block for ~20 s per query. **Fix:** matches the roadmap loading-indicator ask — once the app has an `IsLoading` gate, tests can wait for it to clear before querying elements; UIA server no longer waits for a busy UI thread.

The `20193 ms` uniformity across unrelated elements is the clue — that's a fixed timeout somewhere (Appium `waitForIdleTimeout` defaults to ~10 s but can be extended; or MAUI's frame render blocking). The tests eventually succeed because the element IS there — it's just that reaching it costs a full stall interval.

## Next steps

- Add a very small test-only debug seed for ReadJournalPage (say 3 matches instead of 20) via a `#if DEBUG` flag or env var, run the suite, see if lookups drop from 20 s → 200 ms. That would confirm hypothesis 1.
- If not, add a loading indicator on ReadJournalPage and have tests `WaitUntilGone("LoadingIndicator")` before element lookups.

## Update — 2026-08-04

Confirmed: dropping seed from 14 → 4 matches did NOT reduce lookup time. Still ~20200 ms per UIA call. So NOT tree size / seed count.

Also confirmed the 20200 ms cost is per **UIA call** (not per FindElement) — `.Text` access on an already-found element also blocks 20 s, and `IsElementPresent` (ImplicitWait=0) blocks 20 s too. Everything routes through Appium UiAutomator2 which respects `waitForIdleTimeout` (~20 s default) — meaning **the app's UI thread is never idle while ReadJournalPage is visible**.

### Suspects on ReadJournalPage (XAML at PokemonBattleJournal/Views/ReadJournalPage.xaml)

Four CollectionView declarations, not 34 (grep counted opening + closing tags + sub-elements):

1. `MatchHistoryList` (line 266) — outer match list with:
   - `SelectedItem="{Binding SelectedMatch}"`
   - `SelectionChangedCommand="{Binding LoadMatchCommand}"` — mutates ~10 ObservableProperties synchronously
   - `ItemSizingStrategy="MeasureFirstItem"` + `ItemsUpdatingScrollMode="KeepItemsInView"` — continuous measure/scroll callbacks
2. `TagsSelectedGame1` (line 106) — tag list in Game 1 detail panel
3. `TagsSelectedGame2` (line 156) — same for Game 2
4. `TagsSelectedGame3` (line 207) — same for Game 3

Any of these can keep the UI thread churning between UIA queries. Suspect combo: `MatchHistoryList` selection cascade + three CollectionViews each running their own measure/layout loop.

### Fix plan (deferred until MainPage failures resolved)

1. **Swap the 3 tag CollectionViews for `FlexLayout` + `BindableLayout`** — same pattern that fixed the MainPage tag flash. Safe drop-in, no VM changes.
2. If ReadJournal lookups still ~20 s after step 1, swap `MatchHistoryList` for `FlexLayout` + `BindableLayout` too. Requires losing `SelectedItem` binding — replace with `TapGestureRecognizer` per row firing `LoadMatchCommand` with the item as parameter. VM maintains `SelectedMatch` from the tap handler.
3. If STILL slow, add `IsBusy_MatchHistory` gate (roadmap loading-gate design) — flip fast on data-load complete, tests `WaitUntilGone("Busy_MatchHistory")` before UIA queries.

Do not lower Appium `waitForIdleTimeout` as a workaround — that hides broken app behavior and can return stale UIA nodes.

### Key learning

Any MAUI page with 4+ CollectionViews + a selection-driven cascade of ~10 property mutations is going to burn the UiAutomator2 `waitForIdle` budget on every single UIA call. `FlexLayout` + `BindableLayout` is the escape hatch when you don't need CollectionView's virtualization or selection semantics.

## Related

- [[feedback_maui_content_desc_reset]] — MAUI Android re-render resets content-desc/resource-id after IsVisible bindings; the fix pattern is FindUIElement (retries) rather than bare AccessibilityId
- [[project_android_element_lookup]] — Direct resourceId (3s) first, UiScrollable fallback pattern

## When to revisit

After MainPage 8-failure fix session lands. This is next-in-queue for Android test speed / stability work.
