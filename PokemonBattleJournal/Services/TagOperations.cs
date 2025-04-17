namespace PokemonBattleJournal.Services
{
    public class TagOperations : ITagOperations
    {
        private readonly SqliteConnectionFactory _factory;
        private readonly ILogger _logger;

        internal TagOperations(SqliteConnectionFactory factory, ILogger logger)
        {
            _factory = factory;
            _logger = logger;
        }

        /// <summary>
        /// Gets all tags from the database, initializing default tags if none exist.
        /// </summary>
        public async Task<List<Tags>> GetAllAsync()
        {
            SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
            try
            {
                await _factory.GetLock().WaitAsync();
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
                ModalErrorHandler error = new();
                _logger.LogError(ex, "Error getting tags");
                error.HandleError(ex);
                return [];
            }
            finally
            {
                _ = _factory.GetLock().Release();
            }
        }

        /// <summary>
        /// Gets a tag by its ID.
        /// </summary>
        public async Task<Tags?> GetByIdAsync(uint id)
        {
            SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
            try
            {
                await _factory.GetLock().WaitAsync();
                return await db.Table<Tags>()
                    .Where(i => i.Id == id)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                ModalErrorHandler error = new();
                _logger.LogError(ex, "Error getting tag by ID: {Id}", id);
                error.HandleError(ex);
                return null;
            }
            finally
            {
                _ = _factory.GetLock().Release();
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

            SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
            Tags tag = new()
            { Name = tagTxt, TrainerId = trainerId };

            try
            {
                await _factory.GetLock().WaitAsync();
                int affected = 0;
                await db.RunInTransactionAsync(tran =>
                {
                    if (tag.Id != 0)
                    {
                        affected = tran.Update(tag);
                    }
                    else
                    {
                        affected = tran.Insert(tag);
                    }
                });
                return affected;
            }
            catch (ArgumentException ex)
            {
                ModalErrorHandler error = new();
                _logger.LogError(ex, "Invalid data when saving tag: {Message}", ex.Message);
                error.HandleError(ex);
                return 0;
            }
            catch (SQLiteException ex)
            {
                ModalErrorHandler error = new();
                _logger.LogError(ex, "Database error when saving tag: {Message}", ex.Message);
                error.HandleError(ex);
                return 0;
            }
            catch (Exception ex)
            {
                ModalErrorHandler error = new();
                _logger.LogError(ex, "Error saving tag: {TagName} - {Message}", tagTxt, ex.Message);
                error.HandleError(ex);
                return 0;
            }
            finally
            {
                _ = _factory.GetLock().Release();
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

            SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
            try
            {
                await _factory.GetLock().WaitAsync();

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
                        throw new Exception("Failed to delete tag");
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
                ModalErrorHandler error = new();
                _logger.LogError(ex, "Invalid data when deleting tag: {Message}", ex.Message);
                error.HandleError(ex);
                return 0;
            }
            catch (SQLiteException ex)
            {
                ModalErrorHandler error = new();
                _logger.LogError(ex, "Database error when deleting tag: {Message}", ex.Message);
                error.HandleError(ex);
                return 0;
            }
            catch (Exception ex)
            {
                ModalErrorHandler error = new();
                _logger.LogError(ex, "Error deleting tag: {TagName} - {Message}", tag.Name ?? "Unknown", ex.Message);
                error.HandleError(ex);
                return 0;
            }
            finally
            {
                _ = _factory.GetLock().Release();
            }
        }
    }
}
