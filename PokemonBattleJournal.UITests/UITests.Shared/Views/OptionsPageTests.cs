namespace UITests
{
    public partial class OptionsPageTests : BaseTest
    {
        [Fact]
        public void OptionsPage_Loads_PageVisible()
        {
            NavigateTo("Options");
            AppiumElement saveButton = FindUIElement("SaveTrainerNameButton");
            saveButton.ShouldNotBeNull();
        }

        [Fact]
        public async Task OptionsPage_ArchetypeNameInput_AcceptsText()
        {
            NavigateTo("Options");

            AppiumElement input = FindUIElement("ArchetypeNameInput");
            input.Clear();
            input.SendKeys("TestDeck");
            await Task.Delay(300);

            input.Text.ShouldEndWith("TestDeck");

            // Clear so subsequent tests don't see stale name in the input
            input.Clear();
        }

        [Fact]
        public void OptionsPage_ArchetypeIconPicker_Displayed()
        {
            NavigateTo("Options");
            AppiumElement picker = FindUIElement("ArchetypeIconPicker");
            picker.ShouldNotBeNull();
        }

        [Fact]
        public void OptionsPage_SaveArchetypeButton_Displayed()
        {
            NavigateTo("Options");
            AppiumElement btn = FindUIElement("SaveArchetypeButton");
            btn.ShouldNotBeNull();
        }

        [Fact]
        public async Task OptionsPage_SaveArchetype_WithName_ClearsInput()
        {
            NavigateTo("Options");

            // Unique suffix avoids UNIQUE constraint failure on repeated local runs
            string deckName = $"UITestDeck-{DateTime.Now:HHmmss}";
            AppiumElement input = FindUIElement("ArchetypeNameInput");
            input.Clear();
            input.SendKeys(deckName);

            // No explicit icon selection needed — VM falls back to SelectedIcon default ("ball_icon.png").
            FindUIElement("SaveArchetypeButton").Click();

            // Async save command — wait for VM to clear the name field before asserting.
            await Task.Delay(500);

            // Input cleared means save path ran (not early-returned due to missing icon or null trainer).
            AppiumElement clearedInput = FindUIElement("ArchetypeNameInput");
            // On Android, empty Entry returns placeholder text not null/empty — check the typed name is gone.
            clearedInput.Text.ShouldNotContain(deckName);

            // Verify the new archetype row is visible in the list (regression for ScrollView+CollectionView collapse bug).
            FindUIElement($"DeleteArchetype_{deckName}").ShouldNotBeNull();

            // Clean up — delete immediately; use 0ms timeout so a missing button fails fast rather than waiting 15s.
            App.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
            try { FindUIElement($"DeleteArchetype_{deckName}").Click(); }
            catch (OpenQA.Selenium.NoSuchElementException) { }
            finally { App.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(15); }
        }

        [Fact]
        public void OptionsPage_ArchetypeList_ShowsSeededItems()
        {
            // SeedTestData selects "Other" for both archetypes, so DeleteArchetype_Other must be visible.
            // This test catches the ScrollView+BindableLayout collapse bug where items are in the UIA
            // tree but have zero height and are visually invisible.
            NavigateTo("Options");
            FindUIElement("DeleteArchetype_Other").ShouldNotBeNull();
        }

        [Fact]
        public void OptionsPage_TagInput_Displayed()
        {
            NavigateTo("Options");
            AppiumElement tagInput = FindUIElement("TagInput");
            tagInput.ShouldNotBeNull();
        }

        [Fact]
        public async Task OptionsPage_SaveTag_WithName_Saves()
        {
            NavigateTo("Options");

            // Unique suffix prevents UNIQUE constraint failure on repeated runs
            string tagName = $"UITestTag-{DateTime.Now:HHmmss}";
            AppiumElement tagInput = FindUIElement("TagInput");
            tagInput.Clear();
            tagInput.SendKeys(tagName);
            await Task.Delay(300);

            AppiumElement saveBtn = FindUIElement("SaveTagButton");
            saveBtn.Click();
            await Task.Delay(500);

            FindUIElement("SaveTagButton").ShouldNotBeNull();

            // Verify the new tag row is visible in the list.
            FindUIElement($"DeleteTag_{tagName}").ShouldNotBeNull();

            // Clean up with 0ms timeout so a missing button fails fast rather than waiting 15s.
            App.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
            try { FindUIElement($"DeleteTag_{tagName}").Click(); }
            catch (OpenQA.Selenium.NoSuchElementException) { }
            finally { App.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(15); }
        }

        [Fact]
        public void OptionsPage_TrainerSwitchPicker_Displayed()
        {
            NavigateTo("Options");
            AppiumElement picker = FindUIElement("TrainerSwitchPicker");
            picker.ShouldNotBeNull();
        }

        [Fact]
        public void OptionsPage_SaveAllButton_Displayed()
        {
            NavigateTo("Options");
            AppiumElement btn = FindUIElement("SaveAllButton");
            btn.ShouldNotBeNull();
        }

        [Fact]
        public void OptionsPage_TrainerNameInput_Displayed()
        {
            NavigateTo("Options");
            AppiumElement input = FindUIElement("TrainerNameInput");
            input.ShouldNotBeNull();
        }
    }
}
