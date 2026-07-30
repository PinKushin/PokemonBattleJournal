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
    }
}
