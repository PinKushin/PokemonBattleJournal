---
name: feedback-codebehind-preference
description: User prefers pure XAML views with no code-behind; accepts it only for view-layer layout logic
metadata:
  type: feedback
---

Prefer pure XAML views — no logic in code-behind files.

**Why:** User's stated preference for MVVM separation and clean views.

**How to apply:** Default to ViewModel commands, behaviors, converters, and XAML bindings. Code-behind is acceptable ONLY for view-layer concerns that cannot be expressed in XAML (e.g., `OnSizeAllocated` for responsive layout adaptation). When using code-behind for layout, note it explicitly and offer a Behavior alternative. Never put business logic, data access, or navigation in code-behind.
