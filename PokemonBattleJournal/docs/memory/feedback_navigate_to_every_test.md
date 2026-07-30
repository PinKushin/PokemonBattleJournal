---
name: feedback_navigate_to_every_test
description: Every UI test must call NavigateTo explicitly — never assume app is on a specific page
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-07-30T07:43:30.611Z
---

Every test in the Appium suite must call `NavigateTo("Page Name")` at the start. Never assume the app is on a particular page.

**Why:** xUnit test class discovery order is non-deterministic across platforms. On Windows, `MainPageTests` happened to run before `AboutPageTests` so tests passed without NavigateTo. On Android, `AboutPageTests` ran first (alphabetical), set `_currentPage = "About"`, and all MainPage tests failed immediately without logging — a silent cascade with 10-15s wait per test.

**How to apply:** Every `[Fact]` that targets a specific Shell page must start with `NavigateTo("Page Name")`. The `_currentPage` skip optimization handles redundant calls cheaply. No exceptions for "simple" tests.

**Fixed in:** All 15 shared `MainPageTests` methods, Windows-specific and Android-specific `MainPage_BOSwitch_DisplayedAndToggled`.

**Related:** [[uitest-nav-cascade-fix]]
