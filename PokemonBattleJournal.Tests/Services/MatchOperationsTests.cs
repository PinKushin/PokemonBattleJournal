namespace PokemonBattleJournal.Tests.Services
{
    public class MatchOperationsTests
    {
        private MatchOperations _sut = null!;
        private SqliteConnectionFactory _mockFactory = null!;
        private ILogger _mockLogger = null!;

        [SetUp]
        public void SetUp()
        {
            _mockFactory = Substitute.For<SqliteConnectionFactory>(Substitute.For<ILogger<SqliteConnectionFactory>>(), Substitute.For<ILimitlessMetaService>());
            _mockLogger = Substitute.For<ILogger>();
            _sut = new MatchOperations(_mockFactory, _mockLogger);
        }

        [Test]
        public async Task SaveAsync_NullMatchEntry_ThrowsNullReferenceException()
        {
            // Arrange
            List<Game> games = [new Game()];

            // Act & Assert — SaveAsync does not null-check matchEntry, so it throws NullReferenceException
            _ = await Should.ThrowAsync<NullReferenceException>(() =>
                _sut.SaveAsync(null!, games));
        }

        [Test]
        public async Task SaveAsync_ZeroTrainerId_ThrowsArgumentException()
        {
            // Arrange
            MatchEntry matchEntry = new() { TrainerId = 0, PlayingId = 1, AgainstId = 1 };
            List<Game> games = [new Game()];

            // Act & Assert
            _ = await Should.ThrowAsync<ArgumentException>(() =>
                _sut.SaveAsync(matchEntry, games));
        }

        [Test]
        public async Task SaveAsync_ZeroPlayingId_ThrowsArgumentException()
        {
            // Arrange
            MatchEntry matchEntry = new() { TrainerId = 1, PlayingId = 0, AgainstId = 1 };
            List<Game> games = [new Game()];

            // Act & Assert
            _ = await Should.ThrowAsync<ArgumentException>(() =>
                _sut.SaveAsync(matchEntry, games));
        }

        [Test]
        public async Task SaveAsync_ZeroAgainstId_ThrowsArgumentException()
        {
            // Arrange
            MatchEntry matchEntry = new() { TrainerId = 1, PlayingId = 1, AgainstId = 0 };
            List<Game> games = [new Game()];

            // Act & Assert
            _ = await Should.ThrowAsync<ArgumentException>(() =>
                _sut.SaveAsync(matchEntry, games));
        }

        [Test]
        public async Task SaveAsync_EmptyGames_ThrowsArgumentException()
        {
            // Arrange
            MatchEntry matchEntry = new() { TrainerId = 1, PlayingId = 1, AgainstId = 1 };
            List<Game> games = [];

            // Act & Assert
            _ = await Should.ThrowAsync<ArgumentException>(() =>
                _sut.SaveAsync(matchEntry, games));
        }

        [Test]
        public void DeleteAsync_NullMatchEntry_ThrowsArgumentNullException()
        {
            // Act & Assert
            _ = Should.Throw<ArgumentNullException>(() =>
                _sut.DeleteAsync(null!));
        }

        [Test]
        public void DeleteAsync_ZeroId_ThrowsArgumentException()
        {
            // Arrange
            MatchEntry matchEntry = new() { Id = 0 };

            // Act & Assert
            _ = Should.Throw<ArgumentException>(() =>
                _sut.DeleteAsync(matchEntry));
        }
    }
}
