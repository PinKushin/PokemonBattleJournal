---
name: project_ui_element_lookup_resilience
description: UI element lookup in Appium tests should use resilient strategies with retries to handle timing variations
metadata: 
  node_type: memory
  type: project
---

## Summary
UI element lookup in Appium tests should implement retry mechanisms with increasing timeouts to handle timing variations and avoid flaky tests. This approach proved effective in resolving intermittent `NoSuchElementException` failures in Windows UI tests after UI updates.

## The Problem
Windows UI tests were failing intermittently with `NoSuchElementException` for elements like `Game2Tab` and `Game3Tab` after interacting with pickers. The failures occurred because:

1. After selecting an item in a picker (e.g., setting game result to "Win" or "Loss"), the UI updates bound properties like `ShowGame3`
2. These property changes trigger UI re-renders to show/hide related elements (like the Game 3 tab)
3. During this brief re-render period, the element might not be immediately available for lookup
4. The original `FindUIElement` implementation for Windows only attempted a single lookup via `AccessibilityId`

This was a classic race condition between test execution speed and UI update completion.

## The Solution
Made Windows `FindUIElement` implementation match the resilient approach already used for Android:

1. **Multiple attempts with increasing timeouts**:
   - First attempt: 3-second implicit wait (quick check for already-visible elements)
   - Second attempt: 10-second implicit wait (for elements appearing after brief delay)
   - Third attempt: 10-second implicit wait (final attempt before giving up)

2. **Consistent cross-platform approach**:
   - Android already used a 3-stage lookup (resourceId with 3s/10s/10s timeouts + scrollable fallback)
   - Windows now uses the same timing strategy but with `AccessibilityId` lookup
   - This ensures both platforms handle timing variations similarly

3. **Proper timeout reset**:
   - Used `finally` block to reset implicit wait to 10 seconds after each attempt
   - Prevents leaked timeout settings from affecting subsequent operations

## Why This Works
- **Accommodates UI latency**: Gives the UI time to update after property changes trigger re-renders
- **Eliminates race conditions**: Instead of assuming immediate availability, waits appropriately
- **Maintains test speed**: Only waits as long as necessary; fast UI updates still complete quickly
- **Follows existing patterns**: Mirrors the proven Android implementation that was already resilient to similar issues
- **Adheres to principles**: Avoids arbitrary sleeps (`Thread.Sleep`/`Task.Delay`) in favor of element-driven waits

## Implementation Details
In `BaseTest.cs`, the Windows branch of `FindUIElement` now implements:

```csharp
if (App is WindowsDriver)
{
    // Three attempts with increasing timeouts, similar to Android implementation
    try
    {
        App.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(3);
        return App.FindElement(MobileBy.AccessibilityId(id));
    }
    catch (OpenQA.Selenium.NoSuchElementException)
    {
        try
        {
            App.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
            return App.FindElement(MobileBy.AccessibilityId(id));
        }
        catch (OpenQA.Selenium.NoSuchElementException)
        {
            App.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
            return App.FindElement(MobileBy.AccessibilityId(id));
        }
    }
    finally
    {
        App.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
    }
}
```

## Verification
- Fixed intermittent failures in `MainPage_Game3Tab_ShowsGamePanel` and `MainPage_Game3Tab_ShowsWhenGame1IsTie` tests
- All Windows UI tests now pass consistently locally
- Maintains compatibility with existing Android test resilience patterns
- Aligns with the project's `feedback_no_sleeps_in_tests.md` principle of using element discovery for synchronization

## Related Learnings
- `project_performance_logging_diagnostic_value.md`: Shows how performance logging helped expose this flakiness by making timing variations more consistent
- `feedback_no_sleeps_in_tests.md`: Reinforces that proper waits (like those implemented here) are superior to arbitrary sleeps
- `project_game3tab_test_bug.md`: Documents similar Android-specific resilience improvements

## Implementation Guidance
1. **Always use retry logic** for UI element lookup in automated tests
2. **Match timeouts to expected UI latency** (3s for instant, 10s for delayed appearance)
3. **Keep approaches consistent across platforms** when possible
4. **Reset shared state** (like implicit waits) in finally blocks to prevent leakage
5. **Prefer element-driven waits** over time-based sleeps for synchronization