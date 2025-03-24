using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using Shouldly;
#pragma warning disable IDE0058 // Expression value is never used
#pragma warning disable IDE0130 // Namespace does not match folder structure

namespace PokemonBattleJournal.UI.Tests
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    public class MainPageTests : IDisposable
    {
        /// <summary>
        /// The AppiumDriver instance. You need to install Appium from npm and Windows Application Driver to run these tests.
        /// </summary>
        private static AppiumDriver? _driver;
        public static AppiumDriver App => _driver ?? throw new NullReferenceException("AppiumDriver is not initialized.");

        public MainPageTests()
        {
            AppiumOptions windowsOptions = new()
            {
                AutomationName = "Windows",
                PlatformName = "Windows",
                DeviceName = "WindowsPC",
                App = "D:\\source\\PinKushin\\PokemonBattleJournal\\PokemonBattleJournal\\bin\\Debug\\net9.0-windows10.0.19041.0\\win10-x64\\PokemonBattleJournal.exe" //Always update the path on new machine or repartition of OS
            };

            _driver = new WindowsDriver(windowsOptions);
        }

        [Fact]
        public void MainPage_UserNoteInput_ShowTextEntry()
        {
            // Arrange
            AppiumElement userEntry = App.FindElement(MobileBy.AccessibilityId("UserNoteInput"));

            // Act
            userEntry.SendKeys("Hello World");

            // Assert
            userEntry.ShouldNotBeNull();
            userEntry.Text.ShouldBe("Hello World");

        }

        [Fact]
        public void MainPage_BOSwitch_DisplayedAndToggled()
        {
            // Arrange
            const string id = "BOSwitch";
            AppiumElement BOSwitch = App.FindElement(MobileBy.AccessibilityId(id));

            // Act
            BOSwitch.Click();
            var toggledOn = BOSwitch.GetAttribute("Toggle.ToggleState");
            BOSwitch.Click();
            var toggledOff = BOSwitch.GetAttribute("Toggle.ToggleState");
            // Assert
            BOSwitch.ShouldNotBeNull();
            BOSwitch.Displayed.ShouldBeTrue();
            BOSwitch.Enabled.ShouldBeTrue();
            toggledOn.ShouldBe("1"); // "1" for toggled on
            toggledOff.ShouldBe("0");// "0" for toggled off

        }

        [Fact]
        public void MainPage_BallIcon_DisplayedOnPage()
        {
            // Arrange
            AppiumElement BallIconPng = App.FindElement(MobileBy.AccessibilityId("ball_icon.png"));
            // Act
            // Assert
            BallIconPng.ShouldNotBeNull();
            BallIconPng.Displayed.ShouldBeTrue();

        }

        public void Dispose()
        {
            App.Close();
            App.Quit();
            _driver?.Quit();
            GC.SuppressFinalize(this);
        }
    }
}