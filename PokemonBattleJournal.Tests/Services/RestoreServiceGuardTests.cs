using PokemonBattleJournal.Services.Restore;
using PokemonBattleJournal.Tests.Fixtures;

namespace PokemonBattleJournal.Tests.Services
{
    /// <summary>
    /// The refusals <see cref="RestoreService.RestoreBackupAsync"/> makes before it opens a
    /// database.
    /// </summary>
    /// <remarks>
    /// Found by mutation testing rather than by review. The guard CONDITIONS were killed —
    /// every existing test evaluates them — but their bodies were NoCoverage, because every
    /// test passes a valid file and so enters each guard as false. Classic happy-path coverage:
    /// the branch is exercised in one direction only.
    ///
    /// These are unit tests, not integration ones, and that is the point being pinned as much
    /// as the messages: each guard returns BEFORE any connection is opened, so a refused file
    /// costs nothing and cannot half-touch the database. The factory mock asserts exactly that.
    /// </remarks>
    [TestFixture]
    public class RestoreServiceGuardTests
    {
        private ISqliteConnectionFactory _factory = null!;
        private RecordingLogger<RestoreService> _logger = null!;
        private RestoreService _sut = null!;

        [SetUp]
        public void SetUp()
        {
            _factory = Substitute.For<ISqliteConnectionFactory>();
            _logger = new RecordingLogger<RestoreService>();
            _sut = new RestoreService(_factory, _logger);
        }

        private void ShouldNotHaveTouchedTheDatabase() =>
            _ = _factory.DidNotReceive().GetDatabaseAsync();

        [Test]
        public async Task RestoreBackupAsync_EmptyString_ReportsTheFileIsEmpty()
        {
            RestoreResult result = await _sut.RestoreBackupAsync(string.Empty);

            result.Errors.ShouldHaveSingleItem().ShouldContain("empty");
            result.MatchesInserted.ShouldBe(0);
            ShouldNotHaveTouchedTheDatabase();
        }

        [Test]
        public async Task RestoreBackupAsync_WhitespaceOnly_IsTreatedAsEmpty()
        {
            // A file of blank lines is not a backup, and IsNullOrWhiteSpace is what decides it.
            // Weakening that to IsNullOrEmpty would send whitespace to the JSON parser instead.
            RestoreResult result = await _sut.RestoreBackupAsync("   \n\t  ");

            result.Errors.ShouldHaveSingleItem().ShouldContain("empty");
            ShouldNotHaveTouchedTheDatabase();
        }

        [Test]
        public async Task RestoreBackupAsync_OverTheSizeLimit_RefusesAndNamesTheLimit()
        {
            string huge = new('x', IRestoreService.MaxBackupBytes + 1);

            RestoreResult result = await _sut.RestoreBackupAsync(huge);

            string error = result.Errors.ShouldHaveSingleItem();
            error.ShouldContain("larger than");
            error.ShouldContain("8", Case.Insensitive);
            ShouldNotHaveTouchedTheDatabase();
        }

        [Test]
        public async Task RestoreBackupAsync_ExactlyAtTheSizeLimit_IsNotRefusedForSize()
        {
            // Pins the boundary as inclusive. A file exactly at the cap is allowed through and
            // fails later as unparsable, which is a different error — flipping > to >= here
            // would start rejecting a legitimate maximum-size backup.
            string atLimit = new('x', IRestoreService.MaxBackupBytes);

            RestoreResult result = await _sut.RestoreBackupAsync(atLimit);

            result.Errors.ShouldHaveSingleItem().ShouldNotContain("larger than");
        }

        [Test]
        public async Task RestoreBackupAsync_Unparsable_ReportsRatherThanThrowing()
        {
            RestoreResult result = await _sut.RestoreBackupAsync("{ this is not json");

            result.Errors.ShouldNotBeEmpty();
            ShouldNotHaveTouchedTheDatabase();
        }

        [Test]
        public async Task RestoreBackupAsync_Unparsable_LogsWhy()
        {
            // The log is the only evidence of WHICH file failed — the status line carries a
            // count. Mutation testing showed this warning could be deleted with nothing
            // noticing. See feedback_no_silent_guards.
            _ = await _sut.RestoreBackupAsync("{ this is not json");

            _logger.EntriesMatching(LogLevel.Warning, "parsed").ShouldNotBeEmpty(
                $"expected a Warning explaining the parse failure. Logged:{Environment.NewLine}{_logger.Dump()}");
        }
    }
}
