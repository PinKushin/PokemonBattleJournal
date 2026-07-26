namespace UITests
{
    public partial class OptionsPageTests : BaseTest
    {
        [Fact]
        public void OptionsPage_Loads_PageVisible()
        {
            var (saveButton, elapsed) = MeasurePageLoad("Options", "SaveTrainerNameButton");

            saveButton.Displayed.ShouldBeTrue();
            elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));
        }

        [Fact]
        public async Task OptionsPage_ArchetypeNameInput_AcceptsText()
        {
            NavigateTo("Options");

            AppiumElement input = FindUIElement("ArchetypeNameInput");
            input.Clear();
            input.SendKeys("TestDeck");
            await Task.Delay(300);

            input.Text.ShouldBe("TestDeck");
        }
    }
}
