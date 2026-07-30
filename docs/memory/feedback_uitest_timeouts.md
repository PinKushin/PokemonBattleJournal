---
name: feedback_uitest_timeouts
description: "UI test implicit wait timeouts — 5s Windows, 10s Android, 15s CI max"
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-07-30T07:40:18.191Z
---

15 seconds implicit wait for any element is a test failure, not a timeout. If an element takes that long the test is broken.

**Limits:**
- Windows native: 5s max — if visible on screen, should be found immediately
- Android/mobile: 10s max — allow for slower rendering
- CI: 10–15s tolerable but not acceptable long-term; fix locally first

**Why:** User can see the element on screen while WinAppDriver waits 15s — that means the selector or lookup strategy is wrong, not that the element is slow to appear.

**How to apply:** Set `ImplicitWait = TimeSpan.FromSeconds(5)` in Windows `AppiumSetup`, `TimeSpan.FromSeconds(10)` in Android `AppiumSetup`. Fast-fail paths (cleanup try/catch) should use `TimeSpan.Zero`. The Android fast-lookup fallback in `FindUIElement` uses a 3s fast attempt then full wait — align the full wait to the 10s limit.
