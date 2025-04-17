namespace PokemonBattleJournal.Services
{
    public class ArchetypeOperations : IArchetypeOperations
    {
        private readonly SqliteConnectionFactory _factory;
        private readonly ILogger _logger;

        internal ArchetypeOperations(SqliteConnectionFactory factory, ILogger logger)
        {
            _factory = factory;
            _logger = logger;
        }

        /// <summary>
        /// Gets all archetypes from the database, initializing defaults if none exist.
        /// </summary>
        public async Task<List<Archetype>> GetAllAsync()
        {
            SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
            try
            {
                await _factory.GetLock().WaitAsync();
                if (await db.Table<Archetype>().CountAsync() == 0)
                {
                    _ = await db.InsertAllAsync(new List<Archetype>
                    {
                        new() { Name = "Regidrago", ImagePath = "regidrago.png" },
                        new() { Name = "Charizard", ImagePath = "charizard.png" },
                        new() { Name = "Klawf", ImagePath = "klawf.png" },
                        new() { Name = "Snorlax Stall", ImagePath = "snorlax.png" },
                        new() { Name = "Raging Bolt", ImagePath = "raging_bolt.png" },
                        new() { Name = "Gardevoir", ImagePath = "gardevoir.png" },
                        new() { Name = "Miraidon", ImagePath = "miraidon.png" },
                        new() { Name = "Other", ImagePath = "ball_icon.png" }
                    });
                }
                return await db.Table<Archetype>().ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting archetypes");
                ModalErrorHandler modalErrorHandler = new();
                modalErrorHandler.HandleError(ex);
                return [];
            }
            finally
            {
                _ = _factory.GetLock().Release();
            }
        }

        /// <summary>
        /// Gets an archetype by its ID.
        /// </summary>
        public async Task<Archetype?> GetByIdAsync(uint id)
        {
            SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
            try
            {
                await _factory.GetLock().WaitAsync();
                return await db.Table<Archetype>()
                    .Where(i => i.Id == id)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting archetype by ID: {Id}", id);
                ModalErrorHandler modalErrorHandler = new();
                modalErrorHandler.HandleError(ex);
                return null;
            }
            finally
            {
                _ = _factory.GetLock().Release();
            }
        }

        /// <summary>
        /// Saves an archetype to the database.
        /// </summary>
        public async Task<int> SaveAsync(string name, string imgPath, uint trainerId)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Archetype name is required", nameof(name));
            }

            if (string.IsNullOrWhiteSpace(imgPath))
            {
                throw new ArgumentException("Archetype image path is required", nameof(imgPath));
            }

            if (trainerId == 0)
            {
                throw new ArgumentException("Trainer ID is required", nameof(trainerId));
            }

            SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
            Archetype archetype = new()
            {
                Name = name,
                ImagePath = imgPath,
                TrainerId = trainerId
            };

            try
            {
                await _factory.GetLock().WaitAsync();
                int affected = 0;
                await db.RunInTransactionAsync(tran =>
                {
                    if (archetype.Id != 0)
                    {
                        affected = tran.Update(archetype);
                    }
                    else
                    {
                        affected = tran.Insert(archetype);
                    }
                });
                return affected;
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Invalid data when saving archetype: {Message}", ex.Message);
                ModalErrorHandler modalErrorHandler = new();
                modalErrorHandler.HandleError(ex);
                return 0;
            }
            catch (SQLiteException ex)
            {
                _logger.LogError(ex, "Database error when saving archetype: {Message}", ex.Message);
                ModalErrorHandler modalErrorHandler = new();
                modalErrorHandler.HandleError(ex);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving archetype: {Name} - {Message}", name, ex.Message);
                ModalErrorHandler modalErrorHandler = new();
                modalErrorHandler.HandleError(ex);
                return 0;
            }
            finally
            {
                _ = _factory.GetLock().Release();
            }
        }

        /// <summary>
        /// Deletes an archetype from the database.
        /// </summary>
        public async Task<int> DeleteAsync(Archetype archetype)
        {
            if (archetype == null)
            {
                throw new ArgumentNullException(nameof(archetype), "Archetype cannot be null");
            }

            if (archetype.Id == 0)
            {
                throw new ArgumentException("Archetype ID is required", nameof(archetype));
            }

            SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
            try
            {
                await _factory.GetLock().WaitAsync();

                // Check if this archetype is used in any matches first
                int matchCount = await db.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM MatchEntry WHERE PlayingId = ? OR AgainstId = ?",
                    archetype.Id, archetype.Id);

                if (matchCount > 0)
                {
                    _logger.LogWarning("Archetype {ArchetypeId} ({ArchetypeName}) is used in {Count} matches",
                        archetype.Id, archetype.Name, matchCount);
                    throw new InvalidOperationException(
                        $"Cannot delete archetype '{archetype.Name}' because it is used in {matchCount} matches");
                }

                int affected = 0;
                await db.RunInTransactionAsync(tran =>
                {
                    affected = tran.Delete(archetype);

                    // Verify deletion
                    int remainingCount = tran.ExecuteScalar<int>(
                        "SELECT COUNT(*) FROM Archetype WHERE Id = ?", archetype.Id);
                    if (remainingCount > 0)
                    {
                        _logger.LogError("Archetype {ArchetypeId} was not deleted properly", archetype.Id);
                        throw new Exception("Failed to delete archetype");
                    }
                });

                _logger.LogInformation("Successfully deleted archetype {ArchetypeId} ({ArchetypeName})",
                    archetype.Id, archetype.Name);
                return affected;
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Invalid data when deleting archetype: {Message}", ex.Message);
                ModalErrorHandler modalErrorHandler = new();
                modalErrorHandler.HandleError(ex);
                return 0;
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Cannot delete archetype: {Message}", ex.Message);
                ModalErrorHandler modalErrorHandler = new();
                modalErrorHandler.HandleError(ex);
                return 0;
            }
            catch (SQLiteException ex)
            {
                _logger.LogError(ex, "Database error when deleting archetype: {Message}", ex.Message);
                ModalErrorHandler modalErrorHandler = new();
                modalErrorHandler.HandleError(ex);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting archetype: {Name} - {Message}", archetype.Name, ex.Message);
                ModalErrorHandler modalErrorHandler = new();
                modalErrorHandler.HandleError(ex);
                return 0;
            }
            finally
            {
                _ = _factory.GetLock().Release();
            }
        }
    }
}
