namespace UITests
{
    public partial class OptionsPageTests : BaseTest
    {
        [Test]
        public async Task OptionsPage_TrainerNameInput_AcceptsText()
        {
            AppiumElement input = FindUIElement("TrainerNameInput");
            input.Clear();
            input.SendKeys("Ash");

            // Poll until binding update reflects the typed text.
            var deadline = DateTime.UtcNow.AddSeconds(5);
            string actual = input.GetAttribute("Value.Value");
            while (DateTime.UtcNow < deadline && actual != "Ash")
            {
                await Task.Delay(250);
                actual = input.GetAttribute("Value.Value");
            }
            actual.ShouldBe("Ash");

            // Clear so later tests don't see stale trainer name
            try { input.Clear(); } catch (OpenQA.Selenium.NoSuchElementException) { }
        }
    }
}
