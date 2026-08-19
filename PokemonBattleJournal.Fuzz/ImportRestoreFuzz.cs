namespace PokemonBattleJournal.Fuzz;

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using PokemonBattleJournal.Interfaces;
using PokemonBattleJournal.Models;
using PokemonBattleJournal.Scraper.Interfaces;
using PokemonBattleJournal.Scraper.Models;
using PokemonBattleJournal.Services;
using PokemonBattleJournal.Services.Import;
using PokemonBattleJournal.Services.Restore;
using SQLite;

/// <summary>
/// Fuzzes the two file parsers that consume input this app did not produce and a stranger might:
/// <see cref="TrainerHillImportService"/> (a pasted TrainerHill JSON export) and
/// <see cref="RestoreService"/> (a backup envelope). These are the app's real attack surface — an
/// app whose whole job is importing and restoring user data files — and they are the two the pure
/// fuzz targets in <see cref="Program"/> deliberately left out, because each needs a live SQLite
/// connection per iteration and so runs at hundreds of executions a second rather than tens of
/// thousands. That is why this is a separate suite with its own budget (`PBJFUZZ_SUITE=importrestore`)
/// rather than a fifth mode sharing the fast run.
/// </summary>
/// <remarks>
/// <para>
/// <b>A fresh temp database every iteration, closed and deleted.</b> libFuzzer replays a crashing
/// input in isolation to confirm it, so a crash that depended on state left by a previous iteration
/// would not reproduce — it would look flaky, which is the one thing a fuzzer must never be. A new
/// database per call also stops handles and files accumulating across the millions of iterations a
/// real run does.
/// </para>
/// <para>
/// <b>The invariant is not "it did not throw".</b> Both services are built to swallow any parse
/// failure and report it, so an escaping exception is already a finding — the fuzzer catches it
/// with no help from here. What this adds is the property those catch blocks cannot check
/// themselves: <b>the count of matches the parser SAYS it wrote equals the number of match rows
/// actually in the database.</b> A parser that reports importing three matches while writing five,
/// or writes rows it then fails to count, is corrupting the user's journal silently — and no
/// round-trip test over app-produced files would catch it, because the app never produces the
/// malformed input that triggers it.
/// </para>
/// </remarks>
internal static class ImportRestoreFuzz
{
    public static void Consume(byte[] bytes)
    {
        // Empty input selects nothing and exercises nothing; keep the corpus honest.
        if (bytes.Length == 0)
        {
            return;
        }

        // One bit of the first byte picks the parser; the rest of the input is the payload. The
        // fuzzer discovers the selector as just another byte and learns to drive both sides.
        bool restore = (bytes[0] & 1) == 1;
        byte[] payload = bytes[1..];

        RunAsync(restore, payload).GetAwaiter().GetResult();
    }

    private static async Task RunAsync(bool restore, byte[] payload)
    {
        FuzzFactory factory = new();
        try
        {
            if (restore)
            {
                await FuzzRestoreAsync(factory, payload);
            }
            else
            {
                await FuzzImportAsync(factory, payload);
            }
        }
        finally
        {
            // Runs during unwinding even when an invariant or an escaping exception is in flight.
            // The crashing INPUT is already preserved by Program.Run's exception filter, which runs
            // before this, so deleting the scratch database here loses nothing needed to reproduce.
            await factory.CleanupAsync();
        }
    }

    private static async Task FuzzImportAsync(FuzzFactory factory, byte[] payload)
    {
        SQLiteAsyncConnection db = await factory.GetDatabaseAsync();

        // Import needs an existing trainer to own the matches. Seed one directly rather than through
        // the service, so the thing under test is only the parser.
        Trainer trainer = new() { Name = "Fuzz", IsActive = true };
        _ = await db.InsertAsync(trainer);
        uint trainerId = trainer.Id;

        ILimitlessMetaService meta = new EmptyMeta();
        TrainerHillImportService service = new(
            factory, NullLogger<TrainerHillImportService>.Instance, meta, new NoopMonitor());

        // Raw bytes straight in — a JSON parser reading a stream is exactly what production does,
        // and it avoids a UTF-8 round-trip that would hide malformed-byte behaviour.
        using MemoryStream stream = new(payload, writable: false);

        // No try/catch: ImportAsync is designed never to throw, so an escape is a finding.
        (int Imported, int SkippedDuplicates, System.Collections.Generic.List<string> Errors) result =
            await service.ImportAsync(stream, trainerId);

        if (result.Imported < 0 || result.SkippedDuplicates < 0)
        {
            throw new InvalidOperationException(
                $"import returned negative counts: imported={result.Imported}, skipped={result.SkippedDuplicates}");
        }

        int written = await db.Table<MatchEntry>().Where(m => m.TrainerId == trainerId).CountAsync();
        if (written != result.Imported)
        {
            throw new InvalidOperationException(
                $"import reported {result.Imported} matches but wrote {written} rows");
        }
    }

    private static async Task FuzzRestoreAsync(FuzzFactory factory, byte[] payload)
    {
        RestoreService service = new(factory, NullLogger<RestoreService>.Instance, new NoopMonitor());

        // RestoreBackupAsync takes the envelope as a string; feeding arbitrary decoded bytes is the
        // point — a restore must survive any string, not only well-formed JSON.
        string json = Encoding.UTF8.GetString(payload);

        // No try/catch: RestoreBackupAsync is designed never to throw.
        RestoreResult result = await service.RestoreBackupAsync(json);

        if (result.MatchesInserted < 0 || result.TrainersCreated < 0
            || result.MatchesSkippedIdentical < 0 || result.TrainersMerged < 0)
        {
            throw new InvalidOperationException(
                $"restore returned a negative count: inserted={result.MatchesInserted}, "
                + $"created={result.TrainersCreated}, skipped={result.MatchesSkippedIdentical}, merged={result.TrainersMerged}");
        }

        SQLiteAsyncConnection db = await factory.GetDatabaseAsync();
        int written = await db.Table<MatchEntry>().CountAsync();
        if (written != result.MatchesInserted)
        {
            throw new InvalidOperationException(
                $"restore reported {result.MatchesInserted} matches inserted but the database holds {written}");
        }
    }

    /// <summary>A connection factory over an isolated temp database file, cleaned up per iteration.</summary>
    private sealed class FuzzFactory : SqliteConnectionFactory
    {
        private readonly string _path;

        public FuzzFactory()
            : base(NullLogger.Instance, new EmptyMeta(), new NoopErrorHandler()) =>
            _path = Path.Combine(Path.GetTempPath(), $"pbjfuzz_{Guid.NewGuid():N}.db3");

        protected override string GetDbPath() => _path;

        public async Task CleanupAsync()
        {
            try
            {
                SQLiteAsyncConnection db = await GetDatabaseAsync();
                await db.CloseAsync();
            }
            catch (Exception ex)
            {
                // Cleanup must not mask a finding, but must not be silent either — a leak here would
                // eventually starve the run of file handles and look like an unrelated failure.
                Console.Error.WriteLine($"fuzz db close failed: {ex.GetType().Name}: {ex.Message}");
            }

            // WAL mode leaves -wal/-shm beside the file; a plain journal leaves -journal. Delete all.
            foreach (string suffix in new[] { "", "-wal", "-shm", "-journal" })
            {
                try
                {
                    File.Delete(_path + suffix);
                }
                catch (IOException)
                {
                    // Best effort; the temp dir is swept by the OS and a stuck file is not a finding.
                }
            }
        }
    }

    private sealed class EmptyMeta : ILimitlessMetaService
    {
        public Task<System.Collections.Generic.List<MetaDeck>> GetTopDecksAsync(int count = 10) =>
            Task.FromResult(new System.Collections.Generic.List<MetaDeck>());
    }

    private sealed class NoopErrorHandler : IErrorHandler
    {
        public void HandleError(Exception ex)
        {
            // The services report parse failures through here; a fuzz run does not surface them.
        }
    }

    private sealed class NoopMonitor : IPerformanceMonitor
    {
        public ITimedSpan StartSpan(string operation, string description) => new NoopSpan();
    }

    private sealed class NoopSpan : ITimedSpan
    {
        public void SetMeasurement(string name, double value) { }

        public void SetFailed() { }

        public void Dispose() { }
    }
}
