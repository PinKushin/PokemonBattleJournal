namespace UITests
{
    public partial class AboutPageTests : BaseTest
    {
        [Fact]
        public void AboutPage_Loads_TitleDisplayed()
        {
            var (title, elapsed) = MeasurePageLoad("About", "AboutPageTitle");

            title.Displayed.ShouldBeTrue();
            elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));
        }
    }
}
