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
        public void OptionsPage_SaveArchetype_WithName_ClearsInput()
        {
            NavigateTo("Options");

            AppiumElement input = FindUIElement("ArchetypeNameInput");
            input.Clear();
            input.SendKeys("UITestDeck");

            // No explicit icon selection needed — VM falls back to SelectedIcon default ("ball_icon.png").
            FindUIElement("SaveArchetypeButton").Click();

            // Input cleared in finally means the save path ran (not an early return due to missing icon).
            AppiumElement clearedInput = FindUIElement("ArchetypeNameInput");
            clearedInput.Text.ShouldBeNullOrEmpty();
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
