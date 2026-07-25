namespace UITests
{
    public partial class ReadJournalPageTests : BaseTest
    {
        [Fact]
        public void ReadJournalPage_WelcomeLabel_Displayed()
        {
            NavigateTo("Read Journal");

            AppiumElement welcomeLabel = FindUIElement("JournalWelcomeLabel");

            welcomeLabel.Displayed.ShouldBeTrue();
        }
    }
}
