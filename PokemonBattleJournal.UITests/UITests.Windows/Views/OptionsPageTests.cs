namespace UITests
{
    public partial class OptionsPageTests : BaseTest
    {
        [Fact]
        public async Task OptionsPage_TrainerNameInput_AcceptsText()
        {
            NavigateTo("Options");

            AppiumElement input = FindUIElement("TrainerNameInput");
            input.Clear();
            input.SendKeys("Ash");
            await Task.Delay(300);

            input.GetAttribute("Value.Value").ShouldBe("Ash");

            // Clear so later tests don't see stale trainer name
            try { input.Clear(); } catch (OpenQA.Selenium.NoSuchElementException) { }
        }
    }
}
