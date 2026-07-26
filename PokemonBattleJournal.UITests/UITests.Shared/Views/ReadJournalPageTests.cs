namespace UITests
{
    public partial class ReadJournalPageTests : BaseTest
    {
        [Fact]
        public void ReadJournalPage_Loads_PageVisible()
        {
            var (page, elapsed) = MeasurePageLoad("Read Journal", "ReadJournalPage");

            page.Displayed.ShouldBeTrue();
            elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));
        }
    }
}
