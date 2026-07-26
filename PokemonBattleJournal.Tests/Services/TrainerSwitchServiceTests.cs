namespace PokemonBattleJournal.Tests.Services
{
    public class TrainerSwitchServiceTests
    {
        private readonly TrainerSwitchService _sut;
        private readonly ISqliteConnectionFactory _mockConnectionFactory;
        private readonly ITrainerOperations _mockTrainerOps;
        private readonly ILogger<TrainerSwitchService> _mockLogger;

        public TrainerSwitchServiceTests()
        {
            _mockLogger = Substitute.For<ILogger<TrainerSwitchService>>();
            _mockTrainerOps = Substitute.For<ITrainerOperations>();
            _mockConnectionFactory = Substitute.For<ISqliteConnectionFactory>();
            _mockConnectionFactory.Trainers.Returns(_mockTrainerOps);

            _sut = new TrainerSwitchService(_mockConnectionFactory, _mockLogger);
        }

        [Fact]
        public async Task GetAllTrainersAsync_CallsTrainerOperationsGetAllAsync()
        {
            // Arrange
            var expected = new List<Trainer>
            {
                new() { Id = 1, Name = "Ash" },
                new() { Id = 2, Name = "Misty" }
            };
            _mockTrainerOps.GetAllAsync().Returns(Task.FromResult(expected));

            // Act
            var result = await _sut.GetAllTrainersAsync();

            // Assert
            result.ShouldBe(expected);
            await _mockTrainerOps.Received(1).GetAllAsync();
        }

        [Fact]
        public async Task SwitchToAsync_SetsActiveTrainer()
        {
            // Arrange
            var trainer = new Trainer { Id = 1, Name = "Ash" };

            // Act
            await _sut.SwitchToAsync(trainer);

            // Assert
            _sut.ActiveTrainer.ShouldBe(trainer);
        }

        [Fact]
        public async Task SwitchToAsync_FiresTrainerChangedEvent()
        {
            // Arrange
            var trainer = new Trainer { Id = 1, Name = "Ash" };
            Trainer? eventTrainer = null;
            _sut.TrainerChanged += (_, t) => eventTrainer = t;

            // Act
            await _sut.SwitchToAsync(trainer);

            // Assert
            eventTrainer.ShouldNotBeNull();
            eventTrainer.ShouldBe(trainer);
        }

        [Fact]
        public async Task SwitchToAsync_WithNullName_DoesNotThrow()
        {
            // Arrange
            var trainer = new Trainer { Id = 1, Name = null };

            // Act & Assert
            await Should.NotThrowAsync(() => _sut.SwitchToAsync(trainer));
        }
    }
}
