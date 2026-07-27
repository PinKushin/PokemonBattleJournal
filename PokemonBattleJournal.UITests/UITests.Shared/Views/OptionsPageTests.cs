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

            // Android folds SemanticProperties.Description into content-desc, which Appium
            // prepends to the field value ("Archetype name, TestDeck"). Assert on the value.
            input.Text.ShouldEndWith("TestDeck");
        }
    }
}
