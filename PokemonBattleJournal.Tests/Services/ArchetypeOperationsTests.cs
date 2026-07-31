namespace PokemonBattleJournal.Tests.Services
{
    public class ArchetypeOperationsTests
    {
        private ArchetypeOperations _sut = null!;
        private SqliteConnectionFactory _mockFactory = null!;
        private ILogger _mockLogger = null!;

        [SetUp]
        public void SetUp()
        {
            _mockFactory = Substitute.For<SqliteConnectionFactory>(Substitute.For<ILogger<SqliteConnectionFactory>>(), Substitute.For<ILimitlessMetaService>());
            _mockLogger = Substitute.For<ILogger>();
            _sut = new ArchetypeOperations(_mockFactory, _mockLogger, Substitute.For<ILimitlessMetaService>());
        }

        [Test]
        public void SaveAsync_EmptyName_ThrowsArgumentException()
        {
            // Act & Assert
            _ = Should.Throw<ArgumentException>(() =>
                _sut.SaveAsync(string.Empty, "icon.png", 1));
        }

        [Test]
        public void SaveAsync_EmptyImagePath_ThrowsArgumentException()
        {
            // Act & Assert
            _ = Should.Throw<ArgumentException>(() =>
                _sut.SaveAsync("Fire", string.Empty, 1));
        }

        [Test]
        public void SaveAsync_ZeroTrainerId_ThrowsArgumentException()
        {
            // Act & Assert
            _ = Should.Throw<ArgumentException>(() =>
                _sut.SaveAsync("Fire", "icon.png", 0));
        }

        [Test]
        public void DeleteAsync_NullArchetype_ThrowsArgumentNullException()
        {
            // Act & Assert
            _ = Should.Throw<ArgumentNullException>(() =>
                _sut.DeleteAsync(null!));
        }

        [Test]
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
