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
        protected AppiumDriver App
        {
            get
            {
                return AppiumSetup.App;
            }
        }

        // This could also be an extension method to AppiumDriver if you prefer
        protected AppiumElement FindUIElement(string id)
        {
            if (App is WindowsDriver)
            {
                return App.FindElement(MobileBy.AccessibilityId(id));
            }

            return App.FindElement(MobileBy.AndroidUIAutomator(
                $"new UiScrollable(new UiSelector().scrollable(true).instance(0))" +
                $".scrollIntoView(new UiSelector().resourceId(\"com.PinKushin.PokemonBattleJournal:id/{id}\"))"));
        }

        protected void NavigateTo(string pageTitle)
        {
            if (App is WindowsDriver)
            {
                // MAUI Shell on WinUI3 renders a NavigationView; the pane toggle has AutomationId="OK"
                var menu = App.FindElement(MobileBy.AccessibilityId("OK"));
                menu.Click();
                Thread.Sleep(500);
                // Custom FlyoutContent nav items have AutomationId matching the page title
                var item = App.FindElement(MobileBy.AccessibilityId(pageTitle));
                item.Click();
            }
            else
            {
                var menu = App.FindElement(MobileBy.AccessibilityId("Open navigation drawer"));
                menu.Click();
                Thread.Sleep(500);
                var item = App.FindElement(MobileBy.AndroidUIAutomator($"new UiSelector().text(\"{pageTitle}\")"));
                item.Click();
            }
            Thread.Sleep(500);
        }


    }
}