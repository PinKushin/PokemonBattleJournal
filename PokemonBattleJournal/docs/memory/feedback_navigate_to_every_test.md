---
name: feedback_navigate_to_every_test
description: Navigation is in [OneTimeSetUp] per page class — never call NavigateTo inside individual tests
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-07-30T19:59:42.368Z
---

After NUnit migration, each page test class has `[OneTimeSetUp]` that calls `NavigateTo` once for the whole fixture. Individual tests must NOT call NavigateTo — it belongs in setup, not tests.

**Why:** xUnit required NavigateTo in every test because test classes were re-instantiated per test. NUnit reuses one instance per fixture. Moving NavigateTo to `[OneTimeSetUp]` means navigation happens once per class instead of once per test — faster and a single clear failure point when a page stalls rather than a cascade of every test failing.

**How to apply:** Add `[OneTimeSetUp] public void SetUp() => NavigateTo("Page Name");` to each page test class. Remove any NavigateTo calls from individual `[Test]` methods. If a test navigates away intentionally, call `InvalidateCurrentPage()` so the next `[OneTimeSetUp]` knows to re-navigate.

**MainPage note:** MainPageViewModel is a singleton — state persists across tests. Use `[TearDown]` to reset form state (BOSwitch, pickers, input fields) after each test. `[OneTimeTearDown]` calls `InvalidateCurrentPage()`.

**Related:** [[uitest-nav-cascade-fix]]
