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
            TryClickIfPresent($"DeleteArchetype_{name}");
        }

        private void DeleteCreatedTag(string name)
        {
            TryClickIfPresent($"DeleteTag_{name}");
        }

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
        public void OptionsPage_ArchetypeList_ShowsDebugSeededArchetypes()
        {
            // Debug seed inserts Charizard, Regidrago, Miraidon directly.
            FindUIElement("DeleteArchetype_Charizard").ShouldNotBeNull();
            FindUIElement("DeleteArchetype_Regidrago").ShouldNotBeNull();
            FindUIElement("DeleteArchetype_Miraidon").ShouldNotBeNull();
        }

        [Test]
        public void OptionsPage_TagList_ShowsDebugSeededTags()
        {
            // Debug seed inserts "Lucky" and "Early Start" for UITestTrainer.
            FindUIElement("DeleteTag_Lucky").ShouldNotBeNull();
            FindUIElement("DeleteTag_Early Start").ShouldNotBeNull();
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

        [Test]
        public void OptionsPage_SectionHeadings_Displayed()
        {
            FindUIElement("TrainerSectionHeading").ShouldNotBeNull();
            FindUIElement("ArchetypeSectionHeading").ShouldNotBeNull();
            FindUIElement("TagSectionHeading").ShouldNotBeNull();
            FindUIElement("SwitchTrainerHeading").ShouldNotBeNull();
        }

        [Test]
        public void OptionsPage_ArchetypeList_Container_Displayed()
        {
            FindUIElement("ArchetypeList").ShouldNotBeNull();
        }

        [Test]
        public void OptionsPage_TagList_Container_Displayed()
        {
            FindUIElement("TagList").ShouldNotBeNull();
        }

        [Test]
        public void OptionsPage_ArchetypeItem_Other_Displayed()
        {
            // ArchetypeItem_{Name} is the row container in the archetype list.
            FindUIElement("ArchetypeItem_Other").ShouldNotBeNull();
        }

        [Test]
        public void OptionsPage_TagItem_Lucky_Displayed()
        {
            // TagItem_{Name} is the row container in the tag list.
            FindUIElement("TagItem_Lucky").ShouldNotBeNull();
        }

        [Test]
        public void OptionsPage_DeleteTrainer_Displayed()
        {
            FindUIElement("DeleteTrainer").ShouldNotBeNull();
        }

        [Test]
        public void OptionsPage_DeleteArchetype_RemovesFromList()
        {
            string deckName = $"DelTestDeck-{DateTime.Now:HHmmss}";

            AppiumElement input = FindUIElement("ArchetypeNameInput");
            input.Clear();
            input.SendKeys(deckName);
            FindUIElement("SaveArchetypeButton").Click();

            // Wait for row to appear (proves save completed)
            FindUIElement($"DeleteArchetype_{deckName}").ShouldNotBeNull();

            FindUIElement($"DeleteArchetype_{deckName}").Click();

            // Verify gone
            App.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
            try
            {
                bool still = App.FindElements(MobileBy.AccessibilityId($"DeleteArchetype_{deckName}")).Count > 0;
                still.ShouldBeFalse("Archetype row should be removed after delete");
            }
            finally { App.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5); }
        }

        [Test]
        public void OptionsPage_DeleteTag_RemovesFromList()
        {
            string tagName = $"DelTestTag-{DateTime.Now:HHmmss}";

            AppiumElement tagInput = FindUIElement("TagInput");
            tagInput.Clear();
            tagInput.SendKeys(tagName);
            FindUIElement("SaveTagButton").Click();

            // Wait for row to appear (proves save completed)
            FindUIElement($"DeleteTag_{tagName}").ShouldNotBeNull();

            FindUIElement($"DeleteTag_{tagName}").Click();

            // Verify gone
            App.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
            try
            {
                bool still = App.FindElements(MobileBy.AccessibilityId($"DeleteTag_{tagName}")).Count > 0;
                still.ShouldBeFalse("Tag row should be removed after delete");
            }
            finally { App.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5); }
        }
    }
}
