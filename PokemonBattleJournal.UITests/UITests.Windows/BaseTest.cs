using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace UITests
{
    public abstract class BaseTest : TestBase
    {
        // Lazily created; shared across all tests in the session. Not disposed — process exits after suite.
        private static UIA3Automation? _uia;
        private static UIA3Automation UIA => _uia ??= new UIA3Automation();

        // Timing log threshold. A hit on a warm tree costs ~150-250ms; anything above this is
        // the expensive full-descendant-walk case worth attributing, so the threshold keeps
        // the log readable instead of one line per lookup.
        private const int SlowAttemptMs = 750;

        protected override void DoNavigateTo(string pageTitle)
        {
            PerfLog($"NAV click OK");
            App.FindElement(MobileBy.AccessibilityId("OK")).Click();
            PerfLog($"NAV click '{pageTitle}'");
            App.FindElement(MobileBy.AccessibilityId(pageTitle)).Click();
        }

        // Poll every 500ms for up to 30s using WinAppDriver.
        // Every ~2s, also query the UIA tree directly via FlaUI — bypasses WinAppDriver's cached
        // element tree so MAUI IsVisible binding cascades (async on the UI thread) are detected
        // reliably. Once FlaUI confirms the element exists, spin-re-anchor WinAppDriver until it
        // picks it up too, then return the Appium handle for downstream test interactions.
        protected override AppiumElement FindUIElement(string id)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            var total = System.Diagnostics.Stopwatch.StartNew();
            int iteration = 0;
            try
            {
                while (true)
                {
                    var attempt = System.Diagnostics.Stopwatch.StartNew();
                    try
                    {
                        App.Manage().Timeouts().ImplicitWait = TimeSpan.FromMilliseconds(500);
                        AppiumElement found = App.FindElement(MobileBy.AccessibilityId(id));
                        if (attempt.ElapsedMilliseconds > SlowAttemptMs)
                            PerfLog($"FIND '{id}': HIT on attempt {iteration + 1} after {attempt.ElapsedMilliseconds}ms (total {total.ElapsedMilliseconds}ms)");
                        return found;
                    }
                    catch (OpenQA.Selenium.NoSuchElementException) when (DateTime.UtcNow < deadline)
                    {
                        if (attempt.ElapsedMilliseconds > SlowAttemptMs)
                            PerfLog($"FIND '{id}': MISS on attempt {iteration + 1} after {attempt.ElapsedMilliseconds}ms (implicit wait 500ms — remainder is the UIA tree walk)");

                        if (++iteration % 4 == 0)
                        {
                            // Standard re-anchor for elements that are just slow to appear.
                            var anchor = System.Diagnostics.Stopwatch.StartNew();
                            try { App.SwitchTo().Window(App.CurrentWindowHandle); }
                            catch (Exception ex) { NavLog($"re-anchor failed: {ex.Message}"); }
                            PerfLog($"FIND '{id}': re-anchor took {anchor.ElapsedMilliseconds}ms (iteration {iteration})");

                            // If WinAppDriver keeps missing it, check the live UIA tree directly.
                            if (IsVisibleViaUIA(id))
                            {
                                NavLog($"UIA confirmed '{id}' exists; spin-anchoring WinAppDriver.");
                                return SpinAnchorUntilWinAppDriverFindsIt(id);
                            }
                        }
                    }
                }
            }
            finally
            {
                App.Manage().Timeouts().ImplicitWait = AmbientImplicitWait;
                if (total.ElapsedMilliseconds > SlowAttemptMs)
                    PerfLog($"FIND '{id}': done in {total.ElapsedMilliseconds}ms over {iteration + 1} attempt(s)");
            }
        }

        // Once UIA has confirmed the element is in the tree, hammer WinAppDriver with rapid
        // re-anchors until its cached tree catches up. 5s ceiling — if it still can't find it
        // something more fundamental is wrong and the test should fail loudly.
        private AppiumElement SpinAnchorUntilWinAppDriverFindsIt(string id)
        {
            var spinDeadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < spinDeadline)
            {
                try { App.SwitchTo().Window(App.CurrentWindowHandle); }
                catch (Exception ex) { NavLog($"spin re-anchor failed: {ex.Message}"); }
                try
                {
                    App.Manage().Timeouts().ImplicitWait = TimeSpan.FromMilliseconds(200);
                    return App.FindElement(MobileBy.AccessibilityId(id));
                }
                catch (OpenQA.Selenium.NoSuchElementException) { }
            }
            throw new OpenQA.Selenium.NoSuchElementException(
                $"Element '{id}' visible in UIA tree but WinAppDriver could not locate it within 5s of spin-anchoring.");
        }

        // Query the live UIA tree directly via FlaUI/IUIAutomation3, bypassing WinAppDriver's
        // cached snapshot. Swallows all failures and returns false — UIA access is best-effort.
        private bool IsVisibleViaUIA(string automationId)
        {
            try
            {
                string handleStr = App.CurrentWindowHandle;
                IntPtr hwnd = new IntPtr(Convert.ToInt64(
                    handleStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? handleStr : "0x" + handleStr,
                    16));
                AutomationElement window = UIA.FromHandle(hwnd);
                var sw = System.Diagnostics.Stopwatch.StartNew();
                bool found = window.FindFirstDescendant(cf => cf.ByAutomationId(automationId)) != null;
                NavLog($"UIA check '{automationId}': {(found ? "FOUND" : "not found")} in {sw.ElapsedMilliseconds}ms");
                return found;
            }
            catch (Exception ex)
            {
                NavLog($"IsVisibleViaUIA('{automationId}') error: {ex.Message}");
                return false;
            }
        }

        protected override string GetElementText(string automationId) =>
            App.FindElement(MobileBy.AccessibilityId(automationId)).Text;

        protected override AppiumElement FindByDescription(string windowsId, string _) =>
            App.FindElement(MobileBy.AccessibilityId(windowsId));

        // AccessibilityId = AutomationId on Windows — reliable, does not reset after binding cascades.
        // The element is optional by contract, so timeoutMs is pinned as the implicit wait rather
        // than left at the ambient value. Previously this inherited the 5s ambient and a single
        // FindElement consumed 6.7s against a 2000ms budget — the deadline check only runs once
        // the call returns, so the loop could not enforce its own limit. Android's override has
        // always done it this way; this brings Windows in line.
        protected override bool TryClickIfPresent(string id, int timeoutMs = 2000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            var total = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                App.Manage().Timeouts().ImplicitWait = TimeSpan.FromMilliseconds(timeoutMs);
                while (DateTime.UtcNow < deadline)
                {
                    try
                    {
                        App.FindElement(MobileBy.AccessibilityId(id)).Click();
                        if (total.ElapsedMilliseconds > SlowAttemptMs)
                            PerfLog($"TryClickIfPresent('{id}'): clicked after {total.ElapsedMilliseconds}ms (budget {timeoutMs}ms)");
                        return true;
                    }
                    catch (OpenQA.Selenium.NoSuchElementException) when (DateTime.UtcNow < deadline) { }
                    catch (Exception ex)
                    {
                        PerfLog($"TryClickIfPresent('{id}'): gave up after {total.ElapsedMilliseconds}ms (budget {timeoutMs}ms) — {ex.GetType().Name}");
                        NavLog($"TryClickIfPresent({id}): {ex.GetType().Name}: {ex.Message}");
                        return false;
                    }
                }
                if (total.ElapsedMilliseconds > SlowAttemptMs)
                    PerfLog($"TryClickIfPresent('{id}'): deadline exit after {total.ElapsedMilliseconds}ms (budget {timeoutMs}ms)");
                return false;
            }
            finally { App.Manage().Timeouts().ImplicitWait = AmbientImplicitWait; }
        }

        // Scrolls the element with the given AutomationId into the viewport via FlaUI's
        // IUIAutomationScrollItemPattern, then clicks it via WinAppDriver. Use this instead
        // of FindUIElement(...).Click() whenever the target may be off-screen in a long list
        // (e.g. delete buttons in OptionsPage, rows in ReadJournal after many inserts).
        // If ScrollIntoView is unsupported or fails, the WinAppDriver click still runs — it
        // succeeds when the element was already on-screen, and surfaces the real error otherwise.
        protected override void ScrollIntoViewAndClick(string automationId)
        {
            try
            {
                string handleStr = App.CurrentWindowHandle;
                IntPtr hwnd = new IntPtr(Convert.ToInt64(
                    handleStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                        ? handleStr : "0x" + handleStr, 16));
                AutomationElement window = UIA.FromHandle(hwnd);
                AutomationElement? el = window.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
                if (el is not null && el.Patterns.ScrollItem.IsSupported)
                    el.Patterns.ScrollItem.Pattern.ScrollIntoView();
            }
            catch (Exception ex) { NavLog($"ScrollIntoView({automationId}) failed: {ex.Message}"); }

            App.FindElement(MobileBy.AccessibilityId(automationId)).Click();
        }

        // Click picker, type first letter to select item, Tab to confirm.
        // Re-anchor after close so the UIA root reflects any IsVisible changes that fired
        // while the dropdown was open (e.g. Game3Tab becoming visible via ShowGame3).
        protected override void SelectWindowsPickerItem(AppiumElement pickerElement, string itemName)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            pickerElement.Click();
            long clickMs = sw.ElapsedMilliseconds;
            pickerElement.SendKeys(itemName[0].ToString());
            long letterMs = sw.ElapsedMilliseconds - clickMs;
            pickerElement.SendKeys(OpenQA.Selenium.Keys.Tab);
            long tabMs = sw.ElapsedMilliseconds - clickMs - letterMs;
            try { App.SwitchTo().Window(App.CurrentWindowHandle); }
            catch (Exception ex) { NavLog($"re-anchor after picker failed: {ex.Message}"); }
            PerfLog($"SelectWindowsPickerItem('{itemName}'): click {clickMs}ms, letter {letterMs}ms, tab {tabMs}ms, re-anchor {sw.ElapsedMilliseconds - clickMs - letterMs - tabMs}ms");
        }

        /// <summary>
        /// Records what could be swallowing input when a click-with-retry helper gives up.
        /// </summary>
        /// <remarks>
        /// Targets docs/memory/project_windows_mainpage_click_flake.md: partway through
        /// MainPageTests on CI, every click-driven test starts failing while every find-only
        /// test keeps passing. The driver reports each click as dispatched (~1000ms), the
        /// target is present, and nothing happens. An element dump cannot explain that — the
        /// element is right there — so the interesting state is *above* the app: another
        /// top-level window, or a leftover popup holding focus.
        ///
        /// Three facts, cheap to collect, that between them distinguish the candidates:
        ///
        /// - **Window count.** More than one top-level window means a popup or dialog is up.
        ///   This only READS WindowHandles. It does not switch to them, and it is not a
        ///   revival of the deleted WindowHandles-iterating picker selection — see
        ///   docs/memory/project_windows_picker_ci.md, which forbids reinstating that
        ///   *selection mechanism*. Reading a count in a failure path is not that mechanism,
        ///   and switching windows here would mutate driver state mid-test, which is exactly
        ///   what made the old approach fragile.
        /// - **Focused element.** A modal popup holding focus explains clicks going nowhere.
        /// - **Whether the archetype popup is still open.** MainPage_ArchetypePicker_Search_
        ///   FiltersResults opens one earlier in the run, and a popup left open would swallow
        ///   clicks while leaving the page underneath perfectly findable — which matches the
        ///   fingerprint exactly. This is the leading hypothesis.
        ///
        /// Best-effort throughout: this runs while something is already wrong, and the
        /// original failure is the one worth reporting.
        /// </remarks>
        protected override void LogInputBlockers(string context)
        {
            try
            {
                System.Collections.ObjectModel.ReadOnlyCollection<string> handles = App.WindowHandles;
                PerfLog($"BLOCKERS [{context}] top-level windows: {handles.Count} (current '{App.CurrentWindowHandle}')");
                if (handles.Count > 1)
                {
                    PerfLog($"BLOCKERS [{context}] MORE THAN ONE WINDOW — a popup or dialog is up and is the likely input sink. Handles: {string.Join(", ", handles)}");
                }
            }
            catch (Exception ex)
            {
                PerfLog($"BLOCKERS [{context}] could not read window handles: {ex.GetType().Name}");
            }

            try
            {
                AppiumElement focused = (AppiumElement)App.SwitchTo().ActiveElement();
                PerfLog($"BLOCKERS [{context}] focused element: AutomationId='{focused.GetDomAttribute("AutomationId")}' Name='{focused.GetDomAttribute("Name")}' ClassName='{focused.GetDomAttribute("ClassName")}'");
            }
            catch (Exception ex)
            {
                PerfLog($"BLOCKERS [{context}] could not read the focused element: {ex.GetType().Name}");
            }

            // The archetype popup is the leading suspect, so name it explicitly rather than
            // leaving a future reader to infer it from a generic dump.
            foreach (string popupMarker in new[] { "ArchetypeSearchBar", "ArchetypeItem_Other", "ArchetypePopupCancel" })
            {
                try
                {
                    if (IsElementPresent(popupMarker))
                    {
                        PerfLog($"BLOCKERS [{context}] '{popupMarker}' IS PRESENT — an archetype popup is still open and is almost certainly eating the clicks");
                    }
                }
                catch (Exception ex)
                {
                    PerfLog($"BLOCKERS [{context}] could not probe '{popupMarker}': {ex.GetType().Name}");
                }
            }
        }
    }
}
