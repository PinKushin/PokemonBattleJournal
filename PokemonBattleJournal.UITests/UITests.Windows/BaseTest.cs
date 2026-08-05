using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace UITests
{
    public abstract class BaseTest : TestBase
    {
        // Lazily created; shared across all tests in the session. Not disposed — process exits after suite.
        private static UIA3Automation? _uia;
        private static UIA3Automation UIA => _uia ??= new UIA3Automation();

        protected override void DoNavigateTo(string pageTitle)
        {
            PerfLog($"NAV click OK");
            App.FindElement(MobileBy.AccessibilityId("OK")).Click();
            PerfLog($"NAV click '{pageTitle}'");
            App.FindElement(MobileBy.AccessibilityId(pageTitle)).Click();
        }

        // Poll every 500ms for up to 30s using WinAppDriver.
        // Every ~2s, also query the UIA tree directly via FlaUI — bypasses WinAppDriver's cached
        // element tree so MAUI IsVisible binding cascades (async on the UI thread) are detected
        // reliably. Once FlaUI confirms the element exists, spin-re-anchor WinAppDriver until it
        // picks it up too, then return the Appium handle for downstream test interactions.
        // Attempts slower than this are logged individually. A hit on a warm tree costs
        // ~150-250ms; anything above this is the expensive full-descendant-walk case we
        // are trying to attribute, so the threshold keeps the log readable.
        private const int SlowAttemptMs = 750;

        protected override AppiumElement FindUIElement(string id)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            var total = System.Diagnostics.Stopwatch.StartNew();
            int iteration = 0;
            try
            {
                while (true)
                {
                    var attempt = System.Diagnostics.Stopwatch.StartNew();
                    try
                    {
                        App.Manage().Timeouts().ImplicitWait = TimeSpan.FromMilliseconds(500);
                        AppiumElement found = App.FindElement(MobileBy.AccessibilityId(id));
                        if (attempt.ElapsedMilliseconds > SlowAttemptMs)
                            PerfLog($"FIND '{id}': HIT on attempt {iteration + 1} after {attempt.ElapsedMilliseconds}ms (total {total.ElapsedMilliseconds}ms)");
                        return found;
                    }
                    catch (OpenQA.Selenium.NoSuchElementException) when (DateTime.UtcNow < deadline)
                    {
                        if (attempt.ElapsedMilliseconds > SlowAttemptMs)
                            PerfLog($"FIND '{id}': MISS on attempt {iteration + 1} after {attempt.ElapsedMilliseconds}ms (implicit wait 500ms — remainder is the UIA tree walk)");

                        if (++iteration % 4 == 0)
                        {
                            // Standard re-anchor for elements that are just slow to appear.
                            var anchor = System.Diagnostics.Stopwatch.StartNew();
                            try { App.SwitchTo().Window(App.CurrentWindowHandle); }
                            catch (Exception ex) { NavLog($"re-anchor failed: {ex.Message}"); }
                            PerfLog($"FIND '{id}': re-anchor took {anchor.ElapsedMilliseconds}ms (iteration {iteration})");

                            // If WinAppDriver keeps missing it, check the live UIA tree directly.
                            if (IsVisibleViaUIA(id))
                            {
                                NavLog($"UIA confirmed '{id}' exists; spin-anchoring WinAppDriver.");
                                return SpinAnchorUntilWinAppDriverFindsIt(id);
                            }
                        }
                    }
                }
            }
            finally
            {
                App.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);
                if (total.ElapsedMilliseconds > SlowAttemptMs)
                    PerfLog($"FIND '{id}': done in {total.ElapsedMilliseconds}ms over {iteration + 1} attempt(s)");
            }
        }

        // Once UIA has confirmed the element is in the tree, hammer WinAppDriver with rapid
        // re-anchors until its cached tree catches up. 5s ceiling — if it still can't find it
        // something more fundamental is wrong and the test should fail loudly.
        private AppiumElement SpinAnchorUntilWinAppDriverFindsIt(string id)
        {
            var spinDeadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < spinDeadline)
            {
                try { App.SwitchTo().Window(App.CurrentWindowHandle); }
                catch (Exception ex) { NavLog($"spin re-anchor failed: {ex.Message}"); }
                try
                {
                    App.Manage().Timeouts().ImplicitWait = TimeSpan.FromMilliseconds(200);
                    return App.FindElement(MobileBy.AccessibilityId(id));
                }
                catch (OpenQA.Selenium.NoSuchElementException) { }
            }
            throw new OpenQA.Selenium.NoSuchElementException(
                $"Element '{id}' visible in UIA tree but WinAppDriver could not locate it within 5s of spin-anchoring.");
        }

        // Query the live UIA tree directly via FlaUI/IUIAutomation3, bypassing WinAppDriver's
        // cached snapshot. Swallows all failures and returns false — UIA access is best-effort.
        private bool IsVisibleViaUIA(string automationId)
        {
            try
            {
                string handleStr = App.CurrentWindowHandle;
                IntPtr hwnd = new IntPtr(Convert.ToInt64(
                    handleStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? handleStr : "0x" + handleStr,
                    16));
                AutomationElement window = UIA.FromHandle(hwnd);
                var sw = System.Diagnostics.Stopwatch.StartNew();
                bool found = window.FindFirstDescendant(cf => cf.ByAutomationId(automationId)) != null;
                NavLog($"UIA check '{automationId}': {(found ? "FOUND" : "not found")} in {sw.ElapsedMilliseconds}ms");
                return found;
            }
            catch (Exception ex)
            {
                NavLog($"IsVisibleViaUIA('{automationId}') error: {ex.Message}");
                return false;
            }
        }

        protected override string GetElementText(string automationId) =>
            App.FindElement(MobileBy.AccessibilityId(automationId)).Text;

        protected override AppiumElement FindByDescription(string windowsId, string _) =>
            App.FindElement(MobileBy.AccessibilityId(windowsId));

        // AccessibilityId = AutomationId on Windows — reliable, does not reset after binding cascades.
        protected override bool TryClickIfPresent(string id, int timeoutMs = 2000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            var total = System.Diagnostics.Stopwatch.StartNew();
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    App.FindElement(MobileBy.AccessibilityId(id)).Click();
                    if (total.ElapsedMilliseconds > SlowAttemptMs)
                        PerfLog($"TryClickIfPresent('{id}'): clicked after {total.ElapsedMilliseconds}ms (budget {timeoutMs}ms)");
                    return true;
                }
                catch (OpenQA.Selenium.NoSuchElementException) when (DateTime.UtcNow < deadline) { }
                catch (Exception ex)
                {
                    // A single FindElement can overrun the whole budget: the ambient implicit
                    // wait plus the UIA descendant walk are both charged to one call, so the
                    // deadline check only runs once the call finally returns.
                    PerfLog($"TryClickIfPresent('{id}'): gave up after {total.ElapsedMilliseconds}ms (budget {timeoutMs}ms) — {ex.GetType().Name}");
                    NavLog($"TryClickIfPresent({id}): {ex.GetType().Name}: {ex.Message}");
                    return false;
                }
            }
            if (total.ElapsedMilliseconds > SlowAttemptMs)
                PerfLog($"TryClickIfPresent('{id}'): deadline exit after {total.ElapsedMilliseconds}ms (budget {timeoutMs}ms)");
            return false;
        }

        // Scrolls the element with the given AutomationId into the viewport via FlaUI's
        // IUIAutomationScrollItemPattern, then clicks it via WinAppDriver. Use this instead
        // of FindUIElement(...).Click() whenever the target may be off-screen in a long list
        // (e.g. delete buttons in OptionsPage, rows in ReadJournal after many inserts).
        // If ScrollIntoView is unsupported or fails, the WinAppDriver click still runs — it
        // succeeds when the element was already on-screen, and surfaces the real error otherwise.
        protected override void ScrollIntoViewAndClick(string automationId)
        {
            try
            {
                string handleStr = App.CurrentWindowHandle;
                IntPtr hwnd = new IntPtr(Convert.ToInt64(
                    handleStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                        ? handleStr : "0x" + handleStr, 16));
                AutomationElement window = UIA.FromHandle(hwnd);
                AutomationElement? el = window.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
                if (el is not null && el.Patterns.ScrollItem.IsSupported)
                    el.Patterns.ScrollItem.Pattern.ScrollIntoView();
            }
            catch (Exception ex) { NavLog($"ScrollIntoView({automationId}) failed: {ex.Message}"); }

            App.FindElement(MobileBy.AccessibilityId(automationId)).Click();
        }

        // Click picker, type first letter to select item, Tab to confirm.
        // Re-anchor after close so the UIA root reflects any IsVisible changes that fired
        // while the dropdown was open (e.g. Game3Tab becoming visible via ShowGame3).
        protected override void SelectWindowsPickerItem(AppiumElement pickerElement, string itemName)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            pickerElement.Click();
            long clickMs = sw.ElapsedMilliseconds;
            pickerElement.SendKeys(itemName[0].ToString());
            long letterMs = sw.ElapsedMilliseconds - clickMs;
            pickerElement.SendKeys(OpenQA.Selenium.Keys.Tab);
            long tabMs = sw.ElapsedMilliseconds - clickMs - letterMs;
            try { App.SwitchTo().Window(App.CurrentWindowHandle); }
            catch (Exception ex) { NavLog($"re-anchor after picker failed: {ex.Message}"); }
            PerfLog($"SelectWindowsPickerItem('{itemName}'): click {clickMs}ms, letter {letterMs}ms, tab {tabMs}ms, re-anchor {sw.ElapsedMilliseconds - clickMs - letterMs - tabMs}ms");
        }
    }
}
