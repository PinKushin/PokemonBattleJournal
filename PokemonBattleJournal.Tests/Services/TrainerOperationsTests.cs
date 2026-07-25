namespace PokemonBattleJournal.Tests.Services
{
    public class TrainerOperationsTests
    {
        private readonly TrainerOperations _sut;
        private readonly SqliteConnectionFactory _mockFactory;
        private readonly ILogger _mockLogger;

        public TrainerOperationsTests()
        {
            _mockFactory = Substitute.For<SqliteConnectionFactory>(Substitute.For<ILogger<SqliteConnectionFactory>>(), Substitute.For<ILimitlessMetaService>());
            _mockLogger = Substitute.For<ILogger>();
            _sut = new TrainerOperations(_mockFactory, _mockLogger);
        }

        [Fact]
        public void GetByNameAsync_EmptyName_ThrowsArgumentException()
        {
            // Act & Assert
            _ = Should.Throw<ArgumentException>(() =>
                _sut.GetByNameAsync(string.Empty));
        }

        [Fact]
        public void GetByNameAsync_NullName_ThrowsArgumentException()
        {
            // Act & Assert
            _ = Should.Throw<ArgumentException>(() =>
                _sut.GetByNameAsync(null!));
        }

        [Fact]
        public void SaveAsync_EmptyName_ThrowsArgumentException()
        {
            // Act & Assert
            _ = Should.Throw<ArgumentException>(() =>
                _sut.SaveAsync(string.Empty));
        }

        [Fact]
        public void SaveAsync_NullName_ThrowsArgumentException()
        {
            // Act & Assert
            _ = Should.Throw<ArgumentException>(() =>
                _sut.SaveAsync(null!));
        }

        [Fact]
        public void DeleteAsync_NullTrainer_ThrowsArgumentNullException()
        {
            // Act & Assert
            _ = Should.Throw<ArgumentNullException>(() =>
                _sut.DeleteAsync(null!));
        }

        [Fact]
        public void DeleteAsync_ZeroId_ThrowsArgumentException()
        {
            // Arrange
            Trainer trainer = new() { Id = 0 };

            // Act & Assert
            _ = Should.Throw<ArgumentException>(() =>
                _sut.DeleteAsync(trainer));
        }
    }
}
