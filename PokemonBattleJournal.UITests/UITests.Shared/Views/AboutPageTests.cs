namespace UITests
{
    public partial class AboutPageTests : BaseTest
    {
        [Fact]
        public void AboutPage_Loads_TitleDisplayed()
        {
            NavigateTo("About");
            AppiumElement title = FindUIElement("AboutPageTitle");

            title.Displayed.ShouldBeTrue();
        }
    }
}
