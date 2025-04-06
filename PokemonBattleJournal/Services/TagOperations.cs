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
            var db = await _factory.GetDatabaseAsync();
            try
            {
                await _factory.GetLock().WaitAsync();
                if (await db.Table<Tags>().CountAsync() == 0)
                {
                    await db.InsertAllAsync(new List<Tags>
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
                return new List<Tags>();
            }
            finally
            {
                _factory.GetLock().Release();
            }
        }

        /// <summary>
        /// Gets a tag by its ID.
        /// </summary>
        public async Task<Tags?> GetByIdAsync(uint id)
        {
            var db = await _factory.GetDatabaseAsync();
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
                _factory.GetLock().Release();
            }
        }

        /// <summary>
        /// Saves a tag to the database.
        /// </summary>
        public async Task<int> SaveAsync(string tagTxt, uint trainerId)
        {
            var db = await _factory.GetDatabaseAsync();
            var tag = new Tags { Name = tagTxt, TrainerId = trainerId };

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
            catch (Exception ex)
            {
                ModalErrorHandler error = new();
                _logger.LogError(ex, "Error saving tag: {TagName}", tagTxt);
                error.HandleError(ex);
                return 0;
            }
            finally
            {
                _factory.GetLock().Release();
            }
        }

        /// <summary>
        /// Deletes a tag from the database.
        /// </summary>
        public async Task<int> DeleteAsync(Tags tag)
        {
            var db = await _factory.GetDatabaseAsync();
            try
            {
                await _factory.GetLock().WaitAsync();
                int affected = 0;
                await db.RunInTransactionAsync(tran =>
                {
                    affected = tran.Delete(tag);
                });
                return affected;
            }
            catch (Exception ex)
            {
                ModalErrorHandler error = new();
                _logger.LogError(ex, "Error deleting tag: {TagName}", tag.Name);
                error.HandleError(ex);
                return 0;
            }
            finally
            {
                _factory.GetLock().Release();
            }
        }
    }
}
