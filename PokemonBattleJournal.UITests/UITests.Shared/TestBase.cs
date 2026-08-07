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

        /// <summary>
        /// The ambient ImplicitWait every helper restores when it finishes. Single source of
        /// truth for both platforms: shared TestBase helpers used to hardcode the Windows 5s
        /// while Android's own helpers restored 10s, so the effective ambient on Android
        /// silently flipped depending on which helper ran last. 5s is deliberate — a desktop
        /// or emulator page renders effectively instantly, so a lookup still pending after
        /// 5s is a genuine failure, not a slow render, and every second beyond that is paid
        /// in full by every element that is legitimately absent.
        /// </summary>
        protected static readonly TimeSpan AmbientImplicitWait = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Runs <paramref name="action"/> with ImplicitWait pinned to <paramref name="wait"/>,
        /// then restores the ambient value. Use with TimeSpan.Zero for optional-element
        /// lookups — an absent element otherwise costs the full ambient wait plus the
        /// platform's tree walk (~6.8s on Windows), which is charged on every single run.
        /// </summary>
        protected void WithImplicitWait(TimeSpan wait, Action action)
        {
            App.Manage().Timeouts().ImplicitWait = wait;
            try { action(); }
            finally { App.Manage().Timeouts().ImplicitWait = AmbientImplicitWait; }
        }

        protected AppiumDriver App => AppiumSetup.App;

        protected void InvalidateCurrentPage() => _currentPage = null;

        /// <summary>
        /// Clicks a tab and verifies the switch actually happened, retrying if it did not.
        /// </summary>
        /// <param name="tabAutomationId">AutomationId of the tab to click.</param>
        /// <param name="expectedPanelElementId">
        /// An element that exists ONLY when the target panel is selected. Must be inside the
        /// viewport — UiAutomator exposes only on-screen elements, so verifying against
        /// something below the fold loops forever even when the click worked
        /// (see feedback_android_flaky_tap_retry).
        /// </param>
        /// <remarks>
        /// Was a bare Click() that assumed success. On CI run 31037214916 the Game2Tab click
        /// round-trip took 1231ms and 1790ms (locally it is always under 750ms) and the Game 2
        /// panel never appeared — FlaUI confirmed the absence directly in ~20ms, so the element
        /// genuinely was not in the tree rather than merely being slow to find. The test then
        /// carried on against a page that had not switched, failing 38s later and leaving state
        /// dirty enough to cascade into four more failures.
        ///
        /// This is the same click-verify-retry pattern that took Android MainPage from 7-10
        /// failures to 25/25; Windows never got it because it never visibly needed it.
        /// </remarks>
        protected void ClickTab(string tabAutomationId, string expectedPanelElementId, int attempts = 3)
        {
            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                // Re-find each attempt: a partial state change can re-render the tab and stale
                // the previous handle.
                var sw = System.Diagnostics.Stopwatch.StartNew();
                ClickElement(tabAutomationId);
                long clickMs = sw.ElapsedMilliseconds;

                var deadline = DateTime.UtcNow.AddMilliseconds(3000);
                while (DateTime.UtcNow < deadline)
                {
                    if (IsElementPresent(expectedPanelElementId))
                    {
                        if (clickMs > 750 || attempt > 1)
                            PerfLog($"ClickTab('{tabAutomationId}'): panel '{expectedPanelElementId}' shown on attempt {attempt} (click {clickMs}ms)");
                        return;
                    }
                }

                PerfLog($"ClickTab('{tabAutomationId}'): attempt {attempt} — click took {clickMs}ms but '{expectedPanelElementId}' never appeared, retrying");
            }

            // Clicks are being dispatched and going nowhere. See
            // docs/memory/project_windows_mainpage_click_flake.md — on Windows CI this is where
            // the fixture stops responding to input while every element stays findable.
            LogInputBlockers($"ClickTab({tabAutomationId})", tabAutomationId);

            throw new OpenQA.Selenium.NoSuchElementException(
                $"ClickTab: '{expectedPanelElementId}' never appeared after {attempts} clicks on '{tabAutomationId}'. " +
                "The tab click is not reaching the handler — see PerfLog for per-attempt click timings, " +
                "and for BLOCKERS lines naming what owned input at the time.");
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
        /// <summary>
        /// Activates the control with the given AutomationId.
        /// </summary>
        /// <remarks>
        /// A seam, not a convenience. The default synthesizes a real mouse click at the
        /// element's centre, and WinAppDriver reports that centre in SCREEN coordinates — so an
        /// element laid out below the window is "clicked" at a screen position belonging to
        /// something else entirely. Observed 2026-08-06 at CI's 754x512 window: clicks landed on
        /// the taskbar and launched Visual Studio. On CI nothing sits behind the app, so the
        /// same click hits empty desktop and silently does nothing — the open MainPage flake.
        ///
        /// The Windows override routes through UIA's InvokePattern instead, which carries no
        /// coordinates at all. See docs/memory/project_windows_mainpage_click_flake.md.
        /// </remarks>
        protected virtual void ClickElement(string automationId) => FindUIElement(automationId).Click();

        /// <summary>
        /// Activates an already-resolved element, going through <see cref="ClickElement(string)"/>
        /// when it carries an AutomationId.
        /// </summary>
        /// <remarks>
        /// Exists so call sites holding an element rather than an id — list rows, pickers, an
        /// entry being focused — cannot bypass the off-window guard. Without it those sites keep
        /// dispatching raw mouse input at screen coordinates, which is how this suite launched
        /// other applications, and "I did not see it happen" is not evidence that it did not.
        /// </remarks>
        protected void ClickElement(AppiumElement element)
        {
            string? id = null;
            try { id = element.GetAttribute("AutomationId"); }
            catch (OpenQA.Selenium.WebDriverException) { /* attribute unavailable on this platform */ }

            if (!string.IsNullOrEmpty(id))
                ClickElement(id);
            else
                element.Click();
        }

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
        /// Diagnostic for the case where clicks stop landing but elements are still findable.
        /// Windows BaseTest implements it; other platforms are a no-op.
        /// </summary>
        /// <remarks>
        /// Call this when a click-with-retry helper gives up. The failure it exists for looks
        /// nothing like a missing element: the driver reports the click succeeded, the target
        /// element is present and returns sensible bounds, and yet nothing happens — and from
        /// that point on nothing in the fixture that requires a click works again, while every
        /// find-only test keeps passing. See
        /// docs/memory/project_windows_mainpage_click_flake.md.
        ///
        /// That shape says something is swallowing input above the app: a leftover popup or a
        /// second top-level window. Neither shows up in an element dump, which is why
        /// DumpVisibleElements has never explained this one.
        /// </remarks>
        protected virtual void LogInputBlockers(string context, string? targetAutomationId = null) { }

        /// <summary>
        /// Polls via platform-aware single-shot lookup until the element shows <paramref name="expected"/> text
        /// or <paramref name="timeoutMs"/> elapses. Uses a 200ms implicit wait per iteration.
        /// Safer than WebDriverWait + MobileBy.AccessibilityId, which maps to content-desc on Android
        /// (not AutomationId/resource-id) and silently never finds elements that only have AutomationId set.
        /// </summary>
        /// <returns>
        /// True if the text was observed before the deadline. Callers that treat a timeout as
        /// benign may ignore it, but a caller deciding whether an interaction actually landed
        /// must check it — this used to return void, so a timeout was indistinguishable from
        /// success and a click that never reached its handler looked like a click that worked.
        /// </returns>
        protected bool WaitUntilText(string automationId, string expected, int timeoutMs = 5000)
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
                            return true;
                    }
                    catch (OpenQA.Selenium.NoSuchElementException) { }
                }
            }
            finally { App.Manage().Timeouts().ImplicitWait = AmbientImplicitWait; }

            return false;
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
            finally { App.Manage().Timeouts().ImplicitWait = AmbientImplicitWait; }
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
            finally { App.Manage().Timeouts().ImplicitWait = AmbientImplicitWait; }
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
            finally { App.Manage().Timeouts().ImplicitWait = AmbientImplicitWait; }
        }
    }
}
