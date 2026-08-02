namespace UITests
{
    public abstract class BaseTest : TestBase
    {
        private const string PackageName = "com.PinKushin.PokemonBattleJournal";

        protected override void DoNavigateTo(string pageTitle)
        {
            PerfLog($"NAV open drawer");
            App.FindElement(MobileBy.AccessibilityId("Open navigation drawer")).Click();
            PerfLog($"NAV click '{pageTitle}'");
            App.FindElement(MobileBy.AndroidUIAutomator($"new UiSelector().text(\"{pageTitle}\")")).Click();
        }

        // Poll every 500ms for up to 10s with a direct resourceId lookup.
        // If the element is not found in the viewport after 10s, fall back to UiScrollable
        // which scrolls the page to bring off-screen elements into view.
        protected override AppiumElement FindUIElement(string id)
        {
            string resourceId = $"{PackageName}:id/{id}";
            string directSelector = $"new UiSelector().resourceId(\"{resourceId}\")";
            var deadline = DateTime.UtcNow.AddSeconds(10);
            try
            {
                while (true)
                {
                    try
                    {
                        App.Manage().Timeouts().ImplicitWait = TimeSpan.FromMilliseconds(500);
                        return App.FindElement(MobileBy.AndroidUIAutomator(directSelector));
                    }
                    catch (OpenQA.Selenium.NoSuchElementException) when (DateTime.UtcNow < deadline) { }
                }
            }
            catch (OpenQA.Selenium.NoSuchElementException)
            {
                App.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
                return App.FindElement(MobileBy.AndroidUIAutomator(
                    $"new UiScrollable(new UiSelector().scrollable(true).packageName(\"{PackageName}\").instance(0))" +
                    $".scrollIntoView({directSelector})"));
            }
            finally
            {
                App.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
            }
        }

        protected override AppiumElement FindByDescription(string _, string androidDescription) =>
            App.FindElement(MobileBy.AndroidUIAutomator(
                $"new UiScrollable(new UiSelector().scrollable(true).instance(0))" +
                $".scrollIntoView(new UiSelector().description(\"{androidDescription}\"))"));

        // Uses resourceId — reliable on Android even after IsVisible binding cascades reset content-desc.
        protected override bool TryClickIfPresent(string id, int timeoutMs = 2000)
        {
            string resourceId = $"{PackageName}:id/{id}";
            try
            {
                App.Manage().Timeouts().ImplicitWait = TimeSpan.FromMilliseconds(timeoutMs);
                App.FindElement(MobileBy.AndroidUIAutomator(
                    $"new UiSelector().resourceId(\"{resourceId}\")")).Click();
                return true;
            }
            catch (OpenQA.Selenium.NoSuchElementException) { return false; }
            catch (Exception ex)
            {
                NavLog($"TryClickIfPresent({id}): {ex.GetType().Name}: {ex.Message}");
                return false;
            }
            finally
            {
                App.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
            }
        }
    }
}
