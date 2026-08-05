namespace PokemonBattleJournal.Services
{
    public class TagOperations : ITagOperations
    {
        private readonly ISqliteConnectionFactory _factory;
        private readonly ILogger _logger;
        private readonly IErrorHandler _errorHandler;

        internal TagOperations(ISqliteConnectionFactory factory, ILogger logger, IErrorHandler errorHandler)
        {
            _factory = factory;
            _logger = logger;
            _errorHandler = errorHandler;
        }

        /// <summary>
        /// Gets all tags from the database, initializing default tags if none exist.
        /// </summary>
        public async Task<List<Tags>> GetAllAsync()
        {
            try
            {
                using DbSession session = await _factory.BeginAsync();
                SQLiteAsyncConnection db = session.Connection;
                if (await db.Table<Tags>().CountAsync() == 0)
                {
                    _ = await db.InsertAllAsync(new List<Tags>
                {
                    new() { Name = "Early Start" },
                    new() { Name = "Behind Early" },
                    new() { Name = "Donked Rival" },
                    new() { Name = "Got Donked" },
                    new() { Name = "Lucky" },
                    new() { Name = "Unlucky" },
                    new() { Name = "Never Punished" },
                    new() { Name = "Punished" }
                });
                }
                return await db.Table<Tags>().ToListAsync();
            }
            catch (Exception ex)
            {
                // Log only — callers may invoke this from AppearingAsync before XamlRoot is set.
                _logger.LogError(ex, "Error getting tags");
                return [];
            }
        }

        /// <summary>
        /// Gets a tag by its ID.
        /// </summary>
        public async Task<Tags?> GetByIdAsync(uint id)
        {
            try
            {
                using DbSession session = await _factory.BeginAsync();
                return await session.Connection.Table<Tags>()
                    .Where(i => i.Id == id)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tag by ID: {Id}", id);
                _errorHandler.HandleError(ex);
                return null;
            }
        }

        /// <summary>
        /// Saves a tag to the database.
        /// </summary>
        public async Task<int> SaveAsync(string tagTxt, uint trainerId)
        {
            if (string.IsNullOrWhiteSpace(tagTxt))
            {
                throw new ArgumentException("Tag name is required", nameof(tagTxt));
            }

            if (trainerId == 0)
            {
                throw new ArgumentException("Trainer ID is required", nameof(trainerId));
            }

            _logger.LogDebug("SaveAsync: saving tag {Name} for trainer {TrainerId}", tagTxt, trainerId);
            Tags tag = new()
            { Name = tagTxt, TrainerId = trainerId };

            try
            {
                using DbSession session = await _factory.BeginAsync();
                int affected = 0;
                await session.Connection.RunInTransactionAsync(tran =>
                {
                    affected = tran.Insert(tag);
                });
                return affected;
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Invalid data when saving tag: {Message}", ex.Message);
                _errorHandler.HandleError(ex);
                return 0;
            }
            catch (SQLiteException ex)
            {
                _logger.LogError(ex, "Database error when saving tag: {Message}", ex.Message);
                _errorHandler.HandleError(ex);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving tag: {TagName} - {Message}", tagTxt, ex.Message);
                _errorHandler.HandleError(ex);
                return 0;
            }
        }

        /// <summary>
        /// Deletes a tag from the database.
        /// </summary>
        public async Task<int> DeleteAsync(Tags tag)
        {
            if (tag.Id == 0)
            {
                throw new ArgumentException("Tag ID is required", nameof(tag));
            }

            _logger.LogDebug("DeleteAsync: deleting tag {Name} ({Id})", tag.Name, tag.Id);
            try
            {
                using DbSession session = await _factory.BeginAsync();
                SQLiteAsyncConnection db = session.Connection;

                // First check if this tag is used in any games
                int tagGameCount = await db.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM TagGame WHERE TagId = ?", tag.Id);

                if (tagGameCount > 0)
                {
                    _logger.LogInformation("Tag {TagId} ({TagName}) is used in {Count} games, " +
                        "related relationships will be deleted", tag.Id, tag.Name, tagGameCount);
                }

                int affected = 0;
                await db.RunInTransactionAsync(tran =>
                {
                    // First delete any relationships in TagGame
                    int relationshipsDeleted = tran.Execute("DELETE FROM TagGame WHERE TagId = ?", tag.Id);
                    _logger.LogDebug("Deleted {Count} TagGame relationships for tag {TagId}",
                        relationshipsDeleted, tag.Id);
                    affected += relationshipsDeleted;

                    // Then delete the tag
                    affected += tran.Delete(tag);

                    // Verify deletion
                    int remainingCount = tran.ExecuteScalar<int>(
                        "SELECT COUNT(*) FROM Tags WHERE Id = ?", tag.Id);
                    if (remainingCount > 0)
                    {
                        _logger.LogError("Tag {TagId} was not deleted properly", tag.Id);
                        throw new InvalidOperationException("Failed to delete tag");
                    }

                    // Verify relationship deletion
                    int remainingRelationships = tran.ExecuteScalar<int>(
                        "SELECT COUNT(*) FROM TagGame WHERE TagId = ?", tag.Id);
                    if (remainingRelationships > 0)
                    {
                        _logger.LogWarning("Some TagGame relationships for Tag {TagId} were not deleted properly",
                            tag.Id);
                        _ = tran.Execute("DELETE FROM TagGame WHERE TagId = ?", tag.Id);
                    }
                });

                _logger.LogInformation("Successfully deleted tag {TagId} ({TagName}) with {Count} affected rows",
                    tag.Id, tag.Name, affected);
                return affected;
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Invalid data when deleting tag: {Message}", ex.Message);
                _errorHandler.HandleError(ex);
                return 0;
            }
            catch (SQLiteException ex)
            {
                _logger.LogError(ex, "Database error when deleting tag: {Message}", ex.Message);
                _errorHandler.HandleError(ex);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting tag: {TagName} - {Message}", tag.Name ?? "Unknown", ex.Message);
                _errorHandler.HandleError(ex);
                return 0;
            }
        }
    }
}
