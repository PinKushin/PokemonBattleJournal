using SQLiteNetExtensionsAsync.Extensions;
namespace PokemonBattleJournal.Services
{
    public class MatchOperations : IMatchOperations
    {
        private readonly SqliteConnectionFactory _factory;
        private readonly ILogger _logger;

        public MatchOperations(SqliteConnectionFactory factory, ILogger logger)
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
            SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
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
                return [];
            }
            finally
            {
                _ = _factory.GetLock().Release();
            }
        }

        /// <summary>
        /// Saves a match entry and its associated games to the database in a single transaction.
        /// </summary>
        public virtual async Task<int> SaveAsync(MatchEntry matchEntry, List<Game> games)
        {
            // Validate required fields
            if (matchEntry.TrainerId == 0)
            {
                throw new ArgumentException("Trainer ID is required", nameof(matchEntry));
            }

            if (matchEntry.PlayingId == 0)
            {
                throw new ArgumentException("Playing archetype ID is required", nameof(matchEntry));
            }

            if (matchEntry.AgainstId == 0)
            {
                throw new ArgumentException("Against archetype ID is required", nameof(matchEntry));
            }

            // Validate games
            if (games == null || games.Count == 0)
            {
                throw new ArgumentException("At least one game is required", nameof(games));
            }

            try
            {
                SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
                int affected = 0;
                await _factory.GetLock().WaitAsync();

                // Pre-validate tags to ensure they exist in the database
                await PreValidateTagsAsync(db, games);

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
                    foreach (Game game in games)
                    {
                        affected += SaveGame(tran, game);
                    }

                    // Update match entry with game references
                    if (games.Count > 0)
                    {
                        matchEntry.Game1Id = games[0].Id;
                        if (games.Count > 1)
                        {
                            matchEntry.Game2Id = games[1].Id;
                        }

                        if (games.Count > 2)
                        {
                            matchEntry.Game3Id = games[2].Id;
                        }

                        affected += tran.Update(matchEntry);
                    }
                });
                // Verify data integrity after save
                await VerifyDataIntegrityAsync(db, matchEntry, games);
                return affected;
            }
            catch (ArgumentException ex)
            {
                ModalErrorHandler error = new();
                _logger.LogError(ex, "Invalid data when saving match entry: {Message}", ex.Message);
                error.HandleError(ex);
                return 0;
            }
            catch (SQLiteException ex)
            {
                ModalErrorHandler error = new();
                _logger.LogError(ex, "Database error when saving match entry: {Message}", ex.Message);
                error.HandleError(ex);
                return 0;
            }
            catch (Exception ex)
            {
                ModalErrorHandler error = new();
                _logger.LogError(ex, "Unexpected error saving match entry: {Message}", ex.Message);
                error.HandleError(ex);
                return 0;
            }
            finally
            {
                _ = _factory.GetLock().Release();
            }
        }

        /// <summary>
        /// Gets a match entry with all related data by ID.
        /// </summary>
        public virtual async Task<MatchEntry?> GetByIdAsync(uint id, bool includeRelated = true)
        {
            SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
            try
            {
                await _factory.GetLock().WaitAsync();
                MatchEntry? matchEntry = await db.GetWithChildrenAsync<MatchEntry>(id, true);

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
                _ = _factory.GetLock().Release();
            }
        }

        /// <summary>
        /// Gets all match entries for a trainer with related data.
        /// </summary>
        public virtual async Task<List<MatchEntry>> GetByTrainerIdAsync(uint trainerId, bool includeRelated = true)
        {
            SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
            try
            {
                await _factory.GetLock().WaitAsync();
                List<MatchEntry> entries = await db.GetAllWithChildrenAsync<MatchEntry>(
                    e => e.TrainerId == trainerId, true);
                if (entries == null || entries.Count == 0)
                {
                    _logger.LogInformation("No Entries Found");
                    return [];
                }
                if (includeRelated)
                {
                    foreach (MatchEntry entry in entries)
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
                return [];
            }
            finally
            {
                _ = _factory.GetLock().Release();
            }
        }

        /// <summary>
        /// Deletes a match entry and all related records.
        /// </summary>
        public virtual async Task<int> DeleteAsync(MatchEntry matchEntry)
        {
            if (matchEntry == null)
            {
                throw new ArgumentNullException(nameof(matchEntry), "Match entry cannot be null");
            }

            if (matchEntry.Id == 0)
            {
                throw new ArgumentException("Match entry ID is required", nameof(matchEntry));
            }

            SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
            try
            {
                int affected = 0;
                await _factory.GetLock().WaitAsync();

                // Get all related records to ensure complete deletion
                List<uint> gamesIds = [];
                if (matchEntry.Game1Id.HasValue)
                {
                    gamesIds.Add(matchEntry.Game1Id.Value);
                }

                if (matchEntry.Game2Id.HasValue)
                {
                    gamesIds.Add(matchEntry.Game2Id.Value);
                }

                if (matchEntry.Game3Id.HasValue)
                {
                    gamesIds.Add(matchEntry.Game3Id.Value);
                }

                await db.RunInTransactionAsync(tran =>
                {
                    // Delete games and their tags first
                    if (matchEntry.Game1 != null)
                    {
                        affected += DeleteGame(tran, matchEntry.Game1);
                    }

                    if (matchEntry.Game2 != null)
                    {
                        affected += DeleteGame(tran, matchEntry.Game2);
                    }

                    if (matchEntry.Game3 != null)
                    {
                        affected += DeleteGame(tran, matchEntry.Game3);
                    }

                    affected += tran.Delete(matchEntry);

                    // Verify all related records are deleted
                    foreach (uint gameId in gamesIds)
                    {
                        Task<Game> gameExists = db.FindAsync<Game>(gameId);
                        if (!gameExists.IsCompletedSuccessfully)
                        {
                            _logger.LogWarning("Game {GameId} was not deleted properly", gameId);
                            _ = tran.Delete(gameExists);
                        }

                        Task<List<TagGame>> tagGameExists = db.Table<TagGame>().Where(tg => tg.GameId == gameId).ToListAsync();
                        if (tagGameExists.Result.Count != 0)
                        {
                            _logger.LogWarning("TagGame entries for Game {GameId} were not deleted properly", gameId);
                            foreach (TagGame tagGame in tagGameExists.Result)
                            {
                                _ = tran.Delete(tagGame);
                            }
                        }
                    }
                });
                return affected;
            }
            catch (SQLiteException ex)
            {
                ModalErrorHandler error = new();
                _logger.LogError(ex, "Database error when deleting match entry: {Message}", ex.Message);
                error.HandleError(ex);
                return 0;
            }
            catch (Exception ex)
            {
                ModalErrorHandler error = new();
                _logger.LogError(ex, "Error deleting match entry: {Message}", ex.Message);
                error.HandleError(ex);
                return 0;
            }
            finally
            {
                _ = _factory.GetLock().Release();
            }
        }

        private async Task LoadRelatedDataAsync(SQLiteAsyncConnection db, MatchEntry entry)
        {
            // Load archetypes
            if (entry.PlayingId != 0)
            {
                entry.Playing = await db.GetAsync<Archetype>(entry.PlayingId);
            }

            if (entry.AgainstId != 0)
            {
                entry.Against = await db.GetAsync<Archetype>(entry.AgainstId);
            }

            // Load games and their tags
            if (entry.Game1Id.HasValue)
            {
                entry.Game1 = await LoadGameWithTagsAsync(db, entry.Game1Id.Value);
            }

            if (entry.Game2Id.HasValue)
            {
                entry.Game2 = await LoadGameWithTagsAsync(db, entry.Game2Id.Value);
            }

            if (entry.Game3Id.HasValue)
            {
                entry.Game3 = await LoadGameWithTagsAsync(db, entry.Game3Id.Value);
            }
        }

        private async Task<Game?> LoadGameWithTagsAsync(SQLiteAsyncConnection db, uint gameId)
        {
            Game? game = await db.GetWithChildrenAsync<Game>(gameId, true);
            if (game != null)
            {
                _logger.LogDebug("Game {GameId} Tags loaded: {@Tags}", gameId, game.Tags);
            }
            return game;
        }

        internal int SaveGame(SQLiteConnection tran, Game game)
        {
            int affected = 0;

            // Save game
            if (game.Id != 0)
            {
                affected += tran.Update(game);
            }
            else
            {
                affected += tran.Insert(game);
            }

            // Save tags and tag-game relationships
            if (game.Tags != null && game.Tags.Count > 0)
            {
                _logger.LogDebug("Saving {Count} tags for game {GameId}", game.Tags.Count, game.Id);

                // First, delete any existing relationships
                _ = tran.Execute("DELETE FROM TagGame WHERE GameId = ?", game.Id);

                foreach (Tags tag in game.Tags)
                {
                    // Make sure tag exists in the database
                    if (tag.Id == 0)
                    {
                        _logger.LogDebug("Inserting new tag: {TagName}", tag.Name);
                        affected += tran.Insert(tag);
                    }

                    // Create the many-to-many relationship
                    TagGame tagGame = new()
                    {
                        GameId = game.Id,
                        TagId = tag.Id
                    };

                    affected += tran.Insert(tagGame);
                }
            }

            return affected;
        }

        internal int DeleteGame(SQLiteConnection tran, Game game)
        {
            int affected = 0;

            // Delete tag-game relationships first
            affected += tran.Execute("DELETE FROM TagGame WHERE GameId = ?", game.Id);

            _logger.LogDebug("Deleted tag relationships for game {GameId}", game.Id);

            // Delete game
            affected += tran.Delete(game);
            _logger.LogDebug("Deleted game {GameId}", game.Id);

            return affected;
        }

        /// <summary>
        /// Pre-validates tags to ensure they exist in the database before saving games
        /// </summary>
        private async Task PreValidateTagsAsync(SQLiteAsyncConnection db, List<Game> games)
        {
            HashSet<uint> allTagIds = [];

            // Collect all tag IDs from games
            foreach (Game game in games)
            {
                if (game.Tags != null)
                {
                    foreach (Tags tag in game.Tags)
                    {
                        if (tag.Id != 0) // Only check existing tags
                        {
                            _ = allTagIds.Add(tag.Id);
                        }
                    }
                }
            }

            // Verify all tags exist
            if (allTagIds.Count > 0)
            {
                // Check if all tags exist in the database
                List<uint> existingTagIds = (await db.Table<Tags>()
                    .Where(t => allTagIds.Contains(t.Id))
                    .ToListAsync())
                    .Select(t => t.Id)
                    .ToList();

                List<uint> missingTagIds = allTagIds.Except(existingTagIds).ToList();
                if (missingTagIds.Count > 0)
                {
                    throw new ArgumentException($"Tags with IDs {string.Join(", ", missingTagIds)} do not exist in the database");
                }

                _logger.LogDebug("All {Count} tags validated successfully", allTagIds.Count);
            }
        }

        /// <summary>
        /// Verifies data integrity after saving a match entry and its games
        /// </summary>
        private async Task VerifyDataIntegrityAsync(SQLiteAsyncConnection db, MatchEntry matchEntry, List<Game> games)
        {
            // Check if match entry was saved
            MatchEntry? matchExists = await db.FindAsync<MatchEntry>(matchEntry.Id);
            if (matchExists is null)
            {
                _logger.LogError("Match entry {Id} was not saved properly", matchEntry.Id);
                throw new Exception("Failed to save match entry");
            }

            // Check if all games were saved
            foreach (Game game in games)
            {
                Game? gameExists = await db.FindAsync<Game>(game.Id);
                if (gameExists is null)
                {
                    _logger.LogError("Game {Id} was not saved properly", game.Id);
                    throw new Exception("Failed to save game");
                }

                // Check tag relationships
                if (game.Tags is not null && game.Tags.Count > 0)
                {
                    foreach (Tags tag in game.Tags)
                    {
                        TagGame? relationExists = await db.Table<TagGame>()
                            .Where(tg => tg.GameId == game.Id && tg.TagId == tag.Id)
                            .FirstOrDefaultAsync();

                        if (relationExists is null)
                        {
                            _logger.LogWarning("Tag {TagId} relationship with Game {GameId} was not saved properly",
                                tag.Id, game.Id);

                            // Try to fix it
                            TagGame tagGame = new()
                            { GameId = game.Id, TagId = tag.Id };
                            _ = await db.InsertAsync(tagGame);
                        }
                    }
                }
            }

            _logger.LogDebug("Data integrity verified successfully for MatchEntry {Id}", matchEntry.Id);
        }
    }
}
