namespace PokemonBattleJournal.Tests.Services
{
    public class MatchOperationsTests
    {
        private readonly MatchOperations _sut;
        private readonly SqliteConnectionFactory _mockFactory;
        private readonly ILogger _mockLogger;

        public MatchOperationsTests()
        {
            _mockFactory = Substitute.For<SqliteConnectionFactory>(Substitute.For<ILogger<SqliteConnectionFactory>>());
            _mockLogger = Substitute.For<ILogger>();
            _sut = new MatchOperations(_mockFactory, _mockLogger);
        }

        [Fact]
        public async Task SaveAsync_NullMatchEntry_ThrowsNullReferenceException()
        {
            // Arrange
            List<Game> games = [new Game()];

            // Act & Assert — SaveAsync does not null-check matchEntry, so it throws NullReferenceException
            _ = await Should.ThrowAsync<NullReferenceException>(() =>
                _sut.SaveAsync(null!, games));
        }

        [Fact]
        public async Task SaveAsync_ZeroTrainerId_ThrowsArgumentException()
        {
            // Arrange
            MatchEntry matchEntry = new() { TrainerId = 0, PlayingId = 1, AgainstId = 1 };
            List<Game> games = [new Game()];

            // Act & Assert
            _ = await Should.ThrowAsync<ArgumentException>(() =>
                _sut.SaveAsync(matchEntry, games));
        }

        [Fact]
        public async Task SaveAsync_ZeroPlayingId_ThrowsArgumentException()
        {
            // Arrange
            MatchEntry matchEntry = new() { TrainerId = 1, PlayingId = 0, AgainstId = 1 };
            List<Game> games = [new Game()];

            // Act & Assert
            _ = await Should.ThrowAsync<ArgumentException>(() =>
                _sut.SaveAsync(matchEntry, games));
        }

        [Fact]
        public async Task SaveAsync_ZeroAgainstId_ThrowsArgumentException()
        {
            // Arrange
            MatchEntry matchEntry = new() { TrainerId = 1, PlayingId = 1, AgainstId = 0 };
            List<Game> games = [new Game()];

            // Act & Assert
            _ = await Should.ThrowAsync<ArgumentException>(() =>
                _sut.SaveAsync(matchEntry, games));
        }

        [Fact]
        public async Task SaveAsync_EmptyGames_ThrowsArgumentException()
        {
            // Arrange
            MatchEntry matchEntry = new() { TrainerId = 1, PlayingId = 1, AgainstId = 1 };
            List<Game> games = [];

            // Act & Assert
            _ = await Should.ThrowAsync<ArgumentException>(() =>
                _sut.SaveAsync(matchEntry, games));
        }

        [Fact]
        public void DeleteAsync_NullMatchEntry_ThrowsArgumentNullException()
        {
            // Act & Assert
            _ = Should.Throw<ArgumentNullException>(() =>
                _sut.DeleteAsync(null!));
        }

        [Fact]
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
