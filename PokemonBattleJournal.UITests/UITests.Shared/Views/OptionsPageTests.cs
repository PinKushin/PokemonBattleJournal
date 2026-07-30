namespace UITests
{
    public partial class OptionsPageTests : BaseTest
    {
        private string? _createdArchetype;
        private string? _createdTag;

        [OneTimeSetUp]
        public void SetUp() => NavigateTo("Options");

        [TearDown]
        public void AfterEach()
        {
            // Delete any archetype or tag created by a test, then restore the normal implicit wait.
            App.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
            try
            {
                if (_createdArchetype is not null)
                {
                    FindUIElement($"DeleteArchetype_{_createdArchetype}").Click();
                    _createdArchetype = null;
                }
            }
            catch (OpenQA.Selenium.NoSuchElementException) { }
            try
            {
                if (_createdTag is not null)
                {
                    FindUIElement($"DeleteTag_{_createdTag}").Click();
                    _createdTag = null;
                }
            }
            catch (OpenQA.Selenium.NoSuchElementException) { }
            finally
            {
                App.Manage().Timeouts().ImplicitWait = App is WindowsDriver
                    ? TimeSpan.FromSeconds(5)
                    : TimeSpan.FromSeconds(10);
            }
        }

        [Test]
        public void OptionsPage_Loads_PageVisible()
        {
            AppiumElement saveButton = FindUIElement("SaveTrainerNameButton");
            saveButton.ShouldNotBeNull();
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
            AppiumElement picker = FindUIElement("ArchetypeIconPicker");
            picker.ShouldNotBeNull();
        }

        [Test]
        public void OptionsPage_SaveArchetypeButton_Displayed()
        {
            AppiumElement btn = FindUIElement("SaveArchetypeButton");
            btn.ShouldNotBeNull();
        }

        [Test]
        public void OptionsPage_SaveArchetype_WithName_ClearsInput()
        {
            _createdArchetype = $"UITestDeck-{DateTime.Now:HHmmss}";
            AppiumElement input = FindUIElement("ArchetypeNameInput");
            input.Clear();
            input.SendKeys(_createdArchetype);

            FindUIElement("SaveArchetypeButton").Click();

            // Poll for the new row — proves async save completed before checking input cleared.
            FindUIElement($"DeleteArchetype_{_createdArchetype}").ShouldNotBeNull();

            AppiumElement clearedInput = FindUIElement("ArchetypeNameInput");
            clearedInput.Text.ShouldNotContain(_createdArchetype);
        }

        [Test]
        public void OptionsPage_ArchetypeList_ShowsSeededItems()
        {
            FindUIElement("DeleteArchetype_Other").ShouldNotBeNull();
        }

        [Test]
        public void OptionsPage_TagInput_Displayed()
        {
            AppiumElement tagInput = FindUIElement("TagInput");
            tagInput.ShouldNotBeNull();
        }

        [Test]
        public void OptionsPage_SaveTag_WithName_Saves()
        {
            _createdTag = $"UITestTag-{DateTime.Now:HHmmss}";
            AppiumElement tagInput = FindUIElement("TagInput");
            tagInput.Clear();
            tagInput.SendKeys(_createdTag);

            FindUIElement("SaveTagButton").Click();

            // Poll for the new row — proves async save completed.
            FindUIElement($"DeleteTag_{_createdTag}").ShouldNotBeNull();
        }

        [Test]
        public void OptionsPage_TrainerSwitchPicker_Displayed()
        {
            AppiumElement picker = FindUIElement("TrainerSwitchPicker");
            picker.ShouldNotBeNull();
        }

        [Test]
        public void OptionsPage_SaveAllButton_Displayed()
        {
            AppiumElement btn = FindUIElement("SaveAllButton");
            btn.ShouldNotBeNull();
        }

        [Test]
        public void OptionsPage_TrainerNameInput_Displayed()
        {
            AppiumElement input = FindUIElement("TrainerNameInput");
            input.ShouldNotBeNull();
        }
    }
}
