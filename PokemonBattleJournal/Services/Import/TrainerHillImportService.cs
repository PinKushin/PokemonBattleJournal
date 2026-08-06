using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using PokemonBattleJournal.Interfaces;
using PokemonBattleJournal.Scraper.Interfaces;
using PokemonBattleJournal.Scraper.Models;
using PokemonBattleJournal.Services;
using SQLite;

namespace PokemonBattleJournal.Services.Import
{
    public class TrainerHillImportService : ITrainerHillImportService
    {
        private static readonly string[] VersionTokens = ["ex", "v", "vmax", "vstar", "gx", "ace", "sp"];

        // Limits on untrusted input. The file is user-chosen, but "user-chosen" includes a
        // battle log someone else sent them, so the document is not trusted. A real TrainerHill
        // export of a heavily played account is a few hundred KB; every ceiling below is orders
        // of magnitude above anything legitimate, so hitting one means the file is wrong.
        internal const long MaxBytes = 8L * 1024 * 1024;
        // The real shape is only array > entry > game > tags deep. System.Text.Json defaults to
        // 64, which is plenty of room for a nesting attack that costs nothing to refuse.
        internal const int MaxDepth = 16;
        internal const int MaxEntries = 10_000;
        // Archetype and tag names are written to the database on import, so an unbounded name is
        // persistent junk rather than a transient parse cost. These two matter most.
        internal const int MaxNameLength = 120;
        internal const int MaxTagNameLength = 100;
        internal const int MaxNotesLength = 4_000;
        internal const int MaxTagsPerGame = 64;

        private static readonly JsonSerializerOptions ImportJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            MaxDepth = MaxDepth,
        };

        private readonly ISqliteConnectionFactory _factory;
        private readonly ILogger<TrainerHillImportService> _logger;
        private readonly ILimitlessMetaService _limitlessService;

        public TrainerHillImportService(
            ISqliteConnectionFactory factory,
            ILogger<TrainerHillImportService> logger,
            ILimitlessMetaService limitlessService)
        {
            _factory = factory;
            _logger = logger;
            _limitlessService = limitlessService;
        }

        public async Task<(int Imported, int SkippedDuplicates, List<string> Errors)> ImportAsync(Stream jsonStream, uint trainerId)
        {
            if (trainerId == 0) throw new ArgumentException("TrainerId required", nameof(trainerId));

            List<string> errors = [];
            int imported = 0;
            int skippedDuplicates = 0;

            Stream? bounded = await TryBoundStreamAsync(jsonStream, errors);
            if (bounded is null)
                return (0, 0, errors);

            List<TrainerHillEntry>? entries;
            try
            {
                entries = await JsonSerializer.DeserializeAsync<List<TrainerHillEntry>>(bounded, ImportJsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize TrainerHill JSON");
                errors.Add($"Invalid JSON: {ex.Message}");
                return (0, 0, errors);
            }
            finally
            {
                // Only dispose a stream we created; the caller owns the one it passed in.
                if (!ReferenceEquals(bounded, jsonStream))
                    await bounded.DisposeAsync();
            }

            if (entries is null || entries.Count == 0)
                return (imported, skippedDuplicates, errors);

            if (entries.Count > MaxEntries)
            {
                // Refuse the whole document rather than importing the first MaxEntries. A partial
                // match log is worse than none: the user cannot tell which entries are missing,
                // and re-importing to "fill the gap" would duplicate everything that did land.
                _logger.LogWarning("Import refused: {Count} entries exceeds the limit of {Max}", entries.Count, MaxEntries);
                errors.Add($"Import refused: too many entries ({entries.Count}); the limit is {MaxEntries}.");
                return (0, 0, errors);
            }

            List<MetaDeck> metaDecks = await _limitlessService.GetTopDecksAsync(int.MaxValue);
            Dictionary<string, string> slugLookup = BuildSlugLookup(metaDecks);
            Dictionary<string, MetaDeck> deckByName = metaDecks.ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);

            // Loaded once rather than queried per entry: a long history would otherwise be
            // N queries deep for no benefit.
            Dictionary<MatchDuplicateKey, MatchEntry> existingByKey = MatchDuplicateKey.Index(
                await _factory.Matches.GetByTrainerIdAsync(trainerId, includeRelated: false));

            foreach (TrainerHillEntry entry in entries)
            {
                try
                {
                    MatchResult? result = ParseResult(entry.Result);
                    if (result is null)
                    {
                        errors.Add($"Skipped entry with unknown result '{entry.Result}'");
                        continue;
                    }

                    if (entry.Game1 is null)
                    {
                        errors.Add($"Skipped entry missing game1 data (playing={entry.Playing})");
                        continue;
                    }

                    // Runs before any database work, deliberately. Archetype and tag names are
                    // created on demand further down, so an over-long name that gets that far is
                    // already a row in the database by the time anything notices.
                    if (!TryValidateEntry(entry, out string? problem))
                    {
                        _logger.LogWarning("Skipped entry {Playing} vs {Against}: {Problem}",
                            entry.Playing, entry.Against, problem);
                        errors.Add($"Skipped entry ({Truncate(entry.Playing)} vs {Truncate(entry.Against)}): {problem}");
                        continue;
                    }

                    string playingName = LookupSlug(entry.Playing, slugLookup) ?? SlugToName(entry.Playing);
                    string againstName = LookupSlug(entry.Against, slugLookup) ?? SlugToName(entry.Against);
                    uint playingId = await ResolveArchetypeAsync(playingName, deckByName);
                    uint againstId = await ResolveArchetypeAsync(againstName, deckByName);

                    DateTime datePlayed = DateTime.TryParse(
                        entry.Time, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt)
                        ? dt
                        : DateTime.UtcNow;

                    MatchEntry match = new()
                    {
                        TrainerId = trainerId,
                        PlayingId = playingId,
                        AgainstId = againstId,
                        Result = result.Value,
                        DatePlayed = datePlayed,
                        StartTime = datePlayed,
                        EndTime = datePlayed,
                    };

                    // Skip what is already stored. TrainerHill has no "since last time"
                    // export, so a re-export overlaps the previous one and re-importing is the
                    // normal case — every entry used to be inserted again, silently.
                    //
                    // Counted rather than passed over: the key cannot distinguish a re-import
                    // from two genuinely identical matches, because AgainstId identifies a deck
                    // and the model stores no opponent identity. See MatchDuplicateKey.
                    MatchDuplicateKey key = MatchDuplicateKey.From(match);
                    if (existingByKey.ContainsKey(key))
                    {
                        skippedDuplicates++;
                        continue;
                    }

                    List<Game> games = [];
                    await AddGameAsync(games, entry.Game1, trainerId, errors);
                    if (entry.Game2 is not null)
                        await AddGameAsync(games, entry.Game2, trainerId, errors);
                    if (entry.Game3 is not null)
                        await AddGameAsync(games, entry.Game3, trainerId, errors);

                    int affected = await _factory.Matches.SaveAsync(match, games);
                    if (affected > 0)
                    {
                        imported++;
                        // The file is not guaranteed free of duplicates itself.
                        existingByKey[key] = match;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error importing entry {Playing} vs {Against}", entry.Playing, entry.Against);
                    errors.Add($"Failed to import {entry.Playing} vs {entry.Against}: {ex.Message}");
                }
            }

            return (imported, skippedDuplicates, errors);
        }

        /// <summary>
        /// Returns a stream guaranteed to be under <see cref="MaxBytes"/>, or null (with an error
        /// recorded) if the input exceeds it.
        /// </summary>
        /// <remarks>
        /// Two paths because the answer depends on the platform. A file picked on Windows gives a
        /// seekable FileStream whose Length settles it without reading a byte; Android hands back
        /// a content-provider stream where Length throws, so the cap has to be counted while
        /// copying.
        /// </remarks>
        private async Task<Stream?> TryBoundStreamAsync(Stream jsonStream, List<string> errors)
        {
            if (jsonStream.CanSeek)
            {
                if (jsonStream.Length > MaxBytes)
                {
                    LogRefusal(jsonStream.Length);
                    errors.Add(FileTooLargeMessage(jsonStream.Length));
                    return null;
                }
                return jsonStream;
            }

            MemoryStream buffer = new();
            byte[] chunk = new byte[81920];
            long total = 0;
            int read;
            while ((read = await jsonStream.ReadAsync(chunk)) > 0)
            {
                total += read;
                if (total > MaxBytes)
                {
                    await buffer.DisposeAsync();
                    LogRefusal(total);
                    errors.Add(FileTooLargeMessage(total));
                    return null;
                }
                await buffer.WriteAsync(chunk.AsMemory(0, read));
            }

            buffer.Position = 0;
            return buffer;

            void LogRefusal(long size) =>
                _logger.LogWarning("Import refused: file is {Size} bytes, limit is {Max}", size, MaxBytes);

            static string FileTooLargeMessage(long size) =>
                $"Import refused: file is too large ({size / 1024 / 1024} MB); the limit is {MaxBytes / 1024 / 1024} MB.";
        }

        /// <summary>
        /// Checks everything about an entry that could produce oversized database content.
        /// </summary>
        private static bool TryValidateEntry(TrainerHillEntry entry, out string? problem)
        {
            if (entry.Playing.Length > MaxNameLength || entry.Against.Length > MaxNameLength)
            {
                problem = $"archetype name too long (limit {MaxNameLength} characters)";
                return false;
            }

            foreach (TrainerHillGame? game in new[] { entry.Game1, entry.Game2, entry.Game3 })
            {
                if (game is null)
                    continue;

                if (game.Notes is not null && game.Notes.Length > MaxNotesLength)
                {
                    problem = $"notes too long (limit {MaxNotesLength} characters)";
                    return false;
                }

                if (game.Tags is null)
                    continue;

                if (game.Tags.Count > MaxTagsPerGame)
                {
                    problem = $"too many tags on one game (limit {MaxTagsPerGame})";
                    return false;
                }

                if (game.Tags.Exists(t => t is not null && t.Length > MaxTagNameLength))
                {
                    problem = $"tag name too long (limit {MaxTagNameLength} characters)";
                    return false;
                }
            }

            problem = null;
            return true;
        }

        /// <summary>Keeps a rejected entry's own oversized text out of the error message.</summary>
        private static string Truncate(string value) =>
            value.Length <= 40 ? value : string.Concat(value.AsSpan(0, 40), "…");

        private async Task AddGameAsync(List<Game> games, TrainerHillGame thGame, uint trainerId, List<string> errors)
        {
            MatchResult? gameResult = ParseResult(thGame.Result);
            if (gameResult is null)
            {
                errors.Add($"Skipped game with unknown result '{thGame.Result}'");
                return;
            }

            List<Tags> tags = [];
            foreach (string tagName in thGame.Tags ?? [])
            {
                Tags? tag = await ResolveTagAsync(tagName, trainerId);
                if (tag is not null)
                    tags.Add(tag);
            }

            games.Add(new Game
            {
                Result = gameResult,
                Turn = ParseTurn(thGame.Turn),
                Tags = tags,
                Notes = thGame.Notes,
            });
        }

        private async Task<uint> ResolveArchetypeAsync(string name, Dictionary<string, MetaDeck> deckByName)
        {
            _ = deckByName.TryGetValue(name, out MetaDeck? deck);
            string imagePath = SpriteResolver.FromUrl(deck?.ImageUrl) ?? "substitute.png";
            string? imagePath2 = SpriteResolver.FromUrl(deck?.SecondaryImageUrl);

            using DbSession session = await _factory.BeginAsync();
            SQLiteAsyncConnection db = session.Connection;
            await db.ExecuteAsync(
                "INSERT OR IGNORE INTO Archetype (Name, ImagePath, ImagePath2) VALUES (?, ?, ?)",
                name, imagePath, imagePath2);
            // Upgrade substitute.png placeholder if we now have a real sprite
            if (imagePath != "substitute.png")
                await db.ExecuteAsync(
                    "UPDATE Archetype SET ImagePath = ? WHERE Name = ? AND ImagePath = 'substitute.png'",
                    imagePath, name);
            if (imagePath2 != null)
                await db.ExecuteAsync(
                    "UPDATE Archetype SET ImagePath2 = ? WHERE Name = ? AND ImagePath2 IS NULL",
                    imagePath2, name);
            Archetype? archetype = await db.Table<Archetype>()
                .Where(a => a.Name == name)
                .FirstOrDefaultAsync();
            return archetype is not null ? archetype.Id : 0u;
        }

        private async Task<Tags?> ResolveTagAsync(string name, uint trainerId)
        {
            using DbSession session = await _factory.BeginAsync();
            SQLiteAsyncConnection db = session.Connection;
            try
            {
                Tags? existing = await db.Table<Tags>().Where(t => t.Name == name).FirstOrDefaultAsync();
                if (existing is not null)
                    return existing;

                Tags tag = new() { Name = name, TrainerId = trainerId };
                _ = await db.InsertAsync(tag);
                return tag;
            }
            catch (SQLiteException ex) when (ex.Message.Contains("UNIQUE"))
            {
                // Concurrent insert race — fetch what's there
                return await db.Table<Tags>().Where(t => t.Name == name).FirstOrDefaultAsync();
            }
        }

        internal static Dictionary<string, string> BuildSlugLookup(IEnumerable<MetaDeck> decks)
        {
            Dictionary<string, string> lookup = new(StringComparer.OrdinalIgnoreCase);
#pragma warning disable S3267 // SelectMany would hurt readability: each key must be paired with its source deck.Name
            foreach (MetaDeck deck in decks)
            {
                foreach (string key in NormalizationKeys(deck.Name))
                    _ = lookup.TryAdd(key, deck.Name);
            }
#pragma warning restore S3267
            return lookup;
        }

        internal static string? LookupSlug(string slug, Dictionary<string, string> lookup)
        {
            string key = slug.ToLowerInvariant().Replace('-', ' ');
            return lookup.TryGetValue(key, out string? name) ? name : null;
        }

        private static IEnumerable<string> NormalizationKeys(string name)
        {
            string lower = name.ToLowerInvariant()
                .Replace('/', ' ')
                .Replace('-', ' ');
            lower = Regex.Replace(lower, @"\s+", " ", RegexOptions.None, TimeSpan.FromSeconds(1)).Trim();

            string versionPattern = $@"\b({string.Join("|", VersionTokens)})\b";
            HashSet<string> seen = [];

            foreach (bool stripPossessive in new[] { false, true })
            {
                string step = stripPossessive
                    ? Regex.Replace(lower, @"'s\b", "", RegexOptions.None, TimeSpan.FromSeconds(1))
                    : lower.Replace("'", "");
                step = Regex.Replace(step, @"\s+", " ", RegexOptions.None, TimeSpan.FromSeconds(1)).Trim();

                foreach (bool stripVersion in new[] { false, true })
                {
                    string key = stripVersion
                        ? Regex.Replace(step, versionPattern, " ", RegexOptions.None, TimeSpan.FromSeconds(1))
                        : step;
                    key = Regex.Replace(key, @"\s+", " ", RegexOptions.None, TimeSpan.FromSeconds(1)).Trim();
                    if (!string.IsNullOrEmpty(key) && seen.Add(key))
                        yield return key;
                }
            }
        }

        /// <summary>
        /// Converts an archetype name to the slug form TrainerHill uses.
        /// </summary>
        /// <remarks>
        /// Deliberately built from <see cref="NormalizationKeys"/> so it is the inverse of
        /// <see cref="LookupSlug"/> by construction rather than by coincidence: the first
        /// normalization key is the least-stripped one, so the slug keeps version qualifiers
        /// ("dragapult-ex-dusknoir") and still matches on import.
        ///
        /// This is lossy without the Limitless meta list — "dragapult-ex-dusknoir" only becomes
        /// "Dragapult ex / Dusknoir" again if that deck is known, otherwise
        /// <see cref="SlugToName"/> yields "Dragapult Ex Dusknoir". That is why the backup format
        /// writes names verbatim instead of calling this.
        /// </remarks>
        internal static string NameToSlug(string name)
        {
            string? key = NormalizationKeys(name).FirstOrDefault();
            return string.IsNullOrEmpty(key) ? name.ToLowerInvariant() : key.Replace(' ', '-');
        }

        internal static string SlugToName(string slug)
        {
            string spaced = slug.Replace('-', ' ');
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(spaced);
        }

        internal static MatchResult? ParseResult(string result) => result.ToLowerInvariant() switch
        {
            "win" => MatchResult.Win,
            "loss" => MatchResult.Loss,
            "tie" => MatchResult.Tie,
            _ => null,
        };

        internal static uint ParseTurn(JsonElement el) => el.ValueKind switch
        {
            JsonValueKind.Number => (uint)el.GetInt32(),
            JsonValueKind.String when uint.TryParse(el.GetString(), out uint v) => v,
            _ => 1u,
        };
    }
}
