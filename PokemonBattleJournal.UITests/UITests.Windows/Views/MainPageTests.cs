namespace UITests
{
    public partial class MainPageTests : BaseTest
    {
        // ---------------------------------------------------------------------------
        // Accessibility contract
        //
        // Windows-only: these read the UIA tree, which is the API assistive technology on
        // Windows actually uses. Android's AccessibilityNodeInfo equivalent is separate work.
        //
        // Zero interactions except where a popup has to be open, so they add no flake surface —
        // they inspect a page the fixture already has on screen.
        //
        // What they are for: CLAUDE.md mandates SemanticProperties on every element, and
        // nothing verified it. Worse, the properties that look like compliance are not the ones
        // that matter. The BO3 switch had Description, Hint and IsInAccessibleTree="True",
        // passed review, was announced correctly, and could not be activated by a screen reader
        // at all — a Border with a TapGestureRecognizer exposes no control pattern. These
        // assert BOTH halves: announced (Name) and operable (a pattern).
        // ---------------------------------------------------------------------------

        [Test]
        [TestCase("PlayerArchetype")]
        [TestCase("RivalArchetype")]
        [TestCase("BOSwitch")]
        [TestCase("SaveMatchButton")]
        [TestCase("PossibleResultsPicker")]
        [TestCase("UserNoteInput")]
        public void MainPage_InteractiveElement_IsAnnouncedAndOperable(string automationId)
        {
            UiaContract contract = GetUiaContract(automationId);

            contract.Found.ShouldBeTrue($"'{automationId}' is not in the UIA tree at all");

            contract.Name.ShouldNotBeNullOrWhiteSpace(
                $"'{automationId}' has no accessible Name, so a screen reader announces nothing " +
                "for it. Set SemanticProperties.Description.");

            contract.Patterns.ShouldNotBeEmpty(
                $"'{automationId}' exposes no UIA control pattern, so assistive technology " +
                "cannot operate it — a screen reader can read it and not use it. This is what a " +
                "Border/Grid with a TapGestureRecognizer looks like; it needs to be a real " +
                "control (see docs/memory/feedback_invokable_controls.md).");
        }

        /// <summary>
        /// Every deck in the archetype popup must be selectable by assistive technology.
        /// </summary>
        /// <remarks>
        /// The one test here that interacts, because the items do not exist until the popup is
        /// open. Worth the cost: this is precisely where the defect was. Each item was a Grid
        /// with a TapGestureRecognizer whose Hint read "Double tap to select …" — announced as
        /// selectable, impossible to select.
        /// </remarks>
        [Test]
        public void MainPage_ArchetypePopupItem_IsAnnouncedAndOperable()
        {
            try
            {
                OpenArchetypePopup("PlayerArchetype");

                UiaContract contract = GetUiaContract("ArchetypeItem_Other");

                contract.Found.ShouldBeTrue("the popup item is not in the UIA tree");
                contract.Name.ShouldNotBeNullOrWhiteSpace(
                    "a popup item with no Name leaves a screen reader announcing an unlabelled row");
                contract.Patterns.ShouldNotBeEmpty(
                    "a popup item with no control pattern cannot be selected by assistive " +
                    "technology, however clearly its hint describes selecting it");
            }
            finally
            {
                DismissArchetypePopup();
            }
        }

        [Test]
        public void MainPage_BOSwitch_DisplayedAndToggled()
        {
            AppiumElement boSwitch = FindUIElement("BOSwitch");
            AppiumElement statusLabel = FindUIElement("BO3StatusLabel");

            if (statusLabel.Text == "Best of 3")
                ClickElement(boSwitch);

            try
            {
                ClickElement(boSwitch);
                string toggledOn = FindUIElement("BO3StatusLabel").Text;

                ClickElement(boSwitch);
                string toggledOff = FindUIElement("BO3StatusLabel").Text;

                boSwitch.ShouldNotBeNull();
                boSwitch.Displayed.ShouldBeTrue();
                boSwitch.Enabled.ShouldBeTrue();
                toggledOn.ShouldBe("Best of 3");
                toggledOff.ShouldBe("Best of 1");
            }
            finally { ResetBOSwitch(); }
        }
    }
}
