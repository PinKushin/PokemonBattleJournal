namespace PokemonBattleJournal.Tests.Services
{
    public class TagOperationsTests
    {
        private readonly TagOperations _sut;
        private readonly SqliteConnectionFactory _mockFactory;
        private readonly ILogger _mockLogger;

        public TagOperationsTests()
        {
            _mockFactory = Substitute.For<SqliteConnectionFactory>(Substitute.For<ILogger<SqliteConnectionFactory>>(), Substitute.For<ILimitlessMetaService>());
            _mockLogger = Substitute.For<ILogger>();
            _sut = new TagOperations(_mockFactory, _mockLogger);
        }

        [Fact]
        public void SaveAsync_EmptyTagName_ThrowsArgumentException()
        {
            // Act & Assert
            _ = Should.Throw<ArgumentException>(() =>
                _sut.SaveAsync(string.Empty, 1));
        }

        [Fact]
        public void SaveAsync_ZeroTrainerId_ThrowsArgumentException()
        {
            // Act & Assert
            _ = Should.Throw<ArgumentException>(() =>
                _sut.SaveAsync("Lucky", 0));
        }

        [Fact]
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
