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

        // Three-stage resourceId lookup:
        // 1. Direct 3s — instant for already-visible elements.
        // 2. Direct 10s — for elements that appear after a binding update.
        // 3. UiScrollable 10s — for elements off-screen that need scrolling.
        protected override AppiumElement FindUIElement(string id)
        {
            string resourceId = $"{PackageName}:id/{id}";
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
                    return App.FindElement(MobileBy.AndroidUIAutomator(
                        $"new UiScrollable(new UiSelector().scrollable(true).packageName(\"{PackageName}\").instance(0))" +
                        $".scrollIntoView(new UiSelector().resourceId(\"{resourceId}\"))"));
                }
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
