using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using Shouldly;

namespace PokemonBattleJournal.UI.Tests.Windows.Views
{
	public class MainPageTests
	{
		private static AppiumDriver? _driver;
		public static AppiumDriver App => _driver ?? throw new NullReferenceException("AppiumDriver is not initialized.");

		public MainPageTests()
		{
			var windowsOptions = new AppiumOptions
			{
				AutomationName = "Windows",
				PlatformName = "Windows",
				DeviceName = "WindowsPC",
				App = "C:\\Users\\pinku\\source\\repos\\PinKushin\\PokemonBattleJournal\\PokemonBattleJournal\\bin\\Debug\\net9.0-windows10.0.19041.0\\win10-x64\\PokemonBattleJournal.exe"
			};

			_driver = new WindowsDriver(windowsOptions);
			_driver.ShouldNotBeNull();
		}

		~MainPageTests()
		{
			_driver?.Quit();
			App.Dispose();
		}

		[Fact]
		public void MainPage_BOSwitch_Exists()
		{
			// Arrange
			var id = "UserNoteInput";
			var element = App.FindElement(MobileBy.AccessibilityId(id));
			// Act
			//var name = element.GetAttribute("Name");
			element.SendKeys("Hello World");
			// Assert
			//name.ShouldBe("ball_icon.png");
			element.ShouldNotBeNull();
			element.Text.ShouldBe("Hello World");
		}
	}
}