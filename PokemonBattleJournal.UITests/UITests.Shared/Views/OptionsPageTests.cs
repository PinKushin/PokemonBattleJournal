namespace UITests
{
    public partial class OptionsPageTests : BaseTest
    {
        [OneTimeSetUp]
        public void SetUp() => NavigateTo("Options");

        // ---------------------------------------------------------------------------
        // Cleanup helpers — only called by tests that create data
        // ---------------------------------------------------------------------------

        private void DeleteCreatedArchetype(string name)
        {
            App.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
            try { FindUIElement($"DeleteArchetype_{name}").Click(); }
            catch (OpenQA.Selenium.NoSuchElementException) { }
            finally { RestoreImplicitWait(); }
        }

        private void DeleteCreatedTag(string name)
        {
            App.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
            try { FindUIElement($"DeleteTag_{name}").Click(); }
            catch (OpenQA.Selenium.NoSuchElementException) { }
            finally { RestoreImplicitWait(); }
        }

        private void RestoreImplicitWait() =>
            App.Manage().Timeouts().ImplicitWait = App is WindowsDriver
                ? TimeSpan.FromSeconds(5)
                : TimeSpan.FromSeconds(10);

        // ---------------------------------------------------------------------------
        // Tests
        // ---------------------------------------------------------------------------

        [Test]
        public void OptionsPage_Loads_PageVisible()
        {
            FindUIElement("SaveTrainerNameButton").ShouldNotBeNull();
        }

        [Test]
        public void OptionsPage_ArchetypeNameInput_AcceptsText()
        {
            AppiumElement input = FindUIElement("ArchetypeNameInput");
            input.Clear();
            input.SendKeys("TestDeck");
            input.Text.ShouldEndWith("TestDeck");
            input.Clear();
        }

        [Test]
        public void OptionsPage_ArchetypeIconPicker_Displayed()
        {
            FindUIElement("ArchetypeIconPicker").ShouldNotBeNull();
        }

        [Test]
        public void OptionsPage_SaveArchetypeButton_Displayed()
        {
            FindUIElement("SaveArchetypeButton").ShouldNotBeNull();
        }

        [Test]
        public void OptionsPage_SaveArchetype_WithName_ClearsInput()
        {
            string deckName = $"UITestDeck-{DateTime.Now:HHmmss}";
            try
            {
                AppiumElement input = FindUIElement("ArchetypeNameInput");
                input.Clear();
                input.SendKeys(deckName);

                FindUIElement("SaveArchetypeButton").Click();

                // Poll for new row — proves async save completed before checking input.
                FindUIElement($"DeleteArchetype_{deckName}").ShouldNotBeNull();

                FindUIElement("ArchetypeNameInput").Text.ShouldNotContain(deckName);
            }
            finally { DeleteCreatedArchetype(deckName); }
        }

        [Test]
        public void OptionsPage_ArchetypeList_ShowsSeededItems()
        {
            FindUIElement("DeleteArchetype_Other").ShouldNotBeNull();
        }

        [Test]
        public void OptionsPage_TagInput_Displayed()
        {
            FindUIElement("TagInput").ShouldNotBeNull();
        }

        [Test]
        public void OptionsPage_SaveTag_WithName_Saves()
        {
            string tagName = $"UITestTag-{DateTime.Now:HHmmss}";
            try
            {
                AppiumElement tagInput = FindUIElement("TagInput");
                tagInput.Clear();
                tagInput.SendKeys(tagName);

                FindUIElement("SaveTagButton").Click();

                // Poll for new row — proves async save completed.
                FindUIElement($"DeleteTag_{tagName}").ShouldNotBeNull();
            }
            finally { DeleteCreatedTag(tagName); }
        }

        [Test]
        public void OptionsPage_TrainerSwitchPicker_Displayed()
        {
            FindUIElement("TrainerSwitchPicker").ShouldNotBeNull();
        }

        [Test]
        public void OptionsPage_SaveAllButton_Displayed()
        {
            FindUIElement("SaveAllButton").ShouldNotBeNull();
        }

        [Test]
        public void OptionsPage_TrainerNameInput_Displayed()
        {
            FindUIElement("TrainerNameInput").ShouldNotBeNull();
        }
    }
}
