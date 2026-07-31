---
name: project_performance_logging_diagnostic_value
description: Performance logging in test infrastructure is valuable for diagnosing flakiness and should be embraced rather than avoided
metadata: 
  node_type: memory
  type: project
---

## Summary
Performance logging added to test infrastructure (via PerfLog calls in AppiumSetup and BaseTest) is a valuable diagnostic tool that helps expose pre-existing flakiness in UI tests. Rather than avoiding such logging due to fear of exposing issues, it should be added extensively to improve test reliability and maintainability.

## The Value Observed
In commit 2441b3f ("feat: enhanced AppiumSetup with detailed performance logging"), detailed timing logs were added to:
- Windows and Android AppiumSetup (build, driver creation, activity wait, etc.)
- BaseTest.NavigateTo (navigation steps)
- Windows AppiumSetup cleanup
- BaseTest setup/teardown (per-test timing)

These logs, written to `UITests.PerfLog.txt`, revealed:
1. **Test performance bottlenecks** - Identified slow steps in test setup
2. **Flakiness exposure** - The slight timing changes from logging made a pre-existing race condition in Windows UI tests manifest consistently as failures (NoSuchElementException for Game2Tab/Game3Tab after picker interactions)
3. **Root cause diagnosis** - By comparing logs between passing and failing runs, the timing discrepancy in element lookup after UI updates became clear

## Why This Is Beneficial
1. **Early detection**: Flakiness that might occur randomly in CI becomes consistent and diagnosable when logging alters timing predictably
2. **Targeted fixes**: Performance logs pinpoint exactly where time is spent, guiding optimization efforts (e.g., identifying slow Appium server startup vs. test execution)
3. **Regression prevention**: Logging creates a baseline for performance; significant deviations indicate potential issues
4. **Knowledge sharing**: Logs provide objective data for debugging, reducing guesswork in triage

## Recommended Practice
- **Add performance logging liberally** to test infrastructure:
  - Test setup/teardown phases
  - Navigation steps between pages
  - Critical setup sequences (emulator start, app build, driver initialization)
  - Cleanup operations
- **Log to dedicated files** (like UITests.PerfLog.txt) to avoid cluttering test output
- **Include millisecond precision** for meaningful comparisons
- **Use consistent formatting** for easy parsing and analysis
- **Review logs regularly** as part of test maintenance, not just when failures occur

## Countering Common Objections
- "Logs might hide real issues by changing timing": Actually, the opposite - they make intermittent issues consistent and easier to catch
- "Logging adds overhead": The overhead is minimal (microseconds for file writes) compared to test execution times (seconds to minutes)
- "We should fix flakiness first": Logging helps identify what needs fixing; it's a diagnostic tool, not a replacement for fixes

## Related Learnings
- `project_game3tab_test_bug.md`: Shows how logging/exposure of timing issues led to fixing Android-specific flakiness
- `feedback_no_sleeps_in_tests.md`: Reinforces that proper waits (like those revealed in performance logs) are better than arbitrary sleeps
- Commit c8eaf7e: Fix that made Windows element lookup robust after performance logging exposed the flakiness

## Implementation Example
From the fix in commit c8eaf7e:
```csharp
// In AppiumSetup.cs
private static void PerfLog(string message)
{
    string line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
    try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "UITests.PerfLog.txt"), line + Environment.NewLine); } catch { }
}

// Usage throughout setup:
var stepTimer = System.Diagnostics.Stopwatch.StartNew();
// ... operation ...
stepTimer.Stop();
PerfLog($"[{DateTime.Now:HH:mm:ss.fff}] Step completed ({stepTimer.ElapsedMilliseconds}ms)");
```

This approach has proven valuable in identifying and fixing multiple test reliability issues in this project.