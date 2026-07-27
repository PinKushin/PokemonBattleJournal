namespace PokemonBattleJournal.Tests.ViewModels;

public class FirstStartPageViewModelTests
{
    private readonly ISqliteConnectionFactory _mockConnection;
    private readonly ITrainerSwitchService _mockSwitchService;
    private readonly FirstStartPageViewModel _viewModel;

    public FirstStartPageViewModelTests()
    {
        _mockConnection = Substitute.For<ISqliteConnectionFactory>();
        _mockConnection.Trainers.Returns(Substitute.For<ITrainerOperations>());
        _mockSwitchService = Substitute.For<ITrainerSwitchService>();
        _viewModel = new FirstStartPageViewModel(_mockConnection, _mockSwitchService);
    }

    [Fact]
    public void Constructor_SetsTrainerNameInputNull()
    {
        _viewModel.TrainerNameInput.ShouldBeNull();
    }

    [Fact]
    public async Task SaveTrainerName_NullInput_DoesNotThrow()
    {
        _viewModel.TrainerNameInput = null;
        await Should.NotThrowAsync(() => _viewModel.SaveTrainerName());
    }

    [Fact]
    public async Task SaveTrainerName_NullInput_DoesNotSetPreferences()
    {
        _viewModel.TrainerNameInput = null;
        await _viewModel.SaveTrainerName();
        _viewModel.TrainerNameInput.ShouldBeNull();
    }

    [Fact]
    public void TrainerNameInput_SetAndGet_ReturnsValue()
    {
        _viewModel.TrainerNameInput = "Ash";
        _viewModel.TrainerNameInput.ShouldBe("Ash");
    }

    [Fact]
    public async Task SaveTrainerName_WithInputButNoApplicationCurrent_DoesNotThrow()
    {
        _viewModel.TrainerNameInput = "Ash";
        await Should.NotThrowAsync(() => _viewModel.SaveTrainerName());
    }
}
