---
name: project_ui_test_coverage
description: Every Shell page must have a navigation + element-visible Appium UI test to catch page hangs before they reach production.
metadata:
  type: project
---

Every Shell page must have at least one Appium UI test that navigates to the page and asserts a key element is visible. `FindUIElement` will timeout if the page hangs, failing the test immediately.

**Why:** TrainerPage once deadlocked the WinUI3 message pump during chart initialization. No UI test existed, so the hang was only caught manually. User explicitly asked for tests to prevent this from happening silently again.

**How to apply:** When adding a new Shell page or a heavy control to an existing page, add (or verify) a `{Page}Loads` test in `UITests.Shared/Views/{Page}Tests.cs`. The test must: navigate via `NavigateTo("Shell title")`, call `FindUIElement("AutomationId")`, assert `.Displayed`. Also add an `AutomationId` to a stable landmark element on every new page.

**Current coverage (2026-07-26):**
- MainPage — `MainPage_BallIcon_DisplayedOnPage` (starts there)
- ReadJournalPage — `ReadJournalPage_WelcomeLabel_Displayed` (`JournalWelcomeLabel`)
- TrainerPage — `TrainerPage_StatsLabels_Displayed` (`TrainerWelcomeLabel`, `WinRateLabel`)
- OptionsPage — `OptionsPage_TitleLabel_Displayed` (`OptionsPageTitleLabel`)
- AboutPage — `AboutPage_Loads_TitleDisplayed` (`AboutPageTitle`)
