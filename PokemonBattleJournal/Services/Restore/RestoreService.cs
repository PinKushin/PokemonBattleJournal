using System.Globalization;
using System.Text.Json;
using PokemonBattleJournal.Services.Export;

namespace PokemonBattleJournal.Services.Restore
{
    /// <inheritdoc cref="IRestoreService"/>
    public class RestoreService : IRestoreService
    {
        /// <summary>Highest envelope version this can read. Refuse anything newer.</summary>
        private const int MaxSupportedVersion = 1;

        /// <summary>
        /// Same ceiling the TrainerHill import enforces. A backup is our own file, but "our own
        /// file" is only ever a claim about a byte array the user picked off disk.
        /// </summary>
        private const int MaxBytes = 8 * 1024 * 1024;

        private const int MaxDepth = 16;

        private static readonly JsonSerializerOptions ReadOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            MaxDepth = MaxDepth,
        };

        private readonly ISqliteConnectionFactory _factory;
        private readonly ILogger<RestoreService> _logger;

        public RestoreService(ISqliteConnectionFactory factory, ILogger<RestoreService> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<RestoreResult> RestoreBackupAsync(string json)
        {
            List<string> errors = [];

            if (string.IsNullOrWhiteSpace(json))
            {
                errors.Add("The backup file is empty.");
                return new RestoreResult { Errors = errors };
            }

            if (json.Length > MaxBytes)
            {
                errors.Add($"Backup refused: the file is larger than {MaxBytes / (1024 * 1024)}MB.");
                return new RestoreResult { Errors = errors };
            }

            ExportBackup? backup;
            try
            {
                backup = JsonSerializer.Deserialize<ExportBackup>(json, ReadOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Backup could not be parsed");
                errors.Add($"The backup file could not be read: {ex.Message}");
                return new RestoreResult { Errors = errors };
            }

            if (backup is null)
            {
                errors.Add("The backup file could not be read.");
                return new RestoreResult { Errors = errors };
            }

            // Refuse rather than guess. A newer envelope may carry fields this cannot honour,
            // and a partial restore of a backup is worse than none: the user believes their
            // data is back.
            if (backup.Version > MaxSupportedVersion)
            {
                errors.Add($"This backup was written by a newer version of the app (format {backup.Version}); this build reads up to {MaxSupportedVersion}.");
                return new RestoreResult { Errors = errors };
            }

            int trainersCreated = 0;
            int trainersMerged = 0;
            int inserted = 0;
            int skipped = 0;
            List<RestoreConflict> conflicts = [];

            await RestoreArchetypesAsync(backup, errors);

            foreach (ExportTrainer exported in backup.Trainers)
            {
                if (string.IsNullOrWhiteSpace(exported.Name))
                {
                    errors.Add("Skipped a trainer with no name.");
                    continue;
                }

                (uint trainerId, bool created) = await ResolveTrainerAsync(exported.Name);
                if (trainerId == 0)
                {
                    errors.Add($"Could not create or find trainer '{exported.Name}'; its matches were skipped.");
                    continue;
                }

                if (created) { trainersCreated++; } else { trainersMerged++; }

                // Loaded once per trainer rather than queried per entry: a restore of a long
                // history would otherwise be N queries deep for no benefit.
                List<MatchEntry> existing = await _factory.Matches.GetByTrainerIdAsync(trainerId, includeRelated: true);
                Dictionary<MatchKey, MatchEntry> existingByKey = [];
                foreach (MatchEntry match in existing)
                {
                    existingByKey[MatchKey.From(match)] = match;
                }

                foreach (ExportEntry entry in exported.Matches)
                {
                    RestoreOutcome outcome = await RestoreEntryAsync(entry, trainerId, exported.Name, existingByKey, errors);
                    switch (outcome)
                    {
                        case RestoreOutcome.Inserted: inserted++; break;
                        case RestoreOutcome.SkippedIdentical: skipped++; break;
                        case RestoreOutcome.Conflicted: break;
                        default: break;
                    }
                }

                conflicts.AddRange(_pendingConflicts);
                _pendingConflicts.Clear();
            }

            _logger.LogInformation(
                "Restore complete: {Created} trainer(s) created, {Merged} merged, {Inserted} match(es) inserted, {Skipped} already present, {Conflicts} conflict(s), {Errors} error(s)",
                trainersCreated, trainersMerged, inserted, skipped, conflicts.Count, errors.Count);

            return new RestoreResult
            {
                TrainersCreated = trainersCreated,
                TrainersMerged = trainersMerged,
                MatchesInserted = inserted,
                MatchesSkippedIdentical = skipped,
                Conflicts = conflicts,
                Errors = errors,
            };
        }

        private readonly List<RestoreConflict> _pendingConflicts = [];

        private enum RestoreOutcome { Inserted, SkippedIdentical, Conflicted, Failed }

        /// <summary>
        /// Recreates archetypes with the icons the backup recorded.
        /// </summary>
        /// <remarks>
        /// Runs before any match so the archetypes a match references already carry their real
        /// icons. Without this the match loop would create them from a bare name and every one
        /// would land on substitute.png.
        ///
        /// Existing rows are left alone: their icon may have been improved by a later scrape,
        /// and overwriting it with an older backup's value would be a downgrade the user never
        /// asked for. INSERT OR IGNORE gives exactly that.
        ///
        /// Backups written before icons were carried have no archetypes array at all, which is
        /// not an error — the match loop's name-based creation still covers them.
        /// </remarks>
        private async Task RestoreArchetypesAsync(ExportBackup backup, List<string> errors)
        {
            if (backup.Archetypes.Count == 0)
            {
                return;
            }

            List<Trainer> knownTrainers = await _factory.Trainers.GetAllAsync();
            Dictionary<string, uint> trainerIdByName = knownTrainers
                .Where(t => !string.IsNullOrEmpty(t.Name))
                .GroupBy(t => t.Name!)
                .ToDictionary(g => g.Key, g => g.First().Id);

            try
            {
                using DbSession session = await _factory.BeginAsync();
                SQLiteAsyncConnection db = session.Connection;

                foreach (ExportArchetype archetype in backup.Archetypes.Where(a => !string.IsNullOrWhiteSpace(a.Name)))
                {
                    // Ownership is restored only when that trainer exists here. A custom
                    // archetype whose owner is not in this database keeps the row but loses the
                    // link, which is better than inventing a trainer.
                    uint ownerId = archetype.TrainerName is not null
                        ? trainerIdByName.GetValueOrDefault(archetype.TrainerName)
                        : 0u;

                    await db.ExecuteAsync(
                        "INSERT OR IGNORE INTO Archetype (Name, ImagePath, ImagePath2, TrainerId) VALUES (?, ?, ?, ?)",
                        archetype.Name,
                        string.IsNullOrWhiteSpace(archetype.ImagePath) ? "substitute.png" : archetype.ImagePath,
                        archetype.ImagePath2,
                        ownerId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore archetypes");
                errors.Add($"Archetype icons could not be restored: {ex.Message}");
            }
        }

        private async Task<(uint TrainerId, bool Created)> ResolveTrainerAsync(string name)
        {
            Trainer? existing = await _factory.Trainers.GetByNameAsync(name);
            if (existing is not null)
            {
                return (existing.Id, false);
            }

            _ = await _factory.Trainers.SaveAsync(name);
            Trainer? created = await _factory.Trainers.GetByNameAsync(name);
            return (created?.Id ?? 0u, true);
        }

        private async Task<RestoreOutcome> RestoreEntryAsync(
            ExportEntry entry,
            uint trainerId,
            string trainerName,
            Dictionary<MatchKey, MatchEntry> existingByKey,
            List<string> errors)
        {
            if (!Enum.TryParse(entry.Result, ignoreCase: true, out MatchResult result))
            {
                errors.Add($"Skipped an entry with an unrecognised result '{entry.Result}'.");
                return RestoreOutcome.Failed;
            }

            uint playingId = await ResolveArchetypeAsync(entry.Playing);
            uint againstId = await ResolveArchetypeAsync(entry.Against);
            if (playingId == 0 || againstId == 0)
            {
                errors.Add($"Skipped an entry: could not resolve archetypes '{entry.Playing}' / '{entry.Against}'.");
                return RestoreOutcome.Failed;
            }

            // startTime is the keying field, so its fallback matters. Backups written before
            // timings were carried have only `time`, which held DatePlayed then — the best
            // available, and still better than "now", which would make every re-restore look
            // like a new match.
            DateTime startTime = ParseTime(entry.StartTime) ?? ParseTime(entry.Time) ?? DateTime.UtcNow;
            DateTime endTime = ParseTime(entry.EndTime) ?? startTime;
            DateTime datePlayed = ParseTime(entry.Time) ?? startTime;

            MatchKey key = new(startTime, playingId, againstId, result);
            if (existingByKey.TryGetValue(key, out MatchEntry? already))
            {
                return ClassifyAgainstExisting(entry, already, trainerName);
            }

            List<Game> games = [];
            foreach (ExportGame? exportGame in new[] { entry.Game1, entry.Game2, entry.Game3 })
            {
                if (exportGame is null)
                {
                    continue;
                }

                if (!Enum.TryParse(exportGame.Result, ignoreCase: true, out MatchResult gameResult))
                {
                    errors.Add($"Skipped a game with an unrecognised result '{exportGame.Result}'.");
                    continue;
                }

                List<Tags> tags = [];
                foreach (string tagName in exportGame.Tags)
                {
                    Tags? tag = await ResolveTagAsync(tagName, trainerId);
                    if (tag is not null)
                    {
                        tags.Add(tag);
                    }
                }

                games.Add(new Game
                {
                    Result = gameResult,
                    Turn = exportGame.Turn,
                    Tags = tags,
                    Notes = exportGame.Notes,
                });
            }

            if (games.Count == 0)
            {
                errors.Add($"Skipped an entry with no usable games ({entry.Playing} vs {entry.Against}).");
                return RestoreOutcome.Failed;
            }

            MatchEntry restored = new()
            {
                TrainerId = trainerId,
                PlayingId = playingId,
                AgainstId = againstId,
                Result = result,
                StartTime = startTime,
                EndTime = endTime,
                DatePlayed = datePlayed,
            };

            int affected = await _factory.Matches.SaveAsync(restored, games);
            if (affected <= 0)
            {
                errors.Add($"Failed to save a restored match ({entry.Playing} vs {entry.Against}).");
                return RestoreOutcome.Failed;
            }

            // Keep the key set current so two identical entries inside one file do not both
            // insert — the file is not guaranteed to be free of duplicates itself.
            existingByKey[key] = restored;
            return RestoreOutcome.Inserted;
        }

        /// <summary>
        /// Decides what an entry that already has a same-key match means.
        /// </summary>
        /// <remarks>
        /// Only two outcomes are acted on. Identical is skipped and counted; anything else is
        /// recorded as a conflict and left alone.
        ///
        /// Nothing is merged or overwritten here, deliberately. Matches are insert-only today —
        /// there is no update path in MatchOperations at all — so a backup restore can meet a
        /// match that is identical or one that does not exist yet, and nothing else. A "richer"
        /// or genuinely conflicting pair therefore means the data diverged some other way, and
        /// guessing at it risks destroying a real match the app cannot distinguish from a
        /// duplicate: the key includes AgainstId, which identifies a *deck*, not a person.
        /// </remarks>
        private RestoreOutcome ClassifyAgainstExisting(ExportEntry entry, MatchEntry existing, string trainerName)
        {
            List<string> differences = [];

            CompareGame(entry.Game1, existing.Game1, "game 1", differences);
            CompareGame(entry.Game2, existing.Game2, "game 2", differences);
            CompareGame(entry.Game3, existing.Game3, "game 3", differences);

            if (differences.Count == 0)
            {
                return RestoreOutcome.SkippedIdentical;
            }

            _pendingConflicts.Add(new RestoreConflict
            {
                TrainerName = trainerName,
                ExistingMatchId = existing.Id,
                StartTime = existing.StartTime,
                Description = string.Join("; ", differences),
            });
            return RestoreOutcome.Conflicted;
        }

        private static void CompareGame(ExportGame? incoming, Game? existing, string label, List<string> differences)
        {
            if (incoming is null && existing is null)
            {
                return;
            }

            if (incoming is null || existing is null)
            {
                differences.Add($"{label} is present on only one side");
                return;
            }

            string incomingNotes = incoming.Notes ?? string.Empty;
            string existingNotes = existing.Notes ?? string.Empty;
            if (!string.Equals(incomingNotes, existingNotes, StringComparison.Ordinal))
            {
                differences.Add($"{label} notes differ");
            }

            HashSet<string> incomingTags = [.. incoming.Tags];
            HashSet<string> existingTags = [.. (existing.Tags ?? []).Select(t => t.Name ?? string.Empty)];
            if (!incomingTags.SetEquals(existingTags))
            {
                differences.Add($"{label} tags differ");
            }
        }

        private static DateTime? ParseTime(string? value) =>
            DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed)
                ? parsed
                : null;

        private async Task<uint> ResolveArchetypeAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return 0;
            }

            using DbSession session = await _factory.BeginAsync();
            SQLiteAsyncConnection db = session.Connection;

            // Created from the name only when RestoreArchetypesAsync did not already supply it
            // with a real icon — which is the case for older backups and for names a match
            // references without a matching archetype row.
            await db.ExecuteAsync(
                "INSERT OR IGNORE INTO Archetype (Name, ImagePath) VALUES (?, ?)",
                name, "substitute.png");

            Archetype? archetype = await db.Table<Archetype>()
                .Where(a => a.Name == name)
                .FirstOrDefaultAsync();
            return archetype?.Id ?? 0u;
        }

        private async Task<Tags?> ResolveTagAsync(string name, uint trainerId)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            using DbSession session = await _factory.BeginAsync();
            SQLiteAsyncConnection db = session.Connection;
            await db.ExecuteAsync(
                "INSERT OR IGNORE INTO Tags (Name, TrainerId) VALUES (?, ?)",
                name, trainerId);

            return await db.Table<Tags>()
                .Where(t => t.Name == name)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// The duplicate-detection key: <c>(StartTime, PlayingId, AgainstId, Result)</c> within
        /// one trainer.
        /// </summary>
        /// <remarks>
        /// <c>StartTime</c>, not <c>DatePlayed</c>. DatePlayed comes from a date picker and sits
        /// at midnight, so it collides constantly; StartTime carries a real time of day from
        /// every source — sub-second for TrainerHill entries, minute precision from the app's
        /// own time picker.
        ///
        /// It cannot be perfect and is not treated as such. <c>AgainstId</c> identifies a deck
        /// rather than a person — the model stores no opponent identity — so two different
        /// opponents on the same deck in the same minute produce the same key. That is why a
        /// key hit is never a licence to delete or overwrite.
        /// </remarks>
        private readonly record struct MatchKey(DateTime StartTime, uint PlayingId, uint AgainstId, MatchResult Result)
        {
            public static MatchKey From(MatchEntry match) =>
                new(match.StartTime, match.PlayingId, match.AgainstId, match.Result ?? MatchResult.Win);
        }
    }
}
