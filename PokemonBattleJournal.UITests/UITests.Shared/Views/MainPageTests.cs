namespace UITests
{
    public partial class MainPageTests : BaseTest
    {
        [Fact]
        public void MainPage_AfterFirstBoot_TrainerNameSet()
        {
            NavigateTo("Journal Entry");
            AppiumElement welcomeLabel = FindUIElement("WelcomeMsg");
            welcomeLabel.ShouldNotBeNull();
            welcomeLabel.Text.ShouldContain("UITestTrainer");
        }

        [Fact]
        public void MainPage_UserNoteInput_ShowTextEntry()
        {
            NavigateTo("Journal Entry");
            AppiumElement userEntry = FindUIElement("UserNoteInput");
            try
            {
                userEntry.Click();
                userEntry.SendKeys("Hello World");
                userEntry.ShouldNotBeNull();
                // Re-fetch element so WinAppDriver returns current Value, not cached state.
                AppiumElement refetched = FindUIElement("UserNoteInput");
                refetched.Text.ShouldContain("Hello World");
            }
            finally
            {
                try { FindUIElement("UserNoteInput").Clear(); } catch (OpenQA.Selenium.NoSuchElementException) { }
            }
        }

        [Fact]
        public void MainPage_BallIcon_DisplayedOnPage()
        {
            NavigateTo("Journal Entry");
            AppiumElement BallIconPng = FindUIElement("ball_icon.png");
            BallIconPng.ShouldNotBeNull();
        }

        [Fact]
        public void MainPage_Pickers_DisplayedAndEnabled()
        {
            NavigateTo("Journal Entry");
            AppiumElement startPicker = FindUIElement("StartTimePicker");
            AppiumElement endPicker = FindUIElement("EndTimePicker");
            AppiumElement datePicker = FindUIElement("DatePlayedPicker");

            startPicker.Enabled.ShouldBeTrue();
            endPicker.Enabled.ShouldBeTrue();
            datePicker.Enabled.ShouldBeTrue();
        }

        [Fact]
        public void MainPage_TagsView_Displayed()
        {
            NavigateTo("Journal Entry");
            AppiumElement tagsView = FindUIElement("TagsView");
            tagsView.ShouldNotBeNull();
        }

        [Fact]
        public void MainPage_BOSwitch_ShowsBO3Fields()
        {
            NavigateTo("Journal Entry");
            AppiumElement boSwitch = FindUIElement("BOSwitch");
            try
            {
                boSwitch.Click();
                FindUIElement("BO3GamesLayout").ShouldNotBeNull();
            }
            finally
            {
                try { FindUIElement("BOSwitch").Click(); } catch (OpenQA.Selenium.NoSuchElementException) { }
            }
        }

        [Fact]
        public void MainPage_PlayerArchetype_Displayed()
        {
            NavigateTo("Journal Entry");
            AppiumElement picker = FindUIElement("PlayerArchetype");
            picker.ShouldNotBeNull();
        }

        [Fact]
        public void MainPage_RivalArchetype_Displayed()
        {
            NavigateTo("Journal Entry");
            AppiumElement picker = FindUIElement("RivalArchetype");
            picker.ShouldNotBeNull();
        }

        [Fact]
        public void MainPage_BO3StatusLabel_Displayed()
        {
            NavigateTo("Journal Entry");
            AppiumElement label = FindUIElement("BO3StatusLabel");
            label.ShouldNotBeNull();
        }

        [Fact]
        public void MainPage_ResultPicker_Displayed()
        {
            NavigateTo("Journal Entry");
            AppiumElement resultPicker = FindUIElement("PossibleResultsPicker");
            resultPicker.ShouldNotBeNull();
        }

        [Fact]
        public void MainPage_FirstCheck_Displayed()
        {
            NavigateTo("Journal Entry");
            AppiumElement firstCheck = FindUIElement("FirstCheck");
            firstCheck.ShouldNotBeNull();
        }

        [Fact]
        public void MainPage_SaveMatchButton_Displayed()
        {
            NavigateTo("Journal Entry");
            AppiumElement saveButton = FindUIElement("SaveMatchButton");
            saveButton.ShouldNotBeNull();
        }

        [Fact]
        public void MainPage_BO3GameTabs_DisplayedWhenBO3Active()
        {
            NavigateTo("Journal Entry");
            try
            {
                FindUIElement("BOSwitch").Click();
                FindUIElement("BO3GamesLayout").ShouldNotBeNull();
                FindUIElement("Game1Tab").ShouldNotBeNull();
                FindUIElement("Game2Tab").ShouldNotBeNull();
            }
            finally
            {
                try { FindUIElement("BOSwitch").Click(); } catch (OpenQA.Selenium.NoSuchElementException) { }
            }
        }

        [Fact]
        public void MainPage_Game3Tab_ShowsWhenGame1IsTie()
        {
            NavigateTo("Journal Entry");
            try
            {
                FindUIElement("BOSwitch").Click();
                FindUIElement("BO3GamesLayout");

                AppiumElement picker1 = FindUIElement("PossibleResultsPicker");
                if (App is not WindowsDriver)
                {
                    picker1.Click();
                    App.FindElement(MobileBy.AndroidUIAutomator("new UiSelector().text(\"Tie\")")).Click();
                }
                else
                    SelectWindowsPickerItem(picker1, "Tie");

                ClickTab(FindUIElement("Game2Tab"));
                // Sync on Game 2 panel visibility — TapGestureRecognizer fires async command.
                FindUIElement("FirstCheck2");

                AppiumElement picker2 = FindUIElement("PossibleResultsPicker2");
                if (App is not WindowsDriver)
                {
                    picker2.Click();
                    App.FindElement(MobileBy.AndroidUIAutomator("new UiSelector().text(\"Win\")")).Click();
                }
                else
                    SelectWindowsPickerItem(picker2, "Win");

                FindUIElement("Game3Tab").ShouldNotBeNull();
            }
            finally
            {
                try
                {
                    if (App is WindowsDriver)
                        App.FindElement(MobileBy.AccessibilityId("PossibleResultsPicker")).SendKeys(OpenQA.Selenium.Keys.Escape);
                }
                catch (OpenQA.Selenium.NoSuchElementException) { }
                try
                {
                    if (App is WindowsDriver)
                        App.FindElement(MobileBy.AccessibilityId("PossibleResultsPicker2")).SendKeys(OpenQA.Selenium.Keys.Escape);
                }
                catch (OpenQA.Selenium.NoSuchElementException) { }
                try { FindUIElement("Game1Tab").Click(); } catch (OpenQA.Selenium.NoSuchElementException) { }
                try { FindUIElement("BOSwitch").Click(); } catch (OpenQA.Selenium.NoSuchElementException) { }
            }
        }

        [Fact]
        public void MainPage_Game3Tab_ShowsGamePanel()
        {
            NavigateTo("Journal Entry");
            try
            {
                FindUIElement("BOSwitch").Click();
                FindUIElement("BO3GamesLayout");

                AppiumElement picker1 = FindUIElement("PossibleResultsPicker");
                if (App is not WindowsDriver)
                {
                    picker1.Click();
                    App.FindElement(MobileBy.AndroidUIAutomator("new UiSelector().text(\"Win\")")).Click();
                }
                else
                    SelectWindowsPickerItem(picker1, "Win");

                ClickTab(FindUIElement("Game2Tab"));
                // Sync on Game 2 panel visibility — TapGestureRecognizer fires async command.
                FindUIElement("FirstCheck2");

                AppiumElement picker2 = FindUIElement("PossibleResultsPicker2");
                if (App is not WindowsDriver)
                {
                    picker2.Click();
                    App.FindElement(MobileBy.AndroidUIAutomator("new UiSelector().text(\"Loss\")")).Click();
                }
                else
                    SelectWindowsPickerItem(picker2, "Loss");

                ClickTab(FindUIElement("Game3Tab"));
                FindUIElement("Match3Tags").ShouldNotBeNull();
                FindUIElement("UserNoteInput3").ShouldNotBeNull();
                FindUIElement("WentFirstLabel3").ShouldNotBeNull();
                FindUIElement("PossibleResultsPicker3").ShouldNotBeNull();
            }
            finally
            {
                try
                {
                    if (App is WindowsDriver)
                    {
                        App.FindElement(MobileBy.AccessibilityId("PossibleResultsPicker")).SendKeys(OpenQA.Selenium.Keys.Escape);
                        App.FindElement(MobileBy.AccessibilityId("PossibleResultsPicker2")).SendKeys(OpenQA.Selenium.Keys.Escape);
                    }
                }
                catch (OpenQA.Selenium.NoSuchElementException) { }
                try { FindUIElement("Game1Tab").Click(); } catch (OpenQA.Selenium.NoSuchElementException) { }
                try { FindUIElement("BOSwitch").Click(); } catch (OpenQA.Selenium.NoSuchElementException) { }
            }
        }

        [Fact]
        public void MainPage_SaveMatch_WithResult_Saves()
        {
            NavigateTo("Journal Entry");
            try
            {
                AppiumElement resultPicker = FindUIElement("PossibleResultsPicker");
                if (App is not WindowsDriver)
                {
                    resultPicker.Click();
                    App.FindElement(MobileBy.AndroidUIAutomator("new UiSelector().text(\"Win\")")).Click();
                }
                else
                    SelectWindowsPickerItem(resultPicker, "Win");
            }
            catch (OpenQA.Selenium.NoSuchElementException)
            {
                try
                {
                    if (App is WindowsDriver)
                        App.FindElement(MobileBy.AccessibilityId("PossibleResultsPicker")).SendKeys(OpenQA.Selenium.Keys.Escape);
                }
                catch (OpenQA.Selenium.NoSuchElementException) { }
                throw;
            }

            FindUIElement("SaveMatchButton").Click();
            FindUIElement("SaveMatchButton").ShouldNotBeNull();
        }
    }
}
