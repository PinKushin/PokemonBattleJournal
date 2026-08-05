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

        // Three-stage lookup:
        //   1. Direct resource-id poll (up to 5s) — fastest, covers most main-page elements.
        //   2. AccessibilityId (content-desc) single-shot — covers popup Dialog elements and
        //      MAUI SearchBar/composite controls whose AutomationId does not propagate to
        //      resource-id on the native widget root.
        //   3. UiScrollable scrollIntoView — brings off-screen elements into view.
        // Stage 2 is necessary because popups open in a separate Android Dialog window whose
        // children may not be under the main app scrollable, and UiScrollable(instance=0)
        // targets the main ScrollView. AccessibilityId works cross-window.
        protected override AppiumElement FindUIElement(string id)
        {
            string resourceId = $"{PackageName}:id/{id}";
            string directSelector = $"new UiSelector().resourceId(\"{resourceId}\")";
            var overallSw = System.Diagnostics.Stopwatch.StartNew();

            // Stage 1: direct resource-id (5s deadline, 500ms per poll)
            var stage1Sw = System.Diagnostics.Stopwatch.StartNew();
            var stage1Deadline = DateTime.UtcNow.AddSeconds(5);
            try
            {
                while (true)
                {
                    try
                    {
                        App.Manage().Timeouts().ImplicitWait = TimeSpan.FromMilliseconds(500);
                        var el = App.FindElement(MobileBy.AndroidUIAutomator(directSelector));
                        PerfLog($"FIND '{id}' STAGE1_OK resource-id in {stage1Sw.ElapsedMilliseconds}ms");
                        return el;
                    }
                    catch (OpenQA.Selenium.NoSuchElementException) when (DateTime.UtcNow < stage1Deadline) { }
                    catch (OpenQA.Selenium.NoSuchElementException) { break; }
                }
            }
            finally { App.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10); }
            PerfLog($"FIND '{id}' STAGE1_MISS after {stage1Sw.ElapsedMilliseconds}ms → try content-desc");

            // Stage 2: AccessibilityId (content-desc) — cross-window, covers popup Dialog elements
            var stage2Sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                App.Manage().Timeouts().ImplicitWait = TimeSpan.FromMilliseconds(500);
                var el = App.FindElement(MobileBy.AccessibilityId(id));
                PerfLog($"FIND '{id}' STAGE2_OK content-desc in {stage2Sw.ElapsedMilliseconds}ms (total {overallSw.ElapsedMilliseconds}ms)");
                return el;
            }
            catch (OpenQA.Selenium.NoSuchElementException) { }
            finally { App.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10); }
            PerfLog($"FIND '{id}' STAGE2_MISS after {stage2Sw.ElapsedMilliseconds}ms → try scrollIntoView");

            // Stage 3: UiScrollable — scrolls the main page to bring off-screen elements into view
            var stage3Sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                App.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
                var el = App.FindElement(MobileBy.AndroidUIAutomator(
                    $"new UiScrollable(new UiSelector().scrollable(true).packageName(\"{PackageName}\").instance(0))" +
                    $".scrollIntoView({directSelector})"));
                PerfLog($"FIND '{id}' STAGE3_OK scrollIntoView in {stage3Sw.ElapsedMilliseconds}ms (total {overallSw.ElapsedMilliseconds}ms)");
                return el;
            }
            catch (Exception ex)
            {
                PerfLog($"FIND '{id}' STAGE3_FAIL after {stage3Sw.ElapsedMilliseconds}ms (total {overallSw.ElapsedMilliseconds}ms): {ex.GetType().Name}");
                DumpVisibleElements($"FIND '{id}' STAGE3_FAIL");
                throw;
            }
        }

        /// <summary>
        /// Dumps the visible resource-ids and content-descs currently in the UIA tree to PerfLog.
        /// Useful for diagnosing why a FindUIElement failed — shows what IS present so you can
        /// see if the id is wrong, on a different element, or if the whole tree is unexpected
        /// (wrong window, popup gone, etc.). Best-effort — never throws.
        /// </summary>
        protected override void DumpVisibleElements(string context, int maxItems = 40)
        {
            // Opt-in via env var. Iterating the tree with GetDomAttribute per element issues
            // ~30+ driver round-trips per dump — running it after every FindUIElement failure
            // churns the emulator badly enough to destabilize downstream tests (session death
            // observed on OptionsPage after ~8 dumps during MainPage failures on 2026-08-04).
            if (Environment.GetEnvironmentVariable("UITEST_DUMP_ON_FAIL") != "1")
            {
                PerfLog($"DUMP [{context}] skipped (set UITEST_DUMP_ON_FAIL=1 to enable)");
                return;
            }
            try
            {
                App.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
                // Get all elements with any resource-id
                var withResIds = App.FindElements(MobileBy.AndroidUIAutomator(
                    $"new UiSelector().packageName(\"{PackageName}\").resourceIdMatches(\".+:id/.+\")"));
                PerfLog($"DUMP [{context}] resource-ids ({withResIds.Count} found, first {maxItems}):");
                int i = 0;
                foreach (var el in withResIds)
                {
                    if (i++ >= maxItems) { PerfLog($"DUMP [{context}] ...(truncated)"); break; }
                    try
                    {
                        string rid = el.GetDomAttribute("resource-id") ?? "?";
                        string cd = el.GetDomAttribute("content-desc") ?? "";
                        string txt = el.Text ?? "";
                        string cls = el.GetDomAttribute("className") ?? "?";
                        PerfLog($"  [{cls}] rid={rid} desc='{cd}' text='{txt}'");
                    }
                    catch (Exception ex) { PerfLog($"  <read failed: {ex.GetType().Name}>"); }
                }

                // Also dump elements with content-desc (may not overlap with resource-id set)
                var withCd = App.FindElements(MobileBy.AndroidUIAutomator(
                    "new UiSelector().descriptionMatches(\".+\")"));
                PerfLog($"DUMP [{context}] content-descs ({withCd.Count} found, first {maxItems}):");
                i = 0;
                foreach (var el in withCd)
                {
                    if (i++ >= maxItems) { PerfLog($"DUMP [{context}] ...(truncated)"); break; }
                    try
                    {
                        string cd = el.GetDomAttribute("content-desc") ?? "?";
                        string cls = el.GetDomAttribute("className") ?? "?";
                        PerfLog($"  [{cls}] desc='{cd}'");
                    }
                    catch { }
                }
            }
            catch (Exception ex) { PerfLog($"DUMP [{context}] failed: {ex.GetType().Name}: {ex.Message}"); }
            finally { App.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10); }
        }

        /// <summary>
        /// Sends the Android BACK keyevent via adb. Dismisses dialogs/popups when the
        /// in-app Cancel button cannot be found or clicked (e.g. content-desc reset).
        /// Best-effort — logs failures but never throws.
        /// </summary>
        protected override void SendAndroidBack()
        {
            PerfLog("SendAndroidBack: firing adb shell input keyevent KEYCODE_BACK");
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("adb", "shell input keyevent KEYCODE_BACK")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = System.Diagnostics.Process.Start(psi);
                bool exited = proc?.WaitForExit(2000) ?? false;
                string stdout = proc?.StandardOutput.ReadToEnd() ?? "";
                string stderr = proc?.StandardError.ReadToEnd() ?? "";
                int code = proc?.ExitCode ?? -1;
                PerfLog($"SendAndroidBack: exited={exited} code={code} stdout='{stdout.Trim()}' stderr='{stderr.Trim()}'");
            }
            catch (Exception ex)
            {
                NavLog($"SendAndroidBack failed: {ex.GetType().Name}: {ex.Message}");
                PerfLog($"SendAndroidBack failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        // AccessibilityId on Android maps to content-desc, not resource-id. AutomationId → resource-id.
        // Override IsElementPresentCore to use resource-id so presence checks are correct.
        protected override bool IsElementPresentCore(string id)
        {
            string resourceId = $"{PackageName}:id/{id}";
            return App.FindElements(MobileBy.AndroidUIAutomator(
                $"new UiSelector().resourceId(\"{resourceId}\")")).Count > 0;
        }

        protected override AppiumElement FindByDescription(string _, string androidDescription) =>
            App.FindElement(MobileBy.AndroidUIAutomator(
                $"new UiScrollable(new UiSelector().scrollable(true).instance(0))" +
                $".scrollIntoView(new UiSelector().description(\"{androidDescription}\"))"));

        // Uses resourceId — reliable on Android even after IsVisible binding cascades reset content-desc.
        // Android: UiScrollable scrolls to the element during lookup, so plain FindUIElement+Click suffices.
        protected override void ScrollIntoViewAndClick(string automationId) =>
            FindUIElement(automationId).Click();

        protected override string GetElementText(string automationId)
        {
            string resourceId = $"{PackageName}:id/{automationId}";
            return App.FindElement(MobileBy.AndroidUIAutomator(
                $"new UiSelector().resourceId(\"{resourceId}\")")).Text;
        }

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
