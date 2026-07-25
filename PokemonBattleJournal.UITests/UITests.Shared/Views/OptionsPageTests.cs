namespace UITests
{
    public partial class OptionsPageTests : BaseTest
    {
        [Fact]
        public void OptionsPage_TitleLabel_Displayed()
        {
            NavigateTo("Options");

            AppiumElement titleLabel = FindUIElement("OptionsPageTitleLabel");

            titleLabel.Displayed.ShouldBeTrue();
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
