namespace PokemonBattleJournal.Tests.Services
{
    public class ArchetypeOperationsTests
    {
        private readonly ArchetypeOperations _sut;
        private readonly SqliteConnectionFactory _mockFactory;
        private readonly ILogger _mockLogger;

        public ArchetypeOperationsTests()
        {
            _mockFactory = Substitute.For<SqliteConnectionFactory>(Substitute.For<ILogger<SqliteConnectionFactory>>());
            _mockLogger = Substitute.For<ILogger>();
            _sut = new ArchetypeOperations(_mockFactory, _mockLogger);
        }

        [Fact]
        public void SaveAsync_EmptyName_ThrowsArgumentException()
        {
            // Act & Assert
            _ = Should.Throw<ArgumentException>(() =>
                _sut.SaveAsync(string.Empty, "icon.png", 1));
        }

        [Fact]
        public void SaveAsync_EmptyImagePath_ThrowsArgumentException()
        {
            // Act & Assert
            _ = Should.Throw<ArgumentException>(() =>
                _sut.SaveAsync("Fire", string.Empty, 1));
        }

        [Fact]
        public void SaveAsync_ZeroTrainerId_ThrowsArgumentException()
        {
            // Act & Assert
            _ = Should.Throw<ArgumentException>(() =>
                _sut.SaveAsync("Fire", "icon.png", 0));
        }

        [Fact]
        public void DeleteAsync_NullArchetype_ThrowsArgumentNullException()
        {
            // Act & Assert
            _ = Should.Throw<ArgumentNullException>(() =>
                _sut.DeleteAsync(null!));
        }

        [Fact]
        public void DeleteAsync_ZeroId_ThrowsArgumentException()
        {
            // Arrange
            Archetype archetype = new() { Id = 0 };

            // Act & Assert
            _ = Should.Throw<ArgumentException>(() =>
                _sut.DeleteAsync(archetype));
        }
    }
}
