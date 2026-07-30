namespace UITests
{
    public partial class OptionsPageTests : BaseTest
    {
        [OneTimeSetUp]
        public void SetUp() => NavigateTo("Options");

        [Test]
        public void OptionsPage_Loads_PageVisible()
        {
            AppiumElement saveButton = FindUIElement("SaveTrainerNameButton");
            saveButton.ShouldNotBeNull();
        }

        [Test]
        public async Task OptionsPage_ArchetypeNameInput_AcceptsText()
        {

            AppiumElement input = FindUIElement("ArchetypeNameInput");
            input.Clear();
            input.SendKeys("TestDeck");

            input.Text.ShouldEndWith("TestDeck");

            // Clear so subsequent tests don't see stale name in the input
            input.Clear();
        }

        [Test]
        public void OptionsPage_ArchetypeIconPicker_Displayed()
        {
            AppiumElement picker = FindUIElement("ArchetypeIconPicker");
            picker.ShouldNotBeNull();
        }

        [Test]
        public void OptionsPage_SaveArchetypeButton_Displayed()
        {
            AppiumElement btn = FindUIElement("SaveArchetypeButton");
            btn.ShouldNotBeNull();
        }

        [Test]
        public async Task OptionsPage_SaveArchetype_WithName_ClearsInput()
        {

            // Unique suffix avoids UNIQUE constraint failure on repeated local runs
            string deckName = $"UITestDeck-{DateTime.Now:HHmmss}";
            AppiumElement input = FindUIElement("ArchetypeNameInput");
            input.Clear();
            input.SendKeys(deckName);

            // No explicit icon selection needed — VM falls back to SelectedIcon default ("ball_icon.png").
            FindUIElement("SaveArchetypeButton").Click();

            // Wait for the new archetype row to appear — this proves the async save completed.
            // Check input cleared only after the row exists, so we never race the VM.
            FindUIElement($"DeleteArchetype_{deckName}").ShouldNotBeNull();

            // Input cleared means save path ran (not early-returned due to missing icon or null trainer).
            AppiumElement clearedInput = FindUIElement("ArchetypeNameInput");
            // On Android, empty Entry returns placeholder text not null/empty — check the typed name is gone.
            clearedInput.Text.ShouldNotContain(deckName);

            // Clean up — delete immediately; use 0ms timeout so a missing button fails fast rather than waiting 15s.
            App.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
            try { FindUIElement($"DeleteArchetype_{deckName}").Click(); }
            catch (OpenQA.Selenium.NoSuchElementException) { }
            finally { App.Manage().Timeouts().ImplicitWait = App is WindowsDriver ? TimeSpan.FromSeconds(5) : TimeSpan.FromSeconds(10); }
        }

        [Test]
        public void OptionsPage_ArchetypeList_ShowsSeededItems()
        {
            // SeedTestData selects "Other" for both archetypes, so DeleteArchetype_Other must be visible.
            // This test catches the ScrollView+BindableLayout collapse bug where items are in the UIA
            // tree but have zero height and are visually invisible.
            FindUIElement("DeleteArchetype_Other").ShouldNotBeNull();
        }

        [Test]
        public void OptionsPage_TagInput_Displayed()
        {
            AppiumElement tagInput = FindUIElement("TagInput");
            tagInput.ShouldNotBeNull();
        }

        [Test]
        public async Task OptionsPage_SaveTag_WithName_Saves()
        {

            // Unique suffix prevents UNIQUE constraint failure on repeated runs
            string tagName = $"UITestTag-{DateTime.Now:HHmmss}";
            AppiumElement tagInput = FindUIElement("TagInput");
            tagInput.Clear();
            tagInput.SendKeys(tagName);
            await Task.Delay(300);

            AppiumElement saveBtn = FindUIElement("SaveTagButton");
            saveBtn.Click();

            // Wait for the new tag row — proves the async save completed, no Task.Delay needed.
            FindUIElement($"DeleteTag_{tagName}").ShouldNotBeNull();

            // Clean up with 0ms timeout so a missing button fails fast rather than waiting 15s.
            App.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
            try { FindUIElement($"DeleteTag_{tagName}").Click(); }
            catch (OpenQA.Selenium.NoSuchElementException) { }
            finally { App.Manage().Timeouts().ImplicitWait = App is WindowsDriver ? TimeSpan.FromSeconds(5) : TimeSpan.FromSeconds(10); }
        }

        [Test]
        public void OptionsPage_TrainerSwitchPicker_Displayed()
        {
            AppiumElement picker = FindUIElement("TrainerSwitchPicker");
            picker.ShouldNotBeNull();
        }

        [Test]
        public void OptionsPage_SaveAllButton_Displayed()
        {
            AppiumElement btn = FindUIElement("SaveAllButton");
            btn.ShouldNotBeNull();
        }

        [Test]
        public void OptionsPage_TrainerNameInput_Displayed()
        {
            AppiumElement input = FindUIElement("TrainerNameInput");
            input.ShouldNotBeNull();
        }
    }
}
