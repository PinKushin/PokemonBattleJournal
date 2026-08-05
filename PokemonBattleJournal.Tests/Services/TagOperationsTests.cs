namespace PokemonBattleJournal.Tests.Services
{
    public class TagOperationsTests
    {
        private TagOperations _sut = null!;
        private SqliteConnectionFactory _mockFactory = null!;
        private ILogger _mockLogger = null!;

        [SetUp]
        public void SetUp()
        {
            _mockFactory = Substitute.For<SqliteConnectionFactory>(Substitute.For<ILogger<SqliteConnectionFactory>>(), Substitute.For<ILimitlessMetaService>(), Substitute.For<IErrorHandler>());
            _mockLogger = Substitute.For<ILogger>();
            _sut = new TagOperations(_mockFactory, _mockLogger, Substitute.For<IErrorHandler>());
        }

        [Test]
        public void SaveAsync_EmptyTagName_ThrowsArgumentException()
        {
            // Act & Assert
            _ = Should.Throw<ArgumentException>(() =>
                _sut.SaveAsync(string.Empty, 1));
        }

        [Test]
        public void SaveAsync_ZeroTrainerId_ThrowsArgumentException()
        {
            // Act & Assert
            _ = Should.Throw<ArgumentException>(() =>
                _sut.SaveAsync("Lucky", 0));
        }

        [Test]
        public void DeleteAsync_ZeroId_ThrowsArgumentException()
        {
            // Arrange
            Tags tag = new() { Id = 0 };

            // Act & Assert
            _ = Should.Throw<ArgumentException>(() =>
                _sut.DeleteAsync(tag));
        }
    }
}
