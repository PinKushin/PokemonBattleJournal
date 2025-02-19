using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using Shouldly;

#pragma warning disable IDE0130 // Namespace does not match folder structure

namespace PokemonBattleJournal.UI.Tests
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
	public class MainPageTests
	{
		private static AppiumDriver? _driver;
		public static AppiumDriver App => _driver ?? throw new NullReferenceException("AppiumDriver is not initialized.");

		public MainPageTests()
		{
			AppiumOptions windowsOptions = new()
			{
				AutomationName = "Windows",
				PlatformName = "Windows",
				DeviceName = "WindowsPC",
				App = "C:\\Users\\pinku\\source\\repos\\PinKushin\\PokemonBattleJournal\\PokemonBattleJournal\\bin\\Debug\\net9.0-windows10.0.19041.0\\win10-x64\\PokemonBattleJournal.exe"
			};

			_driver = new WindowsDriver(windowsOptions);
		}

		~MainPageTests()
		{
			_driver?.Quit();
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
			App.Quit();
		}

		[Fact]
		public void MainPage_BOSwitch_DisplayedOnPage()
		{
			// Arrange
			const string id = "BOSwitch";
			AppiumElement BOSwitch = App.FindElement(MobileBy.AccessibilityId(id));

			// Act
			BOSwitch.Click();
			// Assert
			_ = BOSwitch.ShouldNotBeNull();
			BOSwitch.Displayed.ShouldBeTrue();
			BOSwitch.Enabled.ShouldBeTrue();
			App.Quit();
		}

		[Fact]
		public void MainPage_BallIcon_DisplayedOnPage()
		{
			// Arrange
			AppiumElement BallIconPng = App.FindElement(MobileBy.AccessibilityId("ball_icon.png"));
			// Act
			// Assert
			_ = BallIconPng.ShouldNotBeNull();
			BallIconPng.Displayed.ShouldBeTrue();
			App.Quit();
		}
	}
}