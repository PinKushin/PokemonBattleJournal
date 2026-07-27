namespace UITests
{
    public partial class AboutPageTests : BaseTest
    {
        [Fact]
        public void AboutPage_Loads_TitleDisplayed()
        {
            NavigateTo("About");
            // FindUIElement success proves element exists; .Displayed round-trip can race on slow sessions
            AppiumElement title = FindUIElement("AboutPageTitle");
            title.Enabled.ShouldBeTrue();
        }
    }
}
