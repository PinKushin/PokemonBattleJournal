---
name: feedback_android_flaky_tap_retry
description: Appium taps silently miss MAUI gesture handlers on Android — always click-verify-retry against a state change, verified via viewport-visible elements
metadata:
  type: feedback
---

Appium `.Click()` on Android sometimes never reaches the MAUI handler — `TapGestureRecognizer` on Border/ContentView AND `Command` on Button both affected. The element is found (resource-id resolves), the WebDriver click "succeeds", but the app never sees the tap. Proved 2026-08-04 by in-app lifecycle logging (`ComboBoxControl.OnTapped` writes to CacheDirectory, pulled via `adb run-as` in AppiumSetup teardown → `%TEMP%\UITests.PopupLog.txt`): across a full MainPage run, 4 of 5 popup-open clicks never fired OnTapped. Likely cause: click dispatched while the page is still settling from a `UiScrollable` scroll fling.

**Why:** a single click + assertion is inherently flaky for anything tap-driven on Android. Wrapping the click in a longer wait does NOT help — the tap is already lost; only re-clicking recovers.

**How to apply — the pattern:**

```csharp
for (int i = 0; i < 3; i++)
{
    FindUIElement(targetId).Click();
    var deadline = DateTime.UtcNow.AddMilliseconds(2500);
    while (DateTime.UtcNow < deadline)
        if (/* state-change check */) return;
    PerfLog($"attempt {i + 1} missed, re-clicking");
}
throw new NoSuchElementException("...");  // fail loudly — silent miss cascades
```

Rules:
1. **Verify a state CHANGE, not element presence in general** — poll for what the tap causes (popup content appears, other panel's content disappears, label text flips).
2. **Verify with viewport-visible elements only.** UiAutomator exposes only on-screen views. Checking for an element that is off-screen below the fold loops forever even when the tap worked (bit us in ResetGame1Tab: verified Game 1 panel content that was scrolled out of view — switched to checking the Game 2/3 content DISAPPEARING instead, since that content was in the interacted-with viewport region).
3. **Cleanup helpers must throw on final failure** — a silent miss leaves hidden-panel state that cascades 17-20 s STAGE3 timeouts into every downstream test.
4. WaitUntilText returns silently on timeout — never treat "we waited" as "it happened"; re-read the state.

**Applied in `MainPageTests.cs`:** `OpenArchetypePopup` (popup content appears), `SelectAndroidPickerItem` (dialog item appears; owns the picker click), `ResetGame1Tab` (Game 2/3 content disappears), `EnsureBO3On` (label flips AND Game2Tab enters tree). Result: 25/25 MainPage Android tests green (was 7-10 failing).

Related: [[feedback_maui_content_desc_reset]], [[project_android_mainpage_failures]], [[feedback_cleanup_helper_timeout]]
