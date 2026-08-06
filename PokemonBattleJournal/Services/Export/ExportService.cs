using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using PokemonBattleJournal.Services.Import;

namespace PokemonBattleJournal.Services.Export
{
    /// <inheritdoc cref="IExportService"/>
    public class ExportService : IExportService
    {
        /// <summary>Matches the format TrainerHill emits, so exported files re-import cleanly.</summary>
        private const string TimeFormat = "yyyy-MM-dd HH:mm:ss.ffffff";

        private static readonly JsonSerializerOptions ExportJsonOptions = new()
        {
            WriteIndented = true,
            // A BO1 match has no second game, and TrainerHill's own exports omit the key rather
            // than writing null. Import treats missing and null identically, so this is purely
            // about producing a file that looks like theirs.
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        private readonly ISqliteConnectionFactory _factory;
        private readonly ILogger<ExportService> _logger;

        public ExportService(ISqliteConnectionFactory factory, ILogger<ExportService> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<string> ExportTrainerHillAsync(uint trainerId)
        {
            if (trainerId == 0)
                throw new ArgumentException("TrainerId required", nameof(trainerId));

            List<ExportEntry> entries = await BuildEntriesAsync(trainerId, TrainerHillImportService.NameToSlug, includeTimings: false);
            _logger.LogInformation("Exported {Count} matches for trainer {TrainerId} in TrainerHill format",
                entries.Count, trainerId);
            return JsonSerializer.Serialize(entries, ExportJsonOptions);
        }

        /// <inheritdoc/>
        public async Task<string> ExportBackupAsync(IReadOnlyList<uint>? trainerIds = null)
        {
            List<Trainer> all = await _factory.Trainers.GetAllAsync();
            List<Trainer> selected = trainerIds is null || trainerIds.Count == 0
                ? all
                : [.. all.Where(t => trainerIds.Contains(t.Id))];

            List<ExportTrainer> trainers = [];
            foreach (Trainer trainer in selected)
            {
                // Names verbatim, not slugged: a restore must not need the Limitless meta list
                // to recover what the archetype was called.
                trainers.Add(new ExportTrainer
                {
                    Name = trainer.Name ?? string.Empty,
                    Matches = await BuildEntriesAsync(trainer.Id, static name => name, includeTimings: true),
                });
            }

            _logger.LogInformation("Exported backup covering {TrainerCount} trainer(s), {MatchCount} match(es)",
                trainers.Count, trainers.Sum(t => t.Matches.Count));

            return JsonSerializer.Serialize(
                new ExportBackup
                {
                    ExportedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                    Trainers = trainers,
                },
                ExportJsonOptions);
        }

        /// <param name="nameStyle">
        /// How an archetype name is written: slugged for TrainerHill, verbatim for backups.
        /// </param>
        /// <param name="includeTimings">
        /// Whether to write <c>startTime</c>/<c>endTime</c>. True for backups, which must be
        /// lossless; false for TrainerHill, whose format has no such keys and whose value is
        /// being indistinguishable from a file of theirs.
        /// </param>
        private async Task<List<ExportEntry>> BuildEntriesAsync(uint trainerId, Func<string, string> nameStyle, bool includeTimings)
        {
            List<MatchEntry> matches = await _factory.Matches.GetByTrainerIdAsync(trainerId, includeRelated: true);
            return [.. matches.Select(m => ToEntry(m, nameStyle, includeTimings))];
        }

        private static ExportEntry ToEntry(MatchEntry match, Func<string, string> nameStyle, bool includeTimings) => new()
        {
            Playing = nameStyle(match.Playing?.Name ?? string.Empty),
            Against = nameStyle(match.Against?.Name ?? string.Empty),
            // StartTime, not DatePlayed. TrainerHill has one time field so this export cannot
            // be lossless, but DatePlayed is the weak choice — a date picker leaves it at
            // midnight, throwing away the time of day for nothing. StartTime carries the same
            // date plus real precision, and duplicate detection keys on it.
            Time = match.StartTime.ToString(TimeFormat, CultureInfo.InvariantCulture),
            StartTime = includeTimings ? match.StartTime.ToString(TimeFormat, CultureInfo.InvariantCulture) : null,
            EndTime = includeTimings ? match.EndTime.ToString(TimeFormat, CultureInfo.InvariantCulture) : null,
            Result = match.Result?.ToString() ?? string.Empty,
            Game1 = ToGame(match.Game1),
            Game2 = ToGame(match.Game2),
            Game3 = ToGame(match.Game3),
        };

        private static ExportGame? ToGame(Game? game) => game is null
            ? null
            : new ExportGame
            {
                Result = game.Result?.ToString() ?? string.Empty,
                Turn = game.Turn,
                Tags = [.. (game.Tags ?? []).Select(t => t.Name ?? string.Empty).Where(n => n.Length > 0)],
                Notes = game.Notes,
            };
    }
}
