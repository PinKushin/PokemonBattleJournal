namespace UITests
{
    public abstract class BaseTest
    {
        private static string? _currentPage;

        private static readonly string NavLogPath = Path.Combine(
            Path.GetTempPath(), "UITests.NavLog.txt");

        private static readonly string PerfLogPath = Path.Combine(
            Path.GetTempPath(), "UITests.PerfLog.txt");

        private System.Diagnostics.Stopwatch _testTimer = new();

        private static void NavLog(string message)
        {
            try { File.AppendAllText(NavLogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}"); }
            catch { }
        }

        private static void PerfLog(string message)
        {
            try { File.AppendAllText(PerfLogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}"); }
            catch { }
        }

        [SetUp]
        public void BaseSetUp()
        {
            _testTimer = System.Diagnostics.Stopwatch.StartNew();
            PerfLog($"START {TestContext.CurrentContext.Test.FullName}");
        }

        [TearDown]
        public void BaseTearDown()
        {
            _testTimer.Stop();
            string result = TestContext.CurrentContext.Result.Outcome.Status.ToString();
            PerfLog($"END   {TestContext.CurrentContext.Test.FullName} [{result}] {_testTimer.ElapsedMilliseconds}ms");
        }

        protected AppiumDriver App => AppiumSetup.App;

        protected AppiumElement FindUIElement(string id)
        {
            if (App is WindowsDriver)
            {
                // Poll every 500ms for up to 30s. WinAppDriver FindElement with
                // short implicit wait catches the element the instant it appears
                // in the UIA tree after a binding cascade or dropdown close.
                var deadline = DateTime.UtcNow.AddSeconds(30);
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
                            // Element not in UIA tree yet — poll again
                        }
                    }
                }
                finally
                {
                    App.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);
                }
            }

            string resourceId = $"com.PinKushin.PokemonBattleJournal:id/{id}";
            // Three-stage lookup:
            // 1. Direct 3s — instant for already-visible elements.
            // 2. Direct 10s — for elements in non-scrollable containers that appear after a binding update.
            // 3. UiScrollable 10s — for elements off-screen that need scrolling.
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
                        $"new UiScrollable(new UiSelector().scrollable(true).packageName(\"com.PinKushin.PokemonBattleJournal\").instance(0))" +
                        $".scrollIntoView(new UiSelector().resourceId(\"{resourceId}\"))"));
                }
            }
            finally
            {
                App.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
            }
        }

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
            var navTimer = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                PerfLog($"NAV   [{caller}] start nav to '{pageTitle}'");
                if (App is WindowsDriver)
                {
                    PerfLog($"NAV   [{caller}] click OK button");
                    App.FindElement(MobileBy.AccessibilityId("OK")).Click();
                    PerfLog($"NAV   [{caller}] click page title '{pageTitle}'");
                    App.FindElement(MobileBy.AccessibilityId(pageTitle)).Click();
                }
                else
                {
                    PerfLog($"NAV   [{caller}] open nav drawer");
                    App.FindElement(MobileBy.AccessibilityId("Open navigation drawer")).Click();
                    PerfLog($"NAV   [{caller}] click page '{pageTitle}' in drawer");
                    App.FindElement(MobileBy.AndroidUIAutomator($"new UiSelector().text(\"{pageTitle}\")")).Click();
                }
                navTimer.Stop();
                _currentPage = pageTitle;
                NavLog($"OK    [{caller}] now on '{pageTitle}' ({navTimer.ElapsedMilliseconds}ms)");
                PerfLog($"NAV   '{pageTitle}' {navTimer.ElapsedMilliseconds}ms");
                PerfLog($"NAV   [{caller}] complete nav to '{pageTitle}' ({navTimer.ElapsedMilliseconds}ms)");
            }
            catch (Exception ex)
            {
                navTimer.Stop();
                NavLog($"FAIL  [{caller}] navigating to '{pageTitle}': {ex.GetType().Name}: {ex.Message.Split('\n')[0]} ({navTimer.ElapsedMilliseconds}ms)");
                _currentPage = null;
                throw;
            }
        }

        protected void InvalidateCurrentPage() => _currentPage = null;

        protected static void SelectWindowsPickerItem(AppiumElement pickerElement, string itemName)
        {
            pickerElement.Click();
            pickerElement.SendKeys(itemName[0].ToString());
            pickerElement.SendKeys(OpenQA.Selenium.Keys.Tab);
        }

        protected static void ClickTab(AppiumElement tabElement)
        {
            // Tab elements are Button controls — WinAppDriver invokes them via UIA InvokePattern,
            // which works on all Windows configurations without pointer simulation.
            tabElement.Click();
        }

        /// <summary>
        /// Attempts to find and click an element within a short deadline.
        /// Returns true if clicked, false if element not found. Never throws.
        /// Does NOT modify the driver's ImplicitWait — safe to call from cleanup helpers.
        /// </summary>
        protected bool TryClickIfPresent(string accessibilityId, int timeoutMs = 2000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    App.FindElement(MobileBy.AccessibilityId(accessibilityId)).Click();
                    return true;
                }
                catch (OpenQA.Selenium.NoSuchElementException) when (DateTime.UtcNow < deadline)
                {
                    // Not in UIA tree yet — retry
                }
                catch { return false; }
            }
            return false;
        }
    }
}
