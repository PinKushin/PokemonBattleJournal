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
        private const int MaxBytes = IRestoreService.MaxBackupBytes;

        private const int MaxDepth = 16;

        private static readonly JsonSerializerOptions ReadOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            MaxDepth = MaxDepth,
        };

        private readonly ISqliteConnectionFactory _factory;
        private readonly ILogger<RestoreService> _logger;
        private readonly IPerformanceMonitor _monitor;

        public RestoreService(ISqliteConnectionFactory factory, ILogger<RestoreService> logger, IPerformanceMonitor monitor)
        {
            _factory = factory;
            _logger = logger;
            _monitor = monitor;
        }

        /// <summary>
        /// Times the restore and records its shape as numbers.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A thin wrapper rather than spans threaded through the body: the body has several early
        /// returns, and a <c>using</c> here covers all of them without the logic having to know
        /// it is being measured.
        /// </para>
        /// <para>
        /// <b>Every value here is a count or a length.</b> Span data is NOT filtered by
        /// SentryRedactingSink — that governs Serilog property values only — so a description
        /// carrying a trainer name would leave the device with nothing to stop it. Counts answer
        /// the question that matters anyway ("was it slow because it was big, or slow because it
        /// was broken") without carrying anything about who or what.
        /// </para>
        /// </remarks>
        public async Task<RestoreResult> RestoreBackupAsync(string json)
        {
            using ISpan span = _monitor.StartSpan("restore", "restore backup");
            span.SetMeasurement("bytes", json?.Length ?? 0);

            try
            {
                RestoreResult result = await RestoreBackupCoreAsync(json);

                span.SetMeasurement("trainers.created", result.TrainersCreated);
                span.SetMeasurement("trainers.merged", result.TrainersMerged);
                span.SetMeasurement("matches.inserted", result.MatchesInserted);
                span.SetMeasurement("matches.skipped", result.MatchesSkippedIdentical);
                span.SetMeasurement("conflicts", result.Conflicts.Count);
                span.SetMeasurement("errors", result.Errors.Count);

                // A restore that returns errors has not thrown, but it has not worked either.
                // Marking it failed is what makes "restores that quietly did nothing" findable.
                if (result.Errors.Count > 0)
                {
                    span.SetFailed();
                }

                return result;
            }
            catch (Exception)
            {
                span.SetFailed();
                throw;
            }
        }

        private async Task<RestoreResult> RestoreBackupCoreAsync(string json)
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
                Dictionary<MatchDuplicateKey, MatchEntry> existingByKey = MatchDuplicateKey.Index(existing);

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

        /// <inheritdoc/>
        public async Task<int> ApplyResolutionAsync(RestoreConflict conflict, ConflictResolution resolution)
        {
            // Keep is not a no-op by accident — it is the answer "the stored match is already
            // what I want". Writing the row back unchanged would burn a transaction and make the
            // affected count lie about what happened.
            if (resolution == ConflictResolution.Keep)
            {
                _logger.LogInformation("Conflict kept for match {MatchId}; nothing written", conflict.ExistingMatchId);
                return 0;
            }

            try
            {
                MatchEntry? stored = await _factory.Matches.GetByIdAsync(conflict.ExistingMatchId);
                if (stored is null)
                {
                    // Decisions are staged, so the user can delete a match from the journal while
                    // its conflict is still on screen. Reported, not thrown.
                    _logger.LogWarning(
                        "Conflict resolution skipped: match {MatchId} no longer exists",
                        conflict.ExistingMatchId);
                    return 0;
                }

                List<Game> games = [];
                await ApplyToSlotAsync(stored.Game1, conflict, "Game 1", resolution, stored.TrainerId, games);
                await ApplyToSlotAsync(stored.Game2, conflict, "Game 2", resolution, stored.TrainerId, games);
                await ApplyToSlotAsync(stored.Game3, conflict, "Game 3", resolution, stored.TrainerId, games);

                if (games.Count == 0)
                {
                    _logger.LogWarning(
                        "Conflict resolution skipped: match {MatchId} has no games to write",
                        conflict.ExistingMatchId);
                    return 0;
                }

                // SaveAsync updates rather than inserts because every id is non-zero, and it wraps
                // the whole set in one transaction — which is what makes a match all-or-nothing.
                int affected = await _factory.Matches.SaveAsync(stored, games);
                _logger.LogInformation(
                    "Conflict resolved for match {MatchId} as {Resolution}: {Affected} row(s)",
                    conflict.ExistingMatchId, resolution, affected);
                return affected;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resolve conflict for match {MatchId}", conflict.ExistingMatchId);
                return 0;
            }
        }

        /// <summary>
        /// Applies the resolution to one game slot, if that slot is one of the differing ones.
        /// </summary>
        /// <remarks>
        /// Every existing game goes into the list, changed or not: SaveAsync writes the set it is
        /// given, so omitting an untouched game would drop it from the match. Only the slots the
        /// conflict actually names get rewritten.
        /// </remarks>
        private async Task ApplyToSlotAsync(
            Game? game,
            RestoreConflict conflict,
            string label,
            ConflictResolution resolution,
            uint trainerId,
            List<Game> games)
        {
            if (game is null)
            {
                return;
            }

            games.Add(game);

            ConflictGameDiff? diff = conflict.Games.FirstOrDefault(g => g.Label == label);
            if (diff is null)
            {
                return;
            }

            game.Notes = ConflictResolver.ResolveNotes(diff.ExistingNotes, diff.IncomingNotes, resolution);

            IReadOnlyList<string> names = ConflictResolver.ResolveTags(
                diff.ExistingTags, diff.IncomingTags, resolution);

            List<Tags> tags = [];
            foreach (string name in names)
            {
                Tags? tag = await ResolveTagAsync(name, trainerId);
                if (tag is not null)
                {
                    tags.Add(tag);
                }
            }

            game.Tags = tags;
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
            Dictionary<MatchDuplicateKey, MatchEntry> existingByKey,
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

            MatchDuplicateKey key = new(startTime, playingId, againstId, result);
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
        /// Nothing is merged or overwritten HERE, deliberately — but not because it cannot be.
        /// An earlier version of this comment claimed matches were insert-only and that
        /// MatchOperations had no update path; that was simply wrong. SaveAsync calls
        /// tran.Update when Id is non-zero, and SaveGame updates games and rewrites their tag
        /// relationships. Resolution is applied through exactly that, from ApplyResolutionAsync.
        ///
        /// The real reason nothing is decided automatically is that the app cannot tell a
        /// re-import from two genuinely distinct matches: the dedupe key includes AgainstId,
        /// which identifies a *deck*, not a person, and the model stores no opponent identity at
        /// all. Two opponents on the same deck in the same minute are indistinguishable here. So
        /// the difference is recorded with both sides attached and a human decides.
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

            ConflictGameDiff[] games =
            [
                BuildDiff(entry.Game1, existing.Game1, "Game 1"),
                BuildDiff(entry.Game2, existing.Game2, "Game 2"),
                BuildDiff(entry.Game3, existing.Game3, "Game 3"),
            ];

            _pendingConflicts.Add(new RestoreConflict
            {
                TrainerName = trainerName,
                ExistingMatchId = existing.Id,
                StartTime = existing.StartTime,
                Description = string.Join("; ", differences),
                // Only the games that actually differ. Rendering three panels when one changed
                // buries the decision the user is being asked to make.
                Games = [.. games.Where(g => g.HasAnyDifference)],
            });
            return RestoreOutcome.Conflicted;
        }

        /// <summary>
        /// Captures both versions of one game so a resolution can be applied later.
        /// </summary>
        /// <remarks>
        /// Must stay in step with <see cref="CompareGame"/>: that one decides a conflict exists,
        /// this one describes it. If they disagreed, the user would be shown a difference that
        /// was not the one flagged, or a conflict with nothing visible in it.
        /// </remarks>
        private static ConflictGameDiff BuildDiff(ExportGame? incoming, Game? existing, string label) =>
            new()
            {
                Label = label,
                ExistingPresent = existing is not null,
                IncomingPresent = incoming is not null,
                ExistingNotes = existing?.Notes,
                IncomingNotes = incoming?.Notes,
                ExistingTags = [.. (existing?.Tags ?? []).Select(t => t.Name ?? string.Empty)],
                IncomingTags = incoming?.Tags ?? [],
            };

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

    }
}
