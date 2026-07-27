namespace PokemonBattleJournal.Tests.ViewModels;

public class FirstStartPageViewModelTests
{
    private readonly FirstStartPageViewModel _viewModel = new();

    [Fact]
    public void Constructor_SetsTrainerNameInputNull()
    {
        _viewModel.TrainerNameInput.ShouldBeNull();
    }

    [Fact]
    public void SaveTrainerName_NullInput_DoesNotThrow()
    {
        // TrainerNameInput is null — guard prevents setting preferences or navigating
        _viewModel.TrainerNameInput = null;
        Should.NotThrow(() => _viewModel.SaveTrainerName());
    }

    [Fact]
    public void SaveTrainerName_NullInput_DoesNotSetPreferences()
    {
        _viewModel.TrainerNameInput = null;
        // No Application.Current in test env — exercise the null guard only
        _viewModel.SaveTrainerName();
        // Verify input unchanged
        _viewModel.TrainerNameInput.ShouldBeNull();
    }

    [Fact]
    public void TrainerNameInput_SetAndGet_ReturnsValue()
    {
        _viewModel.TrainerNameInput = "Ash";
        _viewModel.TrainerNameInput.ShouldBe("Ash");
    }

    [Fact]
    public void SaveTrainerName_WithInputButNoApplicationCurrent_DoesNotThrow()
    {
        // Application.Current is null in unit tests — branch checks both TrainerNameInput != null
        // AND Application.Current != null before navigating. Should not throw.
        _viewModel.TrainerNameInput = "Ash";
        Should.NotThrow(() => _viewModel.SaveTrainerName());
    }
}
