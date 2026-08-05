---
name: feedback_android_composite_widget_accessibility
description: "MAUI composite widgets (SearchBar, Picker) don't propagate AutomationId to resource-id on Android — use SemanticProperties.Description for content-desc fallback"
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-08-04T20:03:00.199Z
---

MAUI controls that render as native Android **composite widgets** — most notably `SearchBar` → `androidx.appcompat.widget.SearchView`, and `Picker` → AlertDialog — often do NOT propagate `AutomationId` to `resource-id` on the widget's root native view. The id may end up on an inner child (EditText inside SearchView) or be dropped entirely.

`MobileBy.AndroidUIAutomator("new UiSelector().resourceId(...)")` then finds nothing.

**Why:** simple `View` subclasses (Button, Label, Entry) get their AutomationId set via `View.setTag(R.id.mauiAutomationId, ...)` and Appium's uiautomator2 driver reports that as resource-id. Composite widgets have their own view hierarchy and MAUI's handler doesn't recursively tag the children.

**How to apply:** whenever adding an `AutomationId` to a MAUI `SearchBar`, `Picker`, or other composite control, also set `SemanticProperties.Description` to the same value. That sets `content-desc` on the widget's root View, which is stable and cross-window (works for popup Dialog children too).

```csharp
searchBar.AutomationId = "ArchetypeSearchBar";
SemanticProperties.SetDescription(searchBar, "ArchetypeSearchBar");
```

On the test side, the Android `FindUIElement` should have a fallback that tries `MobileBy.AccessibilityId(id)` (content-desc) between the resource-id direct lookup and the UiScrollable fallback. This is now built into `PokemonBattleJournal.UITests/UITests.Android/BaseTest.cs`.

**Related:** [[feedback_maui_content_desc_reset]] — content-desc can reset after IsVisible binding cascades; use `FindUIElement` (which retries) rather than a bare `MobileBy.AccessibilityId` call for elements adjacent to picker interactions.
