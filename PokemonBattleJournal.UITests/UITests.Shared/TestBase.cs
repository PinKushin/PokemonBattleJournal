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

        // CI runners only surface artifact-uploaded log files after the job finishes (or
        // times out) — no way to see progress mid-hang. Mirroring to the console gives live
        // visibility in the Actions log stream while a run is in progress.
        private static readonly bool IsCi = Environment.GetEnvironmentVariable("CI") == "true";

        private System.Diagnostics.Stopwatch _testTimer = new();

        protected static void NavLog(string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
            if (IsCi) { Console.WriteLine($"[NavLog] {line}"); Console.Out.Flush(); }
            try { File.AppendAllText(NavLogPath, line + Environment.NewLine); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        protected static void PerfLog(string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
            if (IsCi) { Console.WriteLine($"[PerfLog] {line}"); Console.Out.Flush(); }
            try { File.AppendAllText(PerfLogPath, line + Environment.NewLine); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
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
        /// Dismisses the soft keyboard if present. Android-only concern: after SendKeys the
        /// keyboard covers the lower half of the screen, and elements under it never enter
        /// the visible UIA tree — no amount of ScrollView scrolling brings them back while
        /// it's up. No-op on Windows.
        /// </summary>
        protected virtual void DismissKeyboard() { }

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
        /// Waits for a loading-gate sentinel (Busy_*) to leave the tree using the
        /// platform-correct lookup (resource-id on Android, AccessibilityId on Windows).
        /// Call after NavigateTo so element queries run against a settled page instead of
        /// burning the UiAutomator waitForIdle budget mid-load. Returns true if the gate
        /// cleared within the deadline, false on timeout (page may never have shown it —
        /// a gate that never appears also returns true immediately, which is fine: load
        /// already finished before we looked).
        /// </summary>
        protected bool WaitUntilBusyGone(string sentinelId, int timeoutMs = 10000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    if (!IsElementPresent(sentinelId))
                        return true;
                }
                catch (InvalidOperationException)
                {
                    // WinAppDriver phantom-element race mid-rebind — treat as still present, keep polling.
                }
            }
            NavLog($"WaitUntilBusyGone('{sentinelId}') timed out after {timeoutMs}ms");
            return false;
        }

        /// <summary>
        /// Polls until the element is removed from the tree (platform-correct lookup),
        /// treating WinAppDriver's phantom-element race as "still being removed".
        /// While a list rebinds after a row delete, FindElements can return an element
        /// whose ID is null/empty, which surfaces as InvalidOperationException — observed
        /// on CI in OptionsPage delete tests. That is a transient mid-removal state, not
        /// a test failure. Returns true when the element is confirmed gone, false on timeout.
        /// </summary>
        protected bool WaitUntilRemoved(string id, int timeoutMs = 5000)
        {
            App.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
            try
            {
                var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
                while (DateTime.UtcNow < deadline)
                {
                    try
                    {
                        if (!IsElementPresentCore(id))
                            return true;
                    }
                    catch (InvalidOperationException)
                    {
                        // Phantom element mid-removal — keep polling.
                    }
                }
                return false;
            }
            finally { App.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5); }
        }

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
