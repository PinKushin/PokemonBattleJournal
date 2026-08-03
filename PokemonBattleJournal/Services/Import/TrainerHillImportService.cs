using System.Globalization;
using System.Text.Json;
using PokemonBattleJournal.Interfaces;
using SQLite;

namespace PokemonBattleJournal.Services.Import
{
    public class TrainerHillImportService : ITrainerHillImportService
    {
        private readonly ISqliteConnectionFactory _factory;
        private readonly ILogger<TrainerHillImportService> _logger;

        public TrainerHillImportService(ISqliteConnectionFactory factory, ILogger<TrainerHillImportService> logger)
        {
            _factory = factory;
            _logger = logger;
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

                    uint playingId = await ResolveArchetypeAsync(SlugToName(entry.Playing));
                    uint againstId = await ResolveArchetypeAsync(SlugToName(entry.Against));

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

        private async Task<uint> ResolveArchetypeAsync(string name)
        {
            SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
            try
            {
                await _factory.GetLock().WaitAsync();
                await db.ExecuteAsync(
                    "INSERT OR IGNORE INTO Archetype (Name, ImagePath) VALUES (?, ?)",
                    name, "ball_icon.png");
                Archetype? archetype = await db.Table<Archetype>()
                    .Where(a => a.Name == name)
                    .FirstOrDefaultAsync();
                return archetype is not null ? (uint)archetype.Id : 0u;
            }
            finally
            {
                _ = _factory.GetLock().Release();
            }
        }

        private async Task<Tags?> ResolveTagAsync(string name, uint trainerId)
        {
            SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
            try
            {
                await _factory.GetLock().WaitAsync();
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
            finally
            {
                _ = _factory.GetLock().Release();
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
