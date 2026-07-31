namespace PokemonBattleJournal.Tests.Services
{
    public class TrainerOperationsTests
    {
        private TrainerOperations _sut = null!;
        private SqliteConnectionFactory _mockFactory = null!;
        private ILogger _mockLogger = null!;

        [SetUp]
        public void SetUp()
        {
            _mockFactory = Substitute.For<SqliteConnectionFactory>(Substitute.For<ILogger<SqliteConnectionFactory>>(), Substitute.For<ILimitlessMetaService>());
            _mockLogger = Substitute.For<ILogger>();
            _sut = new TrainerOperations(_mockFactory, _mockLogger);
        }

        [Test]
        public void GetByNameAsync_EmptyName_ThrowsArgumentException()
        {
            // Act & Assert
            _ = Should.Throw<ArgumentException>(() =>
                _sut.GetByNameAsync(string.Empty));
        }

        [Test]
        public void GetByNameAsync_NullName_ThrowsArgumentException()
        {
            // Act & Assert
            _ = Should.Throw<ArgumentException>(() =>
                _sut.GetByNameAsync(null!));
        }

        [Test]
        public void SaveAsync_EmptyName_ThrowsArgumentException()
        {
            // Act & Assert
            _ = Should.Throw<ArgumentException>(() =>
                _sut.SaveAsync(string.Empty));
        }

        [Test]
        public void SaveAsync_NullName_ThrowsArgumentException()
        {
            // Act & Assert
            _ = Should.Throw<ArgumentException>(() =>
                _sut.SaveAsync(null!));
        }

        [Test]
        public void DeleteAsync_NullTrainer_ThrowsArgumentNullException()
        {
            // Act & Assert
            _ = Should.Throw<ArgumentNullException>(() =>
                _sut.DeleteAsync(null!));
        }

        [Test]
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
