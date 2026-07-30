namespace UITests
{
    // Add a CollectionDefinition together with a ICollectionFixture
    // to ensure that the setup only runs once
    // xUnit does not have a built-in concept of a fixture that only runs once for the whole test set.
    [CollectionDefinition("UITests")]
    public sealed class UITestsCollectionDefinition : ICollectionFixture<AppiumSetup>
    {

    }

    // Add all tests to the same collection as above so that the Appium server is only setup once
    [Collection("UITests")]
    public abstract class BaseTest
    {
        private static string? _currentPage;

        private static readonly string NavLogPath = Path.Combine(
            Path.GetTempPath(), "UITests.NavLog.txt");

        private static void NavLog(string message)
        {
            try { File.AppendAllText(NavLogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}"); }
            catch { }
        }

        protected AppiumDriver App => AppiumSetup.App;

        protected AppiumElement FindUIElement(string id)
        {
            if (App is WindowsDriver)
                return App.FindElement(MobileBy.AccessibilityId(id));

            string resourceId = $"com.PinKushin.PokemonBattleJournal:id/{id}";
            // Three-stage lookup:
            // 1. Direct 3s — instant for already-visible elements.
            // 2. Direct 10s — for elements in non-scrollable containers that appear after a binding update
            //    (e.g. tabs inside HorizontalStackLayout with IsVisible bound to a toggle).
            //    UiScrollable cannot reach these because their parent is not scrollable.
            // 3. UiScrollable 10s — for elements that are off-screen and need scrolling to reach.
            try
            {
                App.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(3);
                return App.FindElement(MobileBy.AndroidUIAutomator(
                    $"new UiSelector().resourceId(\"{resourceId}\")"));
            }
            catch (OpenQA.Selenium.NoSuchElementException)
            {
                try
                {
                    App.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
                    return App.FindElement(MobileBy.AndroidUIAutomator(
                        $"new UiSelector().resourceId(\"{resourceId}\")"));
                }
                catch (OpenQA.Selenium.NoSuchElementException)
                {
                    App.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
                    // Scope scrollable to app package — prevents UiScrollable grabbing the
                    // notification shade or system scroll views above the app content.
                    return App.FindElement(MobileBy.AndroidUIAutomator(
                        $"new UiScrollable(new UiSelector().scrollable(true).packageName(\"com.PinKushin.PokemonBattleJournal\").instance(0))" +
                        $".scrollIntoView(new UiSelector().resourceId(\"{resourceId}\"))"));
                }
            }
            finally
            {
                App.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
            }
        }

        // MAUI Border and Editor receive content-desc from SemanticProperties.Description
        // on Android (not from AutomationId). Screen readers use content-desc, so use it
        // for automation too. Windows still uses AutomationId via AccessibilityId.
        protected AppiumElement FindByDescription(string windowsId, string androidDescription) =>
            App is WindowsDriver
                ? App.FindElement(MobileBy.AccessibilityId(windowsId))
                : App.FindElement(MobileBy.AndroidUIAutomator(
                    $"new UiScrollable(new UiSelector().scrollable(true).instance(0))" +
                    $".scrollIntoView(new UiSelector().description(\"{androidDescription}\"))"));

        protected void NavigateTo(string pageTitle)
        {
            string caller = new System.Diagnostics.StackTrace().GetFrame(1)?.GetMethod()?.Name ?? "?";
            if (_currentPage == pageTitle)
            {
                NavLog($"SKIP  [{caller}] already on '{pageTitle}'");
                return;
            }

            NavLog($"NAV   [{caller}] '{_currentPage ?? "null"}' -> '{pageTitle}'");
            try
            {
                if (App is WindowsDriver)
                {
                    var menu = App.FindElement(MobileBy.AccessibilityId("OK"));
                    menu.Click();
                    var item = App.FindElement(MobileBy.AccessibilityId(pageTitle));
                    item.Click();
                }
                else
                {
                    var menu = App.FindElement(MobileBy.AccessibilityId("Open navigation drawer"));
                    menu.Click();
                    var item = App.FindElement(MobileBy.AndroidUIAutomator($"new UiSelector().text(\"{pageTitle}\")"));
                    item.Click();
                }
                _currentPage = pageTitle;
                NavLog($"OK    [{caller}] now on '{pageTitle}'");
            }
            catch (Exception ex)
            {
                NavLog($"FAIL  [{caller}] navigating to '{pageTitle}': {ex.GetType().Name}: {ex.Message.Split('\n')[0]}");
                _currentPage = null;
                throw;
            }
        }

        // Call this when a test intentionally navigates away from the tracked page
        // so the next NavigateTo knows it must re-navigate.
        protected void InvalidateCurrentPage() => _currentPage = null;

        // Selects an item in a Windows MAUI Picker/ComboBox by keyboard navigation.
        // Clicks the element to open the dropdown, then uses the first letter to jump
        // to the matching item and Enter to confirm. Split into separate SendKeys calls
        // to avoid the combined-string stall seen on Windows.
        protected static void SelectWindowsPickerItem(AppiumElement pickerElement, string itemName)
        {
            pickerElement.Click();
            pickerElement.SendKeys(itemName[0].ToString());
            pickerElement.SendKeys(OpenQA.Selenium.Keys.Tab);
        }

        // Clicks a MAUI Border tab that uses TapGestureRecognizer.
        // WinAppDriver only supports pen/touch pointer in Actions (not mouse), so we use
        // a touch tap sequence. On Android, plain .Click() works fine via UIAutomator2.
        protected void ClickTab(AppiumElement tabElement)
        {
            if (App is not WindowsDriver)
            {
                tabElement.Click();
                return;
            }

            // WinAppDriver: use touch pointer action (mouse Actions throw UnsupportedOperationException)
            var touch = new OpenQA.Selenium.Appium.Interactions.PointerInputDevice(
                OpenQA.Selenium.Interactions.PointerKind.Touch, "touch");
            var seq = new OpenQA.Selenium.Interactions.ActionSequence(touch, 0);
            seq.AddAction(touch.CreatePointerMove(tabElement, 0, 0, TimeSpan.Zero));
            seq.AddAction(touch.CreatePointerDown(OpenQA.Selenium.Interactions.MouseButton.Left));
            seq.AddAction(touch.CreatePointerUp(OpenQA.Selenium.Interactions.MouseButton.Left));
            App.PerformActions([seq]);
        }
    }
}