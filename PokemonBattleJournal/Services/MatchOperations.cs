using SQLiteNetExtensionsAsync.Extensions;

namespace PokemonBattleJournal.Services
{
    public class MatchOperations : IMatchOperations
    {
        private readonly SqliteConnectionFactory _factory;
        private readonly ILogger _logger;

        internal MatchOperations(SqliteConnectionFactory factory, ILogger logger)
        {
            _factory = factory;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves a list of all match entries from the database.
        /// </summary>
        /// <returns>List of MatchEntry</returns>
        public async Task<List<MatchEntry>> GetAllAsync()
        {
            var db = await _factory.GetDatabaseAsync();
            try
            {
                await _factory.GetLock().WaitAsync();
                return await db.Table<MatchEntry>().ToListAsync();
            }
            catch (Exception ex)
            {
                ModalErrorHandler error = new();
                _logger.LogError(ex, "Error getting all match entries");
                error.HandleError(ex);
                return new List<MatchEntry>();
            }
            finally
            {
                _factory.GetLock().Release();
            }
        }

        /// <summary>
        /// Saves a match entry and its associated games to the database in a single transaction.
        /// </summary>
        public virtual async Task<int> SaveAsync(MatchEntry matchEntry, List<Game> games)
        {
            var db = await _factory.GetDatabaseAsync();
            try
            {
                int affected = 0;
                await _factory.GetLock().WaitAsync();
                await db.RunInTransactionAsync(tran =>
                {
                    // Save match entry
                    if (matchEntry.Id != 0)
                    {
                        _logger.LogInformation("Updating match entry: {MatchEntryId}", matchEntry.Id);
                        affected += tran.Update(matchEntry);
                    }
                    else
                    {
                        _logger.LogInformation("Inserting match entry: {@MatchEntry}", matchEntry);
                        _logger.LogInformation("Playing: {PlayingName} \nAgainst: {AgainstName}",
                            matchEntry.Playing?.Name, matchEntry.Against?.Name);
                        affected += tran.Insert(matchEntry);
                    }

                    // Save games and update relationships
                    foreach (var game in games)
                    {
                        game.MatchEntryId = matchEntry.Id;
                        affected += SaveGame(tran, game);
                    }

                    // Update match entry with game references
                    if (games.Count > 0)
                    {
                        matchEntry.Game1Id = games[0].Id;
                        if (games.Count > 1)
                            matchEntry.Game2Id = games[1].Id;
                        if (games.Count > 2)
                            matchEntry.Game3Id = games[2].Id;

                        affected += tran.Update(matchEntry);
                    }
                });
                return affected;
            }
            catch (Exception ex)
            {
                ModalErrorHandler error = new();
                _logger.LogError(ex, "Error saving match entry");
                error.HandleError(ex);
                return 0;
            }
            finally
            {
                _factory.GetLock().Release();
            }
        }

        /// <summary>
        /// Gets a match entry with all related data by ID.
        /// </summary>
        public virtual async Task<MatchEntry?> GetByIdAsync(uint id, bool includeRelated = true)
        {
            var db = await _factory.GetDatabaseAsync();
            try
            {
                await _factory.GetLock().WaitAsync();
                var matchEntry = await db.GetWithChildrenAsync<MatchEntry>(id, true);

                if (matchEntry != null && includeRelated)
                {
                    await LoadRelatedDataAsync(db, matchEntry);
                }

                return matchEntry;
            }
            catch (Exception ex)
            {
                ModalErrorHandler error = new();
                _logger.LogError(ex, "Error loading match entry");
                error.HandleError(ex);
                return null;
            }
            finally
            {
                _factory.GetLock().Release();
            }
        }

        /// <summary>
        /// Gets all match entries for a trainer with related data.
        /// </summary>
        public virtual async Task<List<MatchEntry>> GetByTrainerIdAsync(uint trainerId, bool includeRelated = true)
        {
            var db = await _factory.GetDatabaseAsync();
            try
            {
                await _factory.GetLock().WaitAsync();
                var entries = await db.GetAllWithChildrenAsync<MatchEntry>(
                    e => e.TrainerId == trainerId, true);
                if (entries == null || entries.Count == 0)
                {
                    _logger.LogInformation("No Entries Found");
                    return new List<MatchEntry>();
                }
                if (includeRelated)
                {
                    foreach (var entry in entries)
                    {
                        await LoadRelatedDataAsync(db, entry);
                    }
                }

                _logger.LogInformation("Loaded {Count} match entries", entries.Count);
                return entries;
            }
            catch (Exception ex)
            {
                ModalErrorHandler error = new();
                _logger.LogError(ex, "Error loading trainer matches");
                error.HandleError(ex);
                return new List<MatchEntry>();
            }
            finally
            {
                _factory.GetLock().Release();
            }
        }

        /// <summary>
        /// Deletes a match entry and all related records.
        /// </summary>
        public virtual async Task<int> DeleteAsync(MatchEntry matchEntry)
        {
            var db = await _factory.GetDatabaseAsync();
            try
            {
                int affected = 0;
                await _factory.GetLock().WaitAsync();
                await db.RunInTransactionAsync(tran =>
                {
                    // Delete games and their tags first
                    if (matchEntry.Game1 != null)
                        affected += DeleteGame(tran, matchEntry.Game1);
                    if (matchEntry.Game2 != null)
                        affected += DeleteGame(tran, matchEntry.Game2);
                    if (matchEntry.Game3 != null)
                        affected += DeleteGame(tran, matchEntry.Game3);

                    affected += tran.Delete(matchEntry);
                });
                return affected;
            }
            catch (Exception ex)
            {
                ModalErrorHandler error = new();
                _logger.LogError(ex, "Error deleting match entry");
                error.HandleError(ex);
                return 0;
            }
            finally
            {
                _factory.GetLock().Release();
            }
        }

        private async Task LoadRelatedDataAsync(SQLiteAsyncConnection db, MatchEntry entry)
        {
            // Load archetypes
            if (entry.PlayingId != 0)
                entry.Playing = await db.GetAsync<Archetype>(entry.PlayingId);
            if (entry.AgainstId != 0)
                entry.Against = await db.GetAsync<Archetype>(entry.AgainstId);

            // Load games and their tags
            if (entry.Game1Id.HasValue)
                entry.Game1 = await LoadGameWithTagsAsync(db, entry.Game1Id.Value);
            if (entry.Game2Id != 0)
                entry.Game2 = await LoadGameWithTagsAsync(db, entry.Game2Id);
            if (entry.Game3Id != 0)
                entry.Game3 = await LoadGameWithTagsAsync(db, entry.Game3Id);
        }

        private async Task<Game?> LoadGameWithTagsAsync(SQLiteAsyncConnection db, uint gameId)
        {
            var game = await db.GetWithChildrenAsync<Game>(gameId, true);
            if (game != null)
            {
                game.Tags = await db.Table<Tags>()
                    .Where(t => t.GameId == game.Id)
                    .ToListAsync();
                _logger.LogDebug("Game {GameId} Tags loaded: {@Tags}", gameId, game.Tags);
            }
            return game;
        }

        internal int SaveGame(SQLiteConnection tran, Game game)
        {
            int affected = 0;

            // Save game
            if (game.Id != 0)
                affected += tran.Update(game);
            else
                affected += tran.Insert(game);

            // Save tags
            if (game.Tags != null)
            {
                foreach (var tag in game.Tags)
                {
                    tag.GameId = game.Id;
                    if (tag.Id != 0)
                        affected += tran.Update(tag);
                    else
                        affected += tran.Insert(tag);
                }
            }

            return affected;
        }

        internal int DeleteGame(SQLiteConnection tran, Game game)
        {
            int affected = 0;

            // Delete tags first
            if (game.Tags != null)
            {
                foreach (var tag in game.Tags)
                {
                    affected += tran.Delete(tag);
                }
            }

            // Delete game
            affected += tran.Delete(game);
            return affected;
        }
    }
}
