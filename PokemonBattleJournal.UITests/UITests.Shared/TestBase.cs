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
        /// Finds the element, scrolls it into the visible viewport if needed, then clicks.
        /// Windows: FlaUI ScrollItemPattern brings it on-screen first so WinAppDriver coordinates are valid.
        /// Android: UiScrollable/UiSelector already handles scrolling at lookup time — plain Click suffices.
        /// </summary>
        protected abstract void ScrollIntoViewAndClick(string automationId);

        /// <summary>
        /// Windows-only: open picker, type first letter to select item, Tab to confirm.
        /// Override in Windows BaseTest. Android tests never reach this — guarded by
        /// <c>if (App is not WindowsDriver)</c> at the call site.
        /// </summary>
        protected virtual void SelectWindowsPickerItem(AppiumElement pickerElement, string itemName) =>
            throw new PlatformNotSupportedException("SelectWindowsPickerItem is Windows-only.");

        /// <summary>
        /// Android-only escape hatch: fires the Android BACK keyevent via adb to dismiss
        /// a popup/dialog when the in-app Cancel button is unreachable. No-op on Windows.
        /// </summary>
        protected virtual void SendAndroidBack() { }

        /// <summary>
        /// Diagnostic: dumps every visible element's resource-id / content-desc / className / text
        /// to PerfLog with the given context label. Android BaseTest implements this; other
        /// platforms are no-op. Use inside a catch block or after an unexpected failure to see
        /// what IS in the UIA tree so you can tell whether the id is wrong, the wrong window is
        /// active, or the popup is already gone.
        /// </summary>
        protected virtual void DumpVisibleElements(string context, int maxItems = 40) { }

        /// <summary>
        /// Polls via platform-aware single-shot lookup until the element shows <paramref name="expected"/> text
        /// or <paramref name="timeoutMs"/> elapses. Uses a 200ms implicit wait per iteration.
        /// Safer than WebDriverWait + MobileBy.AccessibilityId, which maps to content-desc on Android
        /// (not AutomationId/resource-id) and silently never finds elements that only have AutomationId set.
        /// </summary>
        protected void WaitUntilText(string automationId, string expected, int timeoutMs = 5000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            App.Manage().Timeouts().ImplicitWait = TimeSpan.FromMilliseconds(200);
            try
            {
                while (DateTime.UtcNow < deadline)
                {
                    try
                    {
                        if (GetElementText(automationId) == expected)
                            return;
                    }
                    catch (OpenQA.Selenium.NoSuchElementException) { }
                }
            }
            finally { App.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5); }
        }

        /// <summary>
        /// Single-shot element text read using the platform's native selector (resource-id on Android,
        /// AccessibilityId on Windows). ImplicitWait must be set by the caller.
        /// </summary>
        protected abstract string GetElementText(string automationId);

        /// <summary>
        /// Returns true if the element is currently present in the accessibility tree.
        /// Uses platform-correct lookup (resource-id on Android, AccessibilityId on Windows).
        /// Always runs with ImplicitWait = 0 and restores it on exit.
        /// </summary>
        protected bool IsElementPresent(string id)
        {
            App.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
            try { return IsElementPresentCore(id); }
            finally { App.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5); }
        }

        /// <summary>
        /// Platform-specific presence check with ImplicitWait already set to zero by caller.
        /// Windows default uses AccessibilityId. Android BaseTest overrides with resourceId.
        /// </summary>
        protected virtual bool IsElementPresentCore(string id) =>
            App.FindElements(MobileBy.AccessibilityId(id)).Count > 0;

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
