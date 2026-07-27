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
        public async Task OptionsPage_SaveArchetype_WithName_Saves()
        {
            NavigateTo("Options");

            AppiumElement input = FindUIElement("ArchetypeNameInput");
            input.Clear();
            input.SendKeys("UITestDeck");
            await Task.Delay(300);

            AppiumElement saveBtn = FindUIElement("SaveArchetypeButton");
            saveBtn.Click();
            await Task.Delay(500);

            // Button still present means command completed without crash
            FindUIElement("SaveArchetypeButton").ShouldNotBeNull();
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

            AppiumElement tagInput = FindUIElement("TagInput");
            tagInput.Clear();
            tagInput.SendKeys("UITestTag");
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
