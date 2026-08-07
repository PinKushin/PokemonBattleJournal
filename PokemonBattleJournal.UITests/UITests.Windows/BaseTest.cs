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
            ClickElement("OK");
            PerfLog($"NAV click '{pageTitle}'");
            ClickElement(pageTitle);
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
                        ClickElement(id);
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

        /// <summary>
        /// Activates a control through UIA patterns instead of a synthesized mouse click.
        /// </summary>
        /// <remarks>
        /// <para>The pattern ladder, in order: scroll into view, then Invoke, then Toggle, then
        /// SelectionItem, and only then a real WinAppDriver click. Each pattern step carries no
        /// screen coordinates, so window bounds cannot make it miss.</para>
        ///
        /// <para><b>Why.</b> WinAppDriver's click is mouse input at the element's centre,
        /// reported in SCREEN coordinates. An element MAUI has laid out below the window bottom
        /// is therefore "clicked" wherever that lands — at CI's 754x512 window this hit the
        /// taskbar and launched Visual Studio. On CI nothing sits behind the app, so the same
        /// click reaches empty desktop and does nothing at all: dispatched, ~1000ms, no handler.
        /// That is the open MainPage flake's exact signature.</para>
        ///
        /// <para>The mouse fallback stays because patterns cannot cover everything — a MAUI
        /// <c>Border</c> with a <c>TapGestureRecognizer</c> exposes no InvokePattern at all,
        /// which is why the tabs had to become Buttons (project_windows_tab_click_ci). When it
        /// is used, the element's rect is checked against the window first and a miss is logged
        /// loudly rather than dispatched into another application.</para>
        /// </remarks>
        protected override void ClickElement(string automationId)
        {
            AutomationElement? el = TryFindViaUia(automationId);

            if (el is not null)
            {
                try
                {
                    if (el.Patterns.ScrollItem.IsSupported)
                        el.Patterns.ScrollItem.Pattern.ScrollIntoView();

                    if (el.Patterns.Invoke.IsSupported)
                    {
                        el.Patterns.Invoke.Pattern.Invoke();
                        PerfLog($"ClickElement('{automationId}'): UIA Invoke");
                        return;
                    }

                    if (el.Patterns.Toggle.IsSupported)
                    {
                        el.Patterns.Toggle.Pattern.Toggle();
                        PerfLog($"ClickElement('{automationId}'): UIA Toggle");
                        return;
                    }

                    if (el.Patterns.SelectionItem.IsSupported)
                    {
                        el.Patterns.SelectionItem.Pattern.Select();
                        PerfLog($"ClickElement('{automationId}'): UIA Select");
                        return;
                    }

                    // A MAUI Picker becomes a WinUI ComboBox, which exposes ExpandCollapse and
                    // none of the three above. Focus first: the picker helpers type the item's
                    // first letter straight after opening, and an expand that leaves focus
                    // elsewhere sends those keystrokes nowhere.
                    if (el.Patterns.ExpandCollapse.IsSupported)
                    {
                        el.Focus();
                        el.Patterns.ExpandCollapse.Pattern.Expand();
                        PerfLog($"ClickElement('{automationId}'): UIA Expand");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    // Fall through to the mouse path rather than failing the test here: a
                    // pattern can be advertised and still throw (element re-rendered mid-call).
                    PerfLog($"ClickElement('{automationId}'): UIA pattern failed ({ex.GetType().Name}: {ex.Message}) — falling back to mouse");
                }
            }

            // Refuse rather than warn. A mouse click on an off-window element is never correct:
            // on CI it lands on empty desktop and does nothing (the flake), and locally it goes
            // to whatever application occupies that screen position — this suite has launched
            // Visual Studio and the Epic Games store that way. A loud failure is strictly better
            // than input sent to somebody else's window.
            if (IsOutsideWindow(automationId, out string geometry))
            {
                throw new OpenQA.Selenium.ElementNotInteractableException(
                    $"'{automationId}' is outside the app window and has no UIA pattern to invoke, " +
                    $"so a click would land on another application. {geometry}");
            }

            FindUIElement(automationId).Click();
            PerfLog($"ClickElement('{automationId}'): mouse click (no usable UIA pattern)");
        }

        /// <summary>
        /// Windows: resolve the element's AutomationId and go through the guarded path so
        /// element-based call sites cannot bypass the off-window check.
        /// </summary>
        protected override void ClickElement(AppiumElement element)
        {
            string? id = null;
            try { id = element.GetAttribute("AutomationId"); }
            catch (OpenQA.Selenium.WebDriverException) { /* attribute unavailable — click directly */ }

            if (!string.IsNullOrEmpty(id))
                ClickElement(id);
            else
                element.Click();
        }

        /// <summary>Finds an element in the live UIA tree, or null. Best-effort.</summary>
        private AutomationElement? TryFindViaUia(string automationId)
        {
            try
            {
                string handleStr = App.CurrentWindowHandle;
                IntPtr hwnd = new IntPtr(Convert.ToInt64(
                    handleStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? handleStr : "0x" + handleStr, 16));
                return UIA.FromHandle(hwnd).FindFirstDescendant(cf => cf.ByAutomationId(automationId));
            }
            catch (Exception ex)
            {
                NavLog($"TryFindViaUia('{automationId}') error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Logs loudly when a about-to-be-clicked element sits outside the window.
        /// </summary>
        /// <remarks>
        /// Does not throw. A mouse click on an off-window element is how this suite launched
        /// other applications, but turning that into a hard failure today would convert an
        /// intermittent flake into a hard stop before the pattern ladder has been proven across
        /// every call site. The log line is what makes it findable in the meantime.
        /// </remarks>
        private bool IsOutsideWindow(string automationId, out string geometry)
        {
            geometry = string.Empty;
            try
            {
                AppiumElement target = FindUIElement(automationId);
                System.Drawing.Point origin = target.Location;
                System.Drawing.Size size = target.Size;
                System.Drawing.Point windowOrigin = App.Manage().Window.Position;
                System.Drawing.Size windowSize = App.Manage().Window.Size;

                bool inside =
                    origin.Y >= windowOrigin.Y &&
                    origin.Y + size.Height <= windowOrigin.Y + windowSize.Height &&
                    origin.X >= windowOrigin.X &&
                    origin.X + size.Width <= windowOrigin.X + windowSize.Width;

                geometry = $"Element at ({origin.X},{origin.Y}) {size.Width}x{size.Height}; " +
                    $"window at ({windowOrigin.X},{windowOrigin.Y}) {windowSize.Width}x{windowSize.Height}.";

                if (!inside)
                    PerfLog($"ClickElement('{automationId}'): TARGET OUTSIDE WINDOW — {geometry}");

                return !inside;
            }
            catch (Exception ex)
            {
                // Cannot tell — allow the click. Blocking on a failed measurement would turn a
                // diagnostic into an outage.
                PerfLog($"ClickElement('{automationId}'): could not check window bounds ({ex.GetType().Name})");
                return false;
            }
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

            // Through the guarded path: ScrollIntoView above may still leave the target
            // off-window in a container UIA cannot scroll, and a raw click there lands in
            // another application.
            ClickElement(automationId);
        }

        // Click picker, type first letter to select item, Tab to confirm.
        // Re-anchor after close so the UIA root reflects any IsVisible changes that fired
        // while the dropdown was open (e.g. Game3Tab becoming visible via ShowGame3).
        protected override void SelectWindowsPickerItem(AppiumElement pickerElement, string itemName)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            // Route through the UIA path when the element carries an AutomationId — this is the
            // last Windows click site that could otherwise fire a raw mouse event at an
            // off-window coordinate.
            string? pickerId = null;
            try { pickerId = pickerElement.GetAttribute("AutomationId"); }
            catch (OpenQA.Selenium.WebDriverException) { /* attribute unavailable — fall through */ }

            if (!string.IsNullOrEmpty(pickerId))
                ClickElement(pickerId);
            else
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
        protected override void LogInputBlockers(string context, string? targetAutomationId = null)
        {
            // Off-screen target is the leading hypothesis, so check it first and loudest.
            //
            // WinAppDriver's Click on an element outside the visible viewport does not reliably
            // reach the handler (docs/memory/feedback_flaui_scroll_into_view.md is the same
            // lesson for CollectionView items). The element is still in the UIA tree with
            // sensible bounds, so it is found, clicked, reported as clicked — and nothing
            // happens. That is exactly the observed shape.
            //
            // It also explains why this only ever fails on CI: the runner's desktop is smaller
            // than a dev monitor, MainPage's MainColumnsGrid collapses to one column, and
            // turning BO3 on adds a whole panel — so the tab row can sit below the fold there
            // and never here.
            if (targetAutomationId is not null)
            {
                try
                {
                    AppiumElement target = FindUIElement(targetAutomationId);
                    System.Drawing.Point origin = target.Location;
                    System.Drawing.Size size = target.Size;
                    System.Drawing.Point windowOrigin = App.Manage().Window.Position;
                    System.Drawing.Size windowSize = App.Manage().Window.Size;

                    bool insideViewport =
                        origin.Y >= windowOrigin.Y &&
                        origin.Y + size.Height <= windowOrigin.Y + windowSize.Height &&
                        origin.X >= windowOrigin.X &&
                        origin.X + size.Width <= windowOrigin.X + windowSize.Width;

                    PerfLog($"BLOCKERS [{context}] target '{targetAutomationId}' at ({origin.X},{origin.Y}) {size.Width}x{size.Height}; " +
                        $"window at ({windowOrigin.X},{windowOrigin.Y}) {windowSize.Width}x{windowSize.Height}; insideViewport={insideViewport}");

                    if (!insideViewport)
                    {
                        PerfLog($"BLOCKERS [{context}] '{targetAutomationId}' IS OUTSIDE THE VISIBLE WINDOW. " +
                            "It is in the UIA tree so it is found and 'clicked', but the click cannot land. " +
                            "Scroll it into view before clicking rather than retrying the click.");
                    }
                }
                catch (Exception ex)
                {
                    PerfLog($"BLOCKERS [{context}] could not measure '{targetAutomationId}': {ex.GetType().Name}");
                }
            }

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
