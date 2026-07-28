---
name: feedback_no_sleeps_in_tests
description: Never add Thread.Sleep or Task.Delay in UI tests or seed setup — use implicit wait via element discovery instead
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-07-28T04:22:12.389Z
---

Never use `Thread.Sleep` or `Task.Delay` in Appium UI tests or seed setup code.

**Why:** Sleeps mask root causes, slow tests down, and are brittle. The correct sync point is element discovery — `FindElement` with implicit wait (15s) will block until the element appears or timeout, which is both faster and correct. When confirmed: removing all sleeps made the suite run 8s faster (40s → 32s) and exposed a real bug that sleeps were hiding.

**How to apply:**
- After clicking a button/picker/tab, find the NEXT expected element — don't sleep first.
- After save operations, confirm completion by finding a known post-save element (e.g. SaveMatchButton reappearing after form clear).
- After navigation, find the first element on the destination page — don't sleep for "animation".
- Windows tests run via WinAppDriver against the native UIA accessibility tree — state changes are reflected synchronously. Zero reason for any wait.
- CI sleeps are tolerable (user not blocked waiting). Local test sleeps are never acceptable — slow feedback, mask bugs. [[feedback_engineering_principles]]
