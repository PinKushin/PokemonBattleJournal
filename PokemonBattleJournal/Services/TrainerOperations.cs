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
            var db = await _factory.GetDatabaseAsync();
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
                return new List<Trainer>();
            }
            finally
            {
                _factory.GetLock().Release();
            }
        }

        /// <summary>
        /// Retrieves a trainer by name from the database.
        /// </summary>
        public virtual async Task<Trainer?> GetByNameAsync(string name)
        {
            var db = await _factory.GetDatabaseAsync();
            try
            {
                await _factory.GetLock().WaitAsync();
                return await db.Table<Trainer>()
                    .Where(i => i.Name == name)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                ModalErrorHandler error = new();
                _logger.LogError(ex, "Error retrieving trainer by name");
                error.HandleError(ex);
                return null;
            }
            finally
            {
                _factory.GetLock().Release();
            }
        }

        /// <summary>
        /// Deletes a trainer and all related records from the database.
        /// </summary>
        public virtual async Task<int> DeleteAsync(Trainer trainer)
        {
            var db = await _factory.GetDatabaseAsync();
            try
            {
                int affected = 0;
                await _factory.GetLock().WaitAsync();

                // Load related records first (outside transaction)
                var matches = await db.Table<MatchEntry>()
                    .Where(m => m.TrainerId == trainer.Id)
                    .ToListAsync();

                var archetypes = await db.Table<Archetype>()
                    .Where(a => a.TrainerId == trainer.Id)
                    .ToListAsync();

                var tags = await db.Table<Tags>()
                    .Where(t => t.TrainerId == trainer.Id)
                    .ToListAsync();

                // Delete everything in a transaction
                await db.RunInTransactionAsync(tran =>
                {
                    // Delete matches (using synchronous operations inside transaction)
                    foreach (var match in matches)
                    {
                        // Delete match's related data
                        if (match.Game1 != null)
                            DeleteGameAndTags(tran, match.Game1);
                        if (match.Game2 != null)
                            DeleteGameAndTags(tran, match.Game2);
                        if (match.Game3 != null)
                            DeleteGameAndTags(tran, match.Game3);

                        affected += tran.Delete(match);
                    }

                    // Delete archetypes
                    foreach (var archetype in archetypes)
                    {
                        affected += tran.Delete(archetype);
                    }

                    // Delete trainer tags
                    foreach (var tag in tags)
                    {
                        affected += tran.Delete(tag);
                    }

                    // Finally delete the trainer
                    affected += tran.Delete(trainer);
                });

                return affected;
            }
            catch (Exception ex)
            {
                ModalErrorHandler error = new();
                _logger.LogError(ex, "Error deleting trainer");
                error.HandleError(ex);
                return 0;
            }
            finally
            {
                _factory.GetLock().Release();
            }
        }
        // Helper method to delete a game and its tags
        private void DeleteGameAndTags(SQLiteConnection tran, Game game)
        {
            // Delete associated tags first
            if (game.Tags != null)
            {
                foreach (var tag in game.Tags)
                {
                    tran.Delete(tag);
                }
            }
            // Then delete the game
            tran.Delete(game);
        }

        /// <summary>
        /// Saves a trainer to the database. If the trainer has an ID, it updates the existing record; otherwise, it inserts a new record.
        /// </summary>
        /// <param name="trainerName">The name of a trainer to save.</param>
        /// <returns>The number of rows affected.</returns>
        public async Task<int> SaveAsync(string trainerName)
        {
            Trainer trainer = new() { Name = trainerName };
            SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
            try
            {
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
            catch (Exception ex)
            {
                // Log the error
                ModalErrorHandler errorHandler = new ModalErrorHandler();
                errorHandler.HandleError(ex);
                return 0;
            }
            finally
            {
                _factory.GetLock().Release();
            }
        }
    }
}
