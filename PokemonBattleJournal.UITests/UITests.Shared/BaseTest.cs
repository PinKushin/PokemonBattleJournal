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
        // Tracks which Shell page the app is currently on so NavigateTo can skip
        // redundant flyout navigation within and across test classes.
        private static string? _currentPage;

        protected AppiumDriver App => AppiumSetup.App;

        protected AppiumElement FindUIElement(string id)
        {
            if (App is WindowsDriver)
                return App.FindElement(MobileBy.AccessibilityId(id));

            return App.FindElement(MobileBy.AndroidUIAutomator(
                $"new UiScrollable(new UiSelector().scrollable(true).instance(0))" +
                $".scrollIntoView(new UiSelector().resourceId(\"com.PinKushin.PokemonBattleJournal:id/{id}\"))"));
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
            if (_currentPage == pageTitle)
                return; // already on this page — skip flyout open/close round-trip

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
            }
            catch
            {
                _currentPage = null; // navigation failed — force re-attempt on next call
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
    }
}