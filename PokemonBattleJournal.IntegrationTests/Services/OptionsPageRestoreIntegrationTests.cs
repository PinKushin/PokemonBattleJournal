using PokemonBattleJournal.IntegrationTests.Infrastructure;
using PokemonBattleJournal.Services.Export;
using PokemonBattleJournal.Services.Restore;
using PokemonBattleJournal.ViewModels;

namespace PokemonBattleJournal.IntegrationTests.Services;

/// <summary>
/// The OptionsPage restore path end to end: a real export, a real restore, a real database, and
/// the status line the user is left looking at.
/// </summary>
/// <remarks>
/// The ViewModel unit tests drive <c>ApplyRestoreAsync</c> against a mocked
/// <see cref="IRestoreService"/>, so they can only prove the sentence is right for the counts
/// they invented. These prove the counts themselves are the ones a real restore produces —
/// which is the half that would silently rot if <c>RestoreResult</c>'s semantics ever shifted.
///
/// Two factories per test on purpose: the backup has to come out of one database and land in
/// another, because restoring into the database it was exported from exercises the dedupe path
/// rather than the rebuild path.
/// </remarks>
public class OptionsPageRestoreIntegrationTests
{
    private TestSqliteConnectionFactory _source = null!;
    private TestSqliteConnectionFactory _target = null!;
    private ExportService _export = null!;
    private uint _sourceTrainerId;
    private uint _sourceDragapultId;
    private uint _sourceOtherId;

    [SetUp]
    public async Task SetUp()
    {
        _source = NewFactory();
        _target = NewFactory();
        _export = new ExportService(_source, NullLogger<ExportService>.Instance);

        SQLiteAsyncConnection db = await _source.GetDatabaseAsync();

        Trainer trainer = new() { Name = "Ash", IsActive = true };
        _ = await db.InsertAsync(trainer);
        _sourceTrainerId = trainer.Id;

        Archetype dragapult = new() { Name = "Dragapult ex / Dusknoir", ImagePath = "dragapult.png", ImagePath2 = "dusknoir.png" };
        _ = await db.InsertAsync(dragapult);
        _sourceDragapultId = dragapult.Id;

        Archetype other = new() { Name = "Other", ImagePath = "substitute.png" };
        _ = await db.InsertAsync(other);
        _sourceOtherId = other.Id;
    }

    [TearDown]
    public async Task TearDown()
    {
        await _source.DisposeAsync();
        await _target.DisposeAsync();
    }

    /// <summary>
    /// Must return an empty list rather than an unstubbed null: ArchetypeOperations.GetAllAsync
    /// faults on null and returns [] out of its own catch, so a missing stub surfaces as
    /// archetypes quietly absent instead of as an error. See feedback_mock_returns_null_not_empty.
    /// </summary>
    private static TestSqliteConnectionFactory NewFactory()
    {
        ILimitlessMetaService meta = Substitute.For<ILimitlessMetaService>();
        meta.GetTopDecksAsync(Arg.Any<int>()).Returns([]);
        return new TestSqliteConnectionFactory(meta);
    }

    /// <summary>
    /// Builds the ViewModel over a real <see cref="RestoreService"/> pointed at <paramref name="factory"/>.
    /// </summary>
    private static OptionsPageViewModel BuildViewModel(TestSqliteConnectionFactory factory)
    {
        ITrainerSwitchService switchService = Substitute.For<ITrainerSwitchService>();
        MainPageViewModel mainVm = new(
            NullLogger<MainPageViewModel>.Instance,
            factory,
            Substitute.For<IMatchResultsCalculatorFactory>(),
            switchService,
            Substitute.For<IErrorHandler>());
        AppShellViewModel shellVm = new(switchService, mainVm, NullLogger<AppShellViewModel>.Instance);

        return new OptionsPageViewModel(
            NullLogger<OptionsPageViewModel>.Instance,
            factory,
            switchService,
            shellVm,
            Substitute.For<ITrainerHillImportService>(),
            new ExportService(factory, NullLogger<ExportService>.Instance),
            new RestoreService(factory, NullLogger<RestoreService>.Instance, new PokemonBattleJournal.Logging.SentryPerformanceMonitor()),
            Substitute.For<IErrorHandler>(),
            new PokemonBattleJournal.Logging.SentryPerformanceMonitor());
    }

    private async Task SeedSourceMatchAsync(DateTime startTime, params Game[] games)
    {
        MatchEntry match = new()
        {
            TrainerId = _sourceTrainerId,
            PlayingId = _sourceDragapultId,
            AgainstId = _sourceOtherId,
            Result = MatchResult.Win,
            DatePlayed = startTime.Date,
            StartTime = startTime,
            EndTime = startTime.AddMinutes(20),
        };
        (await _source.Matches.SaveAsync(match, [.. games]))
            .ShouldBeGreaterThan(0, "seeding a match must succeed or the test proves nothing");
    }

    /// <summary>
    /// The disaster-recovery case: a backup, a machine with nothing on it, and one sentence
    /// telling the user whether their history came back.
    /// </summary>
    [Test]
    public async Task ApplyRestoreAsync_FreshDatabase_RebuildsTheDataAndSaysSo()
    {
        await SeedSourceMatchAsync(new DateTime(2026, 7, 27, 19, 45, 24, DateTimeKind.Utc),
            new Game { Result = MatchResult.Win, Turn = 1, Notes = "clean start" });
        string backup = await _export.ExportBackupAsync();
        OptionsPageViewModel vm = BuildViewModel(_target);

        await vm.ApplyRestoreAsync(backup);

        vm.RestoreStatusMessage.ShouldBe("Restored 1 match, 1 trainer added");
        vm.HasRestoreStatus.ShouldBeTrue("the status line is the only feedback a restore gives");

        Trainer? restored = await _target.Trainers.GetByNameAsync("Ash");
        restored.ShouldNotBeNull();
        (await _target.Matches.GetByTrainerIdAsync(restored.Id)).Count.ShouldBe(1);

        // The page is displaying these while the restore writes underneath it. If they are not
        // re-read, a fresh install shows an empty trainer picker immediately after a successful
        // restore — which looks exactly like a restore that did nothing.
        vm.AllTrainers.ShouldContain(t => t.Name == "Ash",
            "the trainer picker on this page must show what was just restored");
        vm.AllArchetypes.ShouldContain(a => a.Name == "Dragapult ex / Dusknoir");
    }

    /// <summary>
    /// Restoring onto the machine the backup came from is the ordinary case, and it must not read
    /// as a failure — nor duplicate anything.
    /// </summary>
    [Test]
    public async Task ApplyRestoreAsync_SameDatabase_ReportsAlreadyPresentAndChangesNothing()
    {
        await SeedSourceMatchAsync(new DateTime(2026, 7, 27, 19, 45, 24, DateTimeKind.Utc),
            new Game { Result = MatchResult.Win, Turn = 1, Notes = "clean start" });
        string backup = await _export.ExportBackupAsync();
        OptionsPageViewModel vm = BuildViewModel(_source);

        await vm.ApplyRestoreAsync(backup);

        vm.RestoreStatusMessage.ShouldBe("Restored 0 matches, 1 already present");
        (await _source.Matches.GetByTrainerIdAsync(_sourceTrainerId)).Count
            .ShouldBe(1, "a re-restore must not duplicate the history");
    }

    /// <summary>
    /// A file this build cannot read must say why, not report zero counts.
    /// </summary>
    /// <remarks>
    /// The reason has to survive the whole trip from the service to the label, because it is the
    /// only thing distinguishing "update the app" from "you picked the wrong file".
    /// </remarks>
    [Test]
    public async Task ApplyRestoreAsync_NewerEnvelopeVersion_ShowsTheRefusalReason()
    {
        await SeedSourceMatchAsync(new DateTime(2026, 7, 27, 19, 45, 24, DateTimeKind.Utc),
            new Game { Result = MatchResult.Win, Turn = 1, Notes = "clean start" });
        string backup = (await _export.ExportBackupAsync()).Replace("\"version\": 1", "\"version\": 99");
        OptionsPageViewModel vm = BuildViewModel(_target);

        await vm.ApplyRestoreAsync(backup);

        vm.RestoreStatusMessage.ShouldContain("newer version",
            customMessage: "the refusal reason must reach the label rather than being counted as a generic failure");
        (await _target.Trainers.GetAllAsync()).ShouldBeEmpty("a refused backup must not half-apply");
    }

    /// <summary>
    /// Garbage in must not throw out of the ViewModel — the command has a catch, but relying on
    /// it would surface a modal for a case the service already handles.
    /// </summary>
    [Test]
    public async Task ApplyRestoreAsync_Garbage_ReportsInsteadOfThrowing()
    {
        OptionsPageViewModel vm = BuildViewModel(_target);

        await vm.ApplyRestoreAsync("this is not json");

        vm.RestoreStatusMessage.ShouldNotBeEmpty();
        vm.RestoreStatusMessage.ShouldNotBe("Restore failed",
            "the service handles an unparsable file itself, so this must not land in the catch");
        vm.IsBusyMutating.ShouldBeFalse();
    }
}
