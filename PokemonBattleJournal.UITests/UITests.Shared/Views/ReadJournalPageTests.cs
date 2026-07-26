namespace UITests
{
    public partial class ReadJournalPageTests : BaseTest
    {
        [Fact]
        public void ReadJournalPage_Loads_PageVisible()
        {
            NavigateTo("Read Journal");

            AppiumElement page = FindUIElement("ReadJournalPage");

            page.Displayed.ShouldBeTrue();
        }
    }
}
