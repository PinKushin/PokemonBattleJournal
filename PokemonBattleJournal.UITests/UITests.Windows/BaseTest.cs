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
        protected override AppiumElement FindUIElement(string id)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            int iteration = 0;
            try
            {
                while (true)
                {
                    try
                    {
                        App.Manage().Timeouts().ImplicitWait = TimeSpan.FromMilliseconds(500);
                        return App.FindElement(MobileBy.AccessibilityId(id));
                    }
                    catch (OpenQA.Selenium.NoSuchElementException) when (DateTime.UtcNow < deadline)
                    {
                        if (++iteration % 4 == 0)
                        {
                            // Standard re-anchor for elements that are just slow to appear.
                            try { App.SwitchTo().Window(App.CurrentWindowHandle); }
                            catch (Exception ex) { NavLog($"re-anchor failed: {ex.Message}"); }

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
                bool found = window.FindFirstDescendant(cf => cf.ByAutomationId(automationId)) != null;
                NavLog($"UIA check '{automationId}': {(found ? "FOUND" : "not found")}");
                return found;
            }
            catch (Exception ex)
            {
                NavLog($"IsVisibleViaUIA('{automationId}') error: {ex.Message}");
                return false;
            }
        }

        protected override AppiumElement FindByDescription(string windowsId, string _) =>
            App.FindElement(MobileBy.AccessibilityId(windowsId));

        // AccessibilityId = AutomationId on Windows — reliable, does not reset after binding cascades.
        protected override bool TryClickIfPresent(string id, int timeoutMs = 2000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    App.FindElement(MobileBy.AccessibilityId(id)).Click();
                    return true;
                }
                catch (OpenQA.Selenium.NoSuchElementException) when (DateTime.UtcNow < deadline) { }
                catch (Exception ex)
                {
                    NavLog($"TryClickIfPresent({id}): {ex.GetType().Name}: {ex.Message}");
                    return false;
                }
            }
            return false;
        }

        // Click picker, type first letter to select item, Tab to confirm.
        // Re-anchor after close so the UIA root reflects any IsVisible changes that fired
        // while the dropdown was open (e.g. Game3Tab becoming visible via ShowGame3).
        protected override void SelectWindowsPickerItem(AppiumElement pickerElement, string itemName)
        {
            pickerElement.Click();
            pickerElement.SendKeys(itemName[0].ToString());
            pickerElement.SendKeys(OpenQA.Selenium.Keys.Tab);
            try { App.SwitchTo().Window(App.CurrentWindowHandle); }
            catch (Exception ex) { NavLog($"re-anchor after picker failed: {ex.Message}"); }
        }
    }
}
