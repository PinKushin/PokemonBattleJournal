---
name: project_performance_logging_exposes_flakiness
description: Performance logging changes can expose pre-existing flakiness in UI tests due to altered timing
metadata: 
  node_type: memory
  type: project
---

## Summary
Adding performance logging to test infrastructure (commit 2441b3f) exposed a pre-existing flakiness in Windows UI tests related to element lookup timing after picker interactions. The Android tests were already resilient due to robust element lookup logic, but Windows tests used a simpler approach that became unreliable when timing shifted slightly.

## Root Cause
- The `FindUIElement` method in `BaseTest.cs` had inconsistent implementation between platforms:
  - Android: Used 3-stage lookup with retries (3s, 10s, 10s timeouts)
  - Windows: Single attempt with no retries for `AccessibilityId` lookup
- Commit 2441b3f added `PerfLog` calls throughout test setup/teardown, slightly altering execution timing
- This timing change made the Windows element lookup more likely to fail intermittently when:
  1. A picker selection triggered UI updates (e.g., changing `IsVisible` bindings)
  2. The test immediately tried to find a tab element (`Game2Tab`/`Game3Tab`)
  3. The UI hadn't fully updated before the lookup attempt

## Solution
Made Windows `FindUIElement` use the same retry pattern as Android:
- First attempt: 3s implicit wait
- Second attempt: 10s implicit wait on `NoSuchElementException`
- Third attempt: 10s implicit wait with `UiScrollable` for off-screen elements
- Always reset implicit wait to 10s in `finally` block

## Prevention
1. **Cross-platform consistency**: UI test helpers should have identical robustness strategies across platforms
2. **Timing isolation**: Performance logging should be added carefully to avoid masking flakiness - better to fix underlying issues first
3. **Proactive flakiness detection**: Regularly run tests multiple times in CI to catch intermittent issues before they block merges

## Related
- `project_game3tab_test_bug.md` - Documents similar Android-specific flakiness fixed by using resourceId over AccessibilityId
- `feedback_no_sleeps_in_tests.md` - Reinforces that waits should be element-based, not time-based
- Commit c8eaf7e: Fix implementing this solution