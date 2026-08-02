namespace UITests
{
    public abstract class BaseTest : TestBase
    {
        protected override void DoNavigateTo(string pageTitle)
        {
            PerfLog($"NAV click OK");
            App.FindElement(MobileBy.AccessibilityId("OK")).Click();
            PerfLog($"NAV click '{pageTitle}'");
            App.FindElement(MobileBy.AccessibilityId(pageTitle)).Click();
        }

        // Poll every 500ms for up to 30s. Re-anchor every ~2s so UIA tree updates from
        // IsVisible binding cascades (which fire on the MAUI UI thread asynchronously)
        // are picked up even when they land after the initial tree acquisition.
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
                            try { App.SwitchTo().Window(App.CurrentWindowHandle); }
                            catch (Exception ex) { NavLog($"re-anchor failed: {ex.Message}"); }
                        }
                    }
                }
            }
            finally
            {
                App.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);
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
