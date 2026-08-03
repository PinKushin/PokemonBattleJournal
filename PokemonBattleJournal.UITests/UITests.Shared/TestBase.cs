namespace UITests
{
    /// <summary>
    /// Shared test infrastructure. Platform-specific BaseTest classes inherit this and
    /// implement the abstract driver methods (FindUIElement, NavigateTo, etc.).
    /// </summary>
    public abstract class TestBase
    {
        private static string? _currentPage;

        private static readonly string NavLogPath = Path.Combine(Path.GetTempPath(), "UITests.NavLog.txt");
        private static readonly string PerfLogPath = Path.Combine(Path.GetTempPath(), "UITests.PerfLog.txt");

        private System.Diagnostics.Stopwatch _testTimer = new();

        protected static void NavLog(string message)
        {
            try { File.AppendAllText(NavLogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}"); }
            catch { }
        }

        protected static void PerfLog(string message)
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

        protected void InvalidateCurrentPage() => _currentPage = null;

        protected static void ClickTab(AppiumElement tabElement) => tabElement.Click();

        /// <summary>
        /// Scrolls the element into the viewport via UIA ScrollIntoView (Windows: Actions.MoveToElement;
        /// Android: plain Click — UiScrollable/UiSelector already handles scrolling at the lookup site).
        /// Use this instead of element.Click() whenever the target may be below the visible area.
        /// </summary>
        protected void ScrollToAndClick(AppiumElement element)
        {
            if (App is WindowsDriver)
                new OpenQA.Selenium.Interactions.Actions(App).MoveToElement(element).Click().Perform();
            else
                element.Click();
        }

        // Template method: handles page-tracking and logging; platform provides navigation steps.
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
                DoNavigateTo(pageTitle);
                navTimer.Stop();
                _currentPage = pageTitle;
                NavLog($"OK    [{caller}] now on '{pageTitle}' ({navTimer.ElapsedMilliseconds}ms)");
                PerfLog($"NAV   [{caller}] complete '{pageTitle}' ({navTimer.ElapsedMilliseconds}ms)");
            }
            catch (Exception ex)
            {
                navTimer.Stop();
                NavLog($"FAIL  [{caller}] navigating to '{pageTitle}': {ex.GetType().Name}: {ex.Message.Split('\n')[0]} ({navTimer.ElapsedMilliseconds}ms)");
                _currentPage = null;
                throw;
            }
        }

        protected abstract void DoNavigateTo(string pageTitle);

        protected abstract AppiumElement FindUIElement(string id);

        protected abstract AppiumElement FindByDescription(string windowsId, string androidDescription);

        /// <summary>
        /// Attempts to find and click an element within a short deadline.
        /// Returns true if clicked, false if element not found. Never throws.
        /// </summary>
        protected abstract bool TryClickIfPresent(string id, int timeoutMs = 2000);

        /// <summary>
        /// Windows-only: open picker, type first letter to select item, Tab to confirm.
        /// Override in Windows BaseTest. Android tests never reach this — guarded by
        /// <c>if (App is not WindowsDriver)</c> at the call site.
        /// </summary>
        protected virtual void SelectWindowsPickerItem(AppiumElement pickerElement, string itemName) =>
            throw new PlatformNotSupportedException("SelectWindowsPickerItem is Windows-only.");

        /// <summary>
        /// Spins (no sleep) until the element with <paramref name="automationId"/> disappears
        /// from the accessibility tree or <paramref name="timeoutMs"/> elapses.
        /// Use after closing a popup/modal to sync on its full dismissal before the next interaction.
        /// </summary>
        protected void WaitUntilGone(string automationId, int timeoutMs = 3000)
        {
            // WinAppDriver does not support reading ImplicitWait back — hardcode restore to the
            // 5s value set in AppiumSetup. Setting to zero lets FindElements return immediately.
            App.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
            try
            {
                var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
                while (DateTime.UtcNow < deadline)
                    if (!App.FindElements(MobileBy.AccessibilityId(automationId)).Any())
                        return;
            }
            finally { App.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5); }
        }
    }
}
