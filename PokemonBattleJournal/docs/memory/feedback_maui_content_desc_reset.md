---
name: feedback_maui_content_desc_reset
description: MAUI Android re-renders native views during IsVisible binding updates, resetting content-desc; use resourceId (FindUIElement) not AccessibilityId after picker interactions
metadata:
  type: feedback
---

After any MAUI Picker selection on Android, binding updates that affect `IsVisible` on nearby elements trigger native view re-renders. During these re-renders, `content-desc` on sibling elements resets from `AutomationId` value to `SemanticProperties.Description` value. `MobileBy.AccessibilityId(id)` searches `content-desc` — finds nothing.

Use `FindUIElement(id)` (resourceId 3-stage lookup) instead. ResourceId is assigned at layout inflation and is never touched by binding updates.

**Why:** Discovered when `Game2Tab` became unfindable after selecting "Tie" in `PossibleResultsPicker`. The `Result` property setter fires `[NotifyPropertyChangedFor(nameof(ShowGame3))]`, which updates `Game3Tab.IsVisible`, which re-renders the tab layout, which resets `Game2Tab`'s `content-desc`.

**How to apply:** After any `Picker.Click()` + item selection on Android, use `FindUIElement("ElementId")` for all subsequent lookups in that test, not `MobileBy.AccessibilityId("ElementId")`. This applies even to elements that appear unrelated to the changed binding.
