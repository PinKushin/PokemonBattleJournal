namespace PokemonBattleJournal.Services
{
    public class TrainerOperations : ITrainerOperations
    {
        private readonly ISqliteConnectionFactory _factory;
        private readonly ILogger _logger;
        private readonly IErrorHandler _errorHandler;

        internal TrainerOperations(ISqliteConnectionFactory factory, ILogger logger, IErrorHandler errorHandler)
        {
            _factory = factory;
            _logger = logger;
            _errorHandler = errorHandler;
        }

        /// <summary>
        /// Retrieves a list of all trainers from the database.
        /// </summary>
        public virtual async Task<List<Trainer>> GetAllAsync()
        {
            _logger.LogDebug("GetAllAsync: fetching all trainers");
            try
            {
                using DbSession session = await _factory.BeginAsync();
                List<Trainer> trainers = await session.Connection.Table<Trainer>().ToListAsync();
                _logger.LogDebug("GetAllAsync: returned {Count} trainers", trainers.Count);
                return trainers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving trainers");
                _errorHandler.HandleError(ex);
                return [];
            }
        }

        /// <inheritdoc/>
        public virtual async Task<Trainer?> GetActiveAsync()
        {
            _logger.LogDebug("GetActiveAsync: querying active trainer");
            try
            {
                using DbSession session = await _factory.BeginAsync();
                Trainer? active = await session.Connection.Table<Trainer>().Where(t => t.IsActive).FirstOrDefaultAsync();
                _logger.LogDebug("GetActiveAsync: active trainer = {Name} ({Id})", active?.Name, active?.Id);
                return active;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving active trainer");
                return null;
            }
        }

        /// <inheritdoc/>
        public virtual async Task SetActiveAsync(Trainer trainer)
        {
            _logger.LogDebug("SetActiveAsync: setting trainer {Name} ({Id}) as active", trainer.Name, trainer.Id);
            try
            {
                using DbSession session = await _factory.BeginAsync();
                await session.Connection.RunInTransactionAsync(tran =>
                {
                    tran.Execute("UPDATE Trainer SET IsActive = 0");
                    tran.Execute("UPDATE Trainer SET IsActive = 1 WHERE Id = ?", trainer.Id);
                });
                trainer.IsActive = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting active trainer {Id}", trainer.Id);
            }
        }

        /// <summary>
        /// Retrieves a trainer by name from the database.
        /// </summary>
        public virtual async Task<Trainer?> GetByNameAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Trainer name is required", nameof(name));
            }

            try
            {
                using DbSession session = await _factory.BeginAsync();
                return await session.Connection.Table<Trainer>()
                    .Where(i => i.Name == name)
                    .FirstOrDefaultAsync();
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Invalid data when retrieving trainer: {Message}", ex.Message);
                _errorHandler.HandleError(ex);
                return null;
            }
            catch (SQLiteException ex)
            {
                _logger.LogError(ex, "Database error when retrieving trainer: {Message}", ex.Message);
                _errorHandler.HandleError(ex);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving trainer by name: {Message}", ex.Message);
                _errorHandler.HandleError(ex);
                return null;
            }
        }

        /// <summary>
        /// Retrieves a trainer by ID from the database.
        /// </summary>
        public virtual async Task<Trainer?> GetByIdAsync(uint id)
        {
            try
            {
                using DbSession session = await _factory.BeginAsync();
                return await session.Connection.Table<Trainer>()
                    .Where(i => i.Id == id)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving trainer by id {Id}: {Message}", id, ex.Message);
                _errorHandler.HandleError(ex);
                return null;
            }
        }

        /// <summary>
        /// Deletes a trainer and all related records from the database.
        /// </summary>
        public virtual async Task<int> DeleteAsync(Trainer trainer)
        {
            if (trainer == null)
            {
                throw new ArgumentNullException(nameof(trainer), "Trainer cannot be null");
            }

            if (trainer.Id == 0)
            {
                throw new ArgumentException("Trainer ID is required", nameof(trainer));
            }

            try
            {
                using DbSession session = await _factory.BeginAsync();
                SQLiteAsyncConnection db = session.Connection;
                int affected = 0;

                // Verify the trainer exists
                Trainer existingTrainer = await db.Table<Trainer>()
                    .Where(t => t.Id == trainer.Id)
                    .FirstOrDefaultAsync();

                if (existingTrainer is null)
                    throw new ArgumentException($"Trainer with ID {trainer.Id} not found", nameof(trainer));

                // Load related records first (outside transaction)
                List<MatchEntry> matches = await db.Table<MatchEntry>()
                    .Where(m => m.TrainerId == trainer.Id)
                    .ToListAsync();

                List<Archetype> archetypes = await db.Table<Archetype>()
                    .Where(a => a.TrainerId == trainer.Id)
                    .ToListAsync();

                List<Tags> tags = await db.Table<Tags>()
                    .Where(t => t.TrainerId == trainer.Id)
                    .ToListAsync();

                // Delete everything in a transaction
                await db.RunInTransactionAsync(tran =>
                {
                    // Delete match-related games and tag relationships via SQL
                    // (match.Game1/2/3 are null since matches are loaded without children)
                    foreach (MatchEntry match in matches)
                    {
                        if (match.Game1Id.HasValue)
                        {
                            _ = tran.Execute("DELETE FROM TagGame WHERE GameId = ?", match.Game1Id.Value);
                            _ = tran.Execute("DELETE FROM Game WHERE Id = ?", match.Game1Id.Value);
                        }

                        if (match.Game2Id.HasValue)
                        {
                            _ = tran.Execute("DELETE FROM TagGame WHERE GameId = ?", match.Game2Id.Value);
                            _ = tran.Execute("DELETE FROM Game WHERE Id = ?", match.Game2Id.Value);
                        }

                        if (match.Game3Id.HasValue)
                        {
                            _ = tran.Execute("DELETE FROM TagGame WHERE GameId = ?", match.Game3Id.Value);
                            _ = tran.Execute("DELETE FROM Game WHERE Id = ?", match.Game3Id.Value);
                        }

                        affected += tran.Delete(match);
                    }

                    // Delete archetypes
                    foreach (Archetype archetype in archetypes)
                    {
                        affected += tran.Delete(archetype);
                    }

                    // Delete trainer tags
                    foreach (Tags tag in tags)
                    {
                        affected += tran.Delete(tag);
                    }

                    // Finally delete the trainer
                    affected += tran.Delete(trainer);

                    _logger.LogInformation("Successfully deleted trainer {TrainerId} ({TrainerName}) with {Count} affected rows",
                        trainer.Id, trainer.Name, affected);
                });

                // Verify all related records are properly deleted
                await VerifyDeletionAsync(db, trainer);

                return affected;
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Invalid data when deleting trainer: {Message}", ex.Message);
                _errorHandler.HandleError(ex);
                return 0;
            }
            catch (SQLiteException ex)
            {
                _logger.LogError(ex, "Database error when deleting trainer: {Message}", ex.Message);
                _errorHandler.HandleError(ex);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting trainer: {Message}", ex.Message);
                _errorHandler.HandleError(ex);
                return 0;
            }
        }
        /// <summary>
        /// Saves a trainer to the database. If the trainer has an ID, it updates the existing record; otherwise, it inserts a new record.
        /// </summary>
        /// <param name="trainerName">The name of a trainer to save.</param>
        /// <returns>The number of rows affected.</returns>
        public async Task<int> SaveAsync(string trainerName)
        {
            if (string.IsNullOrWhiteSpace(trainerName))
            {
                throw new ArgumentException("Trainer name is required", nameof(trainerName));
            }

            _logger.LogDebug("SaveAsync: saving trainer {Name}", trainerName);
            // Create the trainer instance
            Trainer trainer = new() { Name = trainerName };

            try
            {
                using DbSession session = await _factory.BeginAsync();
                int affected = 0;
                await session.Connection.RunInTransactionAsync(tran =>
                {
                    Trainer existingTrainer = tran.Table<Trainer>()
                        .Where(t => t.Name == trainerName && t.Id != trainer.Id)
                        .FirstOrDefault();

                    if (existingTrainer != null)
                    {
                        throw new InvalidOperationException($"A trainer with the name '{trainerName}' already exists");
                    }

                    affected = tran.Insert(trainer);
                });
                return affected;
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Invalid data when saving trainer: {Message}", ex.Message);
                _errorHandler.HandleError(ex);
                return 0;
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Operation error when saving trainer: {Message}", ex.Message);
                _errorHandler.HandleError(ex);
                return 0;
            }
            catch (SQLiteException ex)
            {
                _logger.LogError(ex, "Database error when saving trainer: {Message}", ex.Message);
                _errorHandler.HandleError(ex);
                return 0;
            }
            catch (Exception ex)
            {
                // Log the error
                _logger.LogError(ex, "Unexpected error saving trainer: {Message}", ex.Message);
                _errorHandler.HandleError(ex);
                return 0;
            }
        }

        /// <summary>
        /// Verifies that all related records for a trainer were properly deleted
        /// </summary>
        private async Task VerifyDeletionAsync(SQLiteAsyncConnection db, Trainer trainer)
        {
            // Check for remaining MatchEntry records
            int remainingMatches = await db.Table<MatchEntry>()
                .Where(m => m.TrainerId == trainer.Id)
                .CountAsync();

            if (remainingMatches > 0)
            {
                _logger.LogWarning("Some MatchEntry records for Trainer {TrainerId} were not deleted properly",
                    trainer.Id);

                // Attempt to clean up
                _ = await db.ExecuteAsync("DELETE FROM MatchEntry WHERE TrainerId = ?", trainer.Id);
            }

            // Check for remaining Archetype records
            int remainingArchetypes = await db.Table<Archetype>()
                .Where(a => a.TrainerId == trainer.Id)
                .CountAsync();

            if (remainingArchetypes > 0)
            {
                _logger.LogWarning("Some Archetype records for Trainer {TrainerId} were not deleted properly",
                    trainer.Id);

                // Attempt to clean up
                _ = await db.ExecuteAsync("DELETE FROM Archetype WHERE TrainerId = ?", trainer.Id);
            }

            // Check for remaining Tags records
            int remainingTags = await db.Table<Tags>()
                .Where(t => t.TrainerId == trainer.Id)
                .CountAsync();

            if (remainingTags > 0)
            {
                _logger.LogWarning("Some Tags records for Trainer {TrainerId} were not deleted properly",
                    trainer.Id);

                // Attempt to clean up
                _ = await db.ExecuteAsync("DELETE FROM Tags WHERE TrainerId = ?", trainer.Id);
            }

            // Finally check if the trainer was deleted
            int trainerExists = await db.Table<Trainer>()
                .Where(t => t.Id == trainer.Id)
                .CountAsync();

            if (trainerExists > 0)
            {
                _logger.LogError("Trainer {TrainerId} was not deleted properly", trainer.Id);
                throw new InvalidOperationException($"Failed to delete trainer with ID {trainer.Id}");
            }
        }
    }
}
