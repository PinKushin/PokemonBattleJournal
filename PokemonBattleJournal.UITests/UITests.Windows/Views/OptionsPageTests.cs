namespace UITests
{
    public partial class OptionsPageTests : BaseTest
    {
        // Accessibility contract — see the MainPage partial for why these exist and what
        // they catch. Windows-only: they read the UIA tree directly. No interactions.
        [Test]
        [TestCase("SaveTrainerNameButton")]
        [TestCase("SaveArchetypeButton")]
        [TestCase("SaveTagButton")]
        [TestCase("SaveAllButton")]
        [TestCase("ImportTrainerHillButton")]
        [TestCase("ExportTrainerHillButton")]
        [TestCase("ExportBackupButton")]
        [TestCase("RestoreBackupButton")]
        [TestCase("ArchetypeIconPicker")]
        [TestCase("TrainerNameInput")]
        [TestCase("ArchetypeNameInput")]
        // DEBUG-only buttons are present in exactly the configuration these tests run, so there
        // is no reason to exempt them. Note SimulateLoadingButton is NOT listed and predates
        // this — an existing gap rather than a decision, left alone here to keep the change
        // scoped.
        [TestCase("SentryDiagnosticsButton")]
        public void OptionsPage_InteractiveElement_IsAnnouncedAndOperable(string automationId)
        {
            UiaContract contract = GetUiaContract(automationId);

            contract.Found.ShouldBeTrue($"'{automationId}' is not in the UIA tree at all");

            contract.Name.ShouldNotBeNullOrWhiteSpace(
                $"'{automationId}' has no accessible Name, so a screen reader announces nothing " +
                "for it. Set SemanticProperties.Description.");

            contract.Patterns.ShouldNotBeEmpty(
                $"'{automationId}' exposes no UIA control pattern, so assistive technology " +
                "cannot operate it — readable but unusable. See " +
                "docs/memory/feedback_invokable_controls.md.");
        }

        [Test]
        public async Task OptionsPage_TrainerNameInput_AcceptsText()
        {
            AppiumElement input = FindUIElement("TrainerNameInput");
            input.Clear();
            input.SendKeys("Ash");

            // Poll until binding update reflects the typed text.
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            string actual = input.GetAttribute("Value.Value");
            while (DateTime.UtcNow < deadline && actual != "Ash")
            {
                await Task.Delay(250);
                actual = input.GetAttribute("Value.Value");
            }
            actual.ShouldBe("Ash");

            // Clear so later tests don't see stale trainer name
            try { input.Clear(); } catch (OpenQA.Selenium.NoSuchElementException) { }
        }
    }
}
