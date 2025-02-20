using System.Collections.ObjectModel;
#pragma warning disable IDE0058 // Expression value is never used
namespace PokemonBattleJournal.Tests.ViewModels
{
	public class MainPageViewModelTests
	{
		private readonly MainPageViewModel _viewModel;

		public MainPageViewModelTests()
		{
			// Create a mock logger
			Logger<MainPageViewModel> mockLogger = new(new LoggerFactory());
			// SUT
			_viewModel = new MainPageViewModel(logger: mockLogger);
		}

		[Fact]
		public void MainPageViewModel_WhenConstructed_ShouldNotBeNull()
		{
			// Assert

			_viewModel.ShouldNotBeNull();
			_viewModel.Archetypes.ShouldNotBeNull();
			_viewModel.TagCollection.ShouldNotBeNull();
			_viewModel.WelcomeMsg.ShouldNotBeNull();
			_viewModel.WelcomeMsg.ShouldBe("Welcome " + _viewModel.TrainerName);
			_viewModel.CurrentDateTimeDisplay.ShouldNotBeNull();
			_viewModel.TrainerName.ShouldNotBeNull();
			_viewModel.BO3Toggle.ShouldBe(false);
			_viewModel.ShouldBeOfType<MainPageViewModel>();
			_viewModel.NameInput.ShouldBeNull();
			_viewModel.TagsSelected.ShouldBeNull();
			_viewModel.Match2TagsSelected.ShouldBeNull();
			_viewModel.Match3TagsSelected.ShouldBeNull();
			_viewModel.UserNoteInput.ShouldBeNull();
			_viewModel.UserNoteInput2.ShouldBeNull();
			_viewModel.UserNoteInput3.ShouldBeNull();
		}

		[Fact]
		public void MainPageViewModel_CreateMatchEntry_BO3MatchEntryCreated()
		{
			// Arrange
			_viewModel.PlayerSelected = new("Other", "ball_icon.png");
			_viewModel.RivalSelected = new("Other", "ball_icon.png");
			_viewModel.DatePlayed = DateTime.Now;
			_viewModel.TagsSelected = new ObservableCollection<object>() { "Early Start" };
			_viewModel.Match2TagsSelected = new ObservableCollection<object>() { "Behind Early" };
			_viewModel.Match3TagsSelected = new ObservableCollection<object>() { "Donked Rival", "Early Start" };
			_viewModel.Result = "Win";
			_viewModel.Result2 = "Loss";
			_viewModel.Result3 = "Win";
			_viewModel.UserNoteInput = "Test Note";
			_viewModel.UserNoteInput2 = "Test Note 2";
			_viewModel.UserNoteInput3 = "Test Note 3";
			_viewModel.FirstCheck = true;
			_viewModel.FirstCheck2 = false;
			_viewModel.FirstCheck3 = true;
			_viewModel.BO3Toggle = true;
			// Act
			MatchEntry? matchentry = _viewModel.CreateMatchEntry();
			// Assert
			matchentry.ShouldNotBeNull();
			matchentry.Playing.ShouldBeAssignableTo<Archetype>();
			matchentry.Against.ShouldBeAssignableTo<Archetype>();
			matchentry.Game1.Result.ShouldBe("Win");
			matchentry.Game2!.Result.ShouldBe("Loss");
			matchentry.Game3!.Result.ShouldBe("Win");
			matchentry.Game1.Turn.ShouldBe(1);
			matchentry.Game2.Turn.ShouldBe(2);
			matchentry.Game3.Turn.ShouldBe(1);
			matchentry.Game1.Tags?.ShouldContain("Early Start");
			matchentry.Game2.Tags?.ShouldContain("Behind Early");
			matchentry.Game3.Tags?.ShouldContain("Donked Rival");
			matchentry.Game3.Tags?.ShouldContain("Early Start");
			matchentry.Result.ShouldBe("Win");
			_viewModel.BO3Toggle.ShouldBe(false);
			_viewModel.Result.ShouldBe("");
			_viewModel.Result2.ShouldBe("");
			_viewModel.Result3.ShouldBe("");
			_viewModel.UserNoteInput.ShouldBeNull();
			_viewModel.UserNoteInput2.ShouldBeNull();
			_viewModel.UserNoteInput3.ShouldBeNull();
			_viewModel.TagsSelected.ShouldBeNull();
			_viewModel.Match2TagsSelected.ShouldBeNull();
			_viewModel.Match3TagsSelected.ShouldBeNull();
			_viewModel.FirstCheck.ShouldBe(false);
			_viewModel.FirstCheck2.ShouldBe(false);
			_viewModel.FirstCheck3.ShouldBe(false);
			_viewModel.PlayerSelected.ShouldBeNull();
			_viewModel.RivalSelected.ShouldBeNull();
		}
	}
}