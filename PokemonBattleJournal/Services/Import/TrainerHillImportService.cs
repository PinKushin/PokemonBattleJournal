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

        public async Task<(int Imported, List<string> Errors)> ImportAsync(Stream jsonStream, uint trainerId)
        {
            if (trainerId == 0) throw new ArgumentException("TrainerId required", nameof(trainerId));

            List<string> errors = [];
            int imported = 0;

            List<TrainerHillEntry>? entries;
            try
            {
                entries = await JsonSerializer.DeserializeAsync<List<TrainerHillEntry>>(
                    jsonStream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize TrainerHill JSON");
                errors.Add($"Invalid JSON: {ex.Message}");
                return (0, errors);
            }

            if (entries is null || entries.Count == 0)
                return (imported, errors);

            List<MetaDeck> metaDecks = await _limitlessService.GetTopDecksAsync(int.MaxValue);
            Dictionary<string, string> slugLookup = BuildSlugLookup(metaDecks);
            Dictionary<string, MetaDeck> deckByName = metaDecks.ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);

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

                    List<Game> games = [];
                    await AddGameAsync(games, entry.Game1, trainerId, errors);
                    if (entry.Game2 is not null)
                        await AddGameAsync(games, entry.Game2, trainerId, errors);
                    if (entry.Game3 is not null)
                        await AddGameAsync(games, entry.Game3, trainerId, errors);

                    int affected = await _factory.Matches.SaveAsync(match, games);
                    if (affected > 0)
                        imported++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error importing entry {Playing} vs {Against}", entry.Playing, entry.Against);
                    errors.Add($"Failed to import {entry.Playing} vs {entry.Against}: {ex.Message}");
                }
            }

            return (imported, errors);
        }

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
