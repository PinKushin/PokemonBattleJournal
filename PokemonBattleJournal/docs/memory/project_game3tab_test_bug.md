---
name: project_game3tab_test_bug
description: RESOLVED — Game2Tab/Game3Tab AccessibilityId lookup fails after picker interaction on Android; root cause and fix documented
metadata:
  type: project
---

**Status: RESOLVED** on `feature/nunit-migration` (commits 378bed6, d2d7298).

**Root cause:** Selecting a Picker item on Android fires `NotifyPropertyChangedFor(nameof(ShowGame3))` on `Result` (and `Result2`). MAUI updates the `Game3Tab.IsVisible` binding, which triggers a native view re-render of the tab bar. During that re-render, Android resets `content-desc` on adjacent views from `AutomationId` ("Game2Tab") back to `SemanticProperties.Description` ("Game 2 tab"). `AccessibilityId` lookup searches `content-desc` — finds nothing — throws `NoSuchElementException`.

**Fix:** Use `FindUIElement("Game2Tab")` (resourceId 3-stage lookup) instead of `MobileBy.AccessibilityId("Game2Tab")`. ResourceId (`com.PinKushin.PokemonBattleJournal:id/Game2Tab`) is set at layout inflation and never reset by binding updates.

**Why:** MAUI Android binding updates that change `IsVisible` on any element can cause native re-renders of the surrounding layout, resetting `content-desc` on sibling elements. AccessibilityId is fragile in this context; resourceId is stable.

**How to apply:** After any Picker selection in Android UI tests, use `FindUIElement(id)` not `MobileBy.AccessibilityId(id)` for elements in the same layout region. See [[feedback_maui_content_desc_reset]].
