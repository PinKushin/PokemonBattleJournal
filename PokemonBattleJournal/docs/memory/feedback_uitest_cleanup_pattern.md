---
name: feedback_uitest_cleanup_pattern
description: UI test cleanup pattern — targeted helpers in try/finally, not [TearDown], to avoid per-test overhead
metadata:
  node_type: memory
  type: feedback
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-07-30T20:32:11.541Z
---

Use targeted cleanup helpers called in `try/finally` blocks, NOT `[TearDown]`, for UI test state reset.

**Why:** `[TearDown]` runs after every test including display-only tests that change no state. Each driver round-trip (even at 0ms ImplicitWait) adds overhead. With 30+ tests per platform and 15-20 min run times, blanket teardown was a significant contributor.

**How to apply:**
- Create named helpers (e.g. `ResetBOSwitch()`, `DeleteCreatedTag(name)`) in the test class
- Helpers set `ImplicitWait = TimeSpan.Zero`, do their work in try/catch NoSuchElementException, restore wait in finally
- Only the tests that mutate state call the relevant helper(s) in their own `try/finally`
- Display-only tests have no cleanup at all — zero driver overhead between them

**Exception:** `BaseTest` has `[SetUp]/[TearDown]` for timing/perf logging only — that's acceptable since it's pure C# (no driver calls).

**MainPage is singleton VM** — ResetBOSwitch/ResetGame1Tab/CloseWindowsPickers/ClearUserNoteInput are in the shared MainPageTests partial. Platform-specific partials can call them too.

**Related:** [[project_nunit_migration]]
