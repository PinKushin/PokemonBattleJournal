namespace PokemonBattleJournal.Tests.Services
{
    public class TrainerSwitchServiceTests
    {
        private TrainerSwitchService _sut = null!;
        private ISqliteConnectionFactory _mockConnectionFactory = null!;
        private ITrainerOperations _mockTrainerOps = null!;
        private ILogger<TrainerSwitchService> _mockLogger = null!;

        [SetUp]
        public void SetUp()
        {
            _mockLogger = Substitute.For<ILogger<TrainerSwitchService>>();
            _mockTrainerOps = Substitute.For<ITrainerOperations>();
            _mockConnectionFactory = Substitute.For<ISqliteConnectionFactory>();
            _mockConnectionFactory.Trainers.Returns(_mockTrainerOps);

            _sut = new TrainerSwitchService(_mockConnectionFactory, _mockLogger);
        }

        [Test]
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

        [Test]
        public async Task SwitchToAsync_SetsActiveTrainer()
        {
            var trainer = new Trainer { Id = 1, Name = "Ash" };
            _mockTrainerOps.SetActiveAsync(trainer).Returns(Task.CompletedTask);

            await _sut.SwitchToAsync(trainer);

            _sut.ActiveTrainer.ShouldBe(trainer);
        }

        [Test]
        public async Task SwitchToAsync_FiresTrainerChangedEvent()
        {
            var trainer = new Trainer { Id = 1, Name = "Ash" };
            _mockTrainerOps.SetActiveAsync(trainer).Returns(Task.CompletedTask);
            Trainer? eventTrainer = null;
            _sut.TrainerChanged += (_, t) => eventTrainer = t;

            await _sut.SwitchToAsync(trainer);

            eventTrainer.ShouldNotBeNull();
            eventTrainer.ShouldBe(trainer);
        }

        [Test]
        public async Task SwitchToAsync_WithNullName_DoesNotThrow()
        {
            var trainer = new Trainer { Id = 1, Name = null };
            _mockTrainerOps.SetActiveAsync(trainer).Returns(Task.CompletedTask);

            await Should.NotThrowAsync(() => _sut.SwitchToAsync(trainer));
        }

        [Test]
        public async Task InitializeAsync_SetsActiveTrainerFromDb()
        {
            var trainer = new Trainer { Id = 2, Name = "Misty", IsActive = true };
            _mockTrainerOps.GetActiveAsync().Returns(Task.FromResult<Trainer?>(trainer));

            await _sut.InitializeAsync();

            _sut.ActiveTrainer.ShouldBe(trainer);
        }
    }
}
