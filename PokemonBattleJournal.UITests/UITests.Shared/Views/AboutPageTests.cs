namespace UITests
{
    public partial class AboutPageTests : BaseTest
    {
        [OneTimeSetUp]
        public void SetUp() => NavigateTo("About");

        [Test]
        public void AboutPage_Loads_TitleDisplayed()
        {
            AppiumElement title = FindUIElement("AboutPageTitle");
            title.ShouldNotBeNull();
        }

        [Test]
        public void AboutPage_Author_Displayed()
        {
            FindUIElement("AboutPageAuthor").ShouldNotBeNull();
        }

        [Test]
        public void AboutPage_Tagline_Displayed()
        {
            FindUIElement("AboutPageTagline").ShouldNotBeNull();
        }

        [Test]
        public void AboutPage_Logo_Displayed()
        {
            FindUIElement("AboutPageLogo").ShouldNotBeNull();
        }
    }
}
