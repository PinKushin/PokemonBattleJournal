namespace PokemonBattleJournal.Services
{
    public class TrainerOperations : ITrainerOperations
    {
        private readonly SqliteConnectionFactory _factory;
        private readonly ILogger _logger;

        internal TrainerOperations(SqliteConnectionFactory factory, ILogger logger)
        {
            _factory = factory;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves a list of all trainers from the database.
        /// </summary>
        public virtual async Task<List<Trainer>> GetAllAsync()
        {
            SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
            try
            {
                await _factory.GetLock().WaitAsync();
                return await db.Table<Trainer>().ToListAsync();
            }
            catch (Exception ex)
            {
                ModalErrorHandler error = new();
                _logger.LogError(ex, "Error retrieving trainers");
                error.HandleError(ex);
                return [];
            }
            finally
            {
                _ = _factory.GetLock().Release();
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

            SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
            try
            {
                await _factory.GetLock().WaitAsync();
                return await db.Table<Trainer>()
                    .Where(i => i.Name == name)
                    .FirstOrDefaultAsync();
            }
            catch (ArgumentException ex)
            {
                ModalErrorHandler error = new();
                _logger.LogError(ex, "Invalid data when retrieving trainer: {Message}", ex.Message);
                error.HandleError(ex);
                return null;
            }
            catch (SQLiteException ex)
            {
                ModalErrorHandler error = new();
                _logger.LogError(ex, "Database error when retrieving trainer: {Message}", ex.Message);
                error.HandleError(ex);
                return null;
            }
            catch (Exception ex)
            {
                ModalErrorHandler error = new();
                _logger.LogError(ex, "Error retrieving trainer by name: {Message}", ex.Message);
                error.HandleError(ex);
                return null;
            }
            finally
            {
                _ = _factory.GetLock().Release();
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

            SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
            try
            {
                int affected = 0;
                await _factory.GetLock().WaitAsync();

                // Verify the trainer exists
                Trainer existingTrainer = await db.Table<Trainer>()
                    .Where(t => t.Id == trainer.Id)
                    .FirstOrDefaultAsync();

                if (existingTrainer is null)
                {
                    throw new ArgumentException($"Trainer with ID {trainer.Id} not found", nameof(trainer));
                }

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
                    // Delete matches (using synchronous operations inside transaction)
                    foreach (MatchEntry match in matches)
                    {
                        // Delete match's related data
                        if (match.Game1 != null)
                        {
                            DeleteGameAndTags(tran, match.Game1);
                        }

                        if (match.Game2 != null)
                        {
                            DeleteGameAndTags(tran, match.Game2);
                        }

                        if (match.Game3 != null)
                        {
                            DeleteGameAndTags(tran, match.Game3);
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
                ModalErrorHandler error = new();
                _logger.LogError(ex, "Invalid data when deleting trainer: {Message}", ex.Message);
                error.HandleError(ex);
                return 0;
            }
            catch (SQLiteException ex)
            {
                ModalErrorHandler error = new();
                _logger.LogError(ex, "Database error when deleting trainer: {Message}", ex.Message);
                error.HandleError(ex);
                return 0;
            }
            catch (Exception ex)
            {
                ModalErrorHandler error = new();
                _logger.LogError(ex, "Error deleting trainer: {Message}", ex.Message);
                error.HandleError(ex);
                return 0;
            }
            finally
            {
                _ = _factory.GetLock().Release();
            }
        }
        // Helper method to delete a game and its tags
        private void DeleteGameAndTags(SQLiteConnection tran, Game game)
        {
            // Delete associated tags first
            if (game.Tags != null)
            {
                foreach (Tags tag in game.Tags)
                {
                    _ = tran.Delete(tag);
                }
            }
            // Then delete the game
            _ = tran.Delete(game);
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

            // Create the trainer instance
            Trainer trainer = new() { Name = trainerName };
            SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();

            try
            {
                // Check for duplicate name
                Trainer existingTrainer = await db.Table<Trainer>()
                    .Where(t => t.Name == trainerName && t.Id != trainer.Id)
                    .FirstOrDefaultAsync();

                if (existingTrainer != null)
                {
                    throw new InvalidOperationException($"A trainer with the name '{trainerName}' already exists");
                }

                int affected = 0;
                await _factory.GetLock().WaitAsync();
                await db.RunInTransactionAsync(tran =>
                {
                    if (trainer.Id != 0)
                    {
                        affected = tran.Update(trainer);
                    }
                    else
                    {
                        affected = tran.Insert(trainer);
                    }
                });
                return affected;
            }
            catch (ArgumentException ex)
            {
                ModalErrorHandler error = new();
                _logger.LogError(ex, "Invalid data when saving trainer: {Message}", ex.Message);
                error.HandleError(ex);
                return 0;
            }
            catch (InvalidOperationException ex)
            {
                ModalErrorHandler error = new();
                _logger.LogError(ex, "Operation error when saving trainer: {Message}", ex.Message);
                error.HandleError(ex);
                return 0;
            }
            catch (SQLiteException ex)
            {
                ModalErrorHandler error = new();
                _logger.LogError(ex, "Database error when saving trainer: {Message}", ex.Message);
                error.HandleError(ex);
                return 0;
            }
            catch (Exception ex)
            {
                // Log the error
                ModalErrorHandler errorHandler = new();
                _logger.LogError(ex, "Unexpected error saving trainer: {Message}", ex.Message);
                errorHandler.HandleError(ex);
                return 0;
            }
            finally
            {
                _ = _factory.GetLock().Release();
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
                throw new Exception($"Failed to delete trainer with ID {trainer.Id}");
            }
        }
    }
}
