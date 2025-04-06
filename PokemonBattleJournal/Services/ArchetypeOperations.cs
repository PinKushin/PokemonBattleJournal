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
            var db = await _factory.GetDatabaseAsync();
            try
            {
                await _factory.GetLock().WaitAsync();
                if (await db.Table<Archetype>().CountAsync() == 0)
                {
                    await db.InsertAllAsync(new List<Archetype>
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
                return new List<Archetype>();
            }
            finally
            {
                _factory.GetLock().Release();
            }
        }

        /// <summary>
        /// Gets an archetype by its ID.
        /// </summary>
        public async Task<Archetype?> GetByIdAsync(uint id)
        {
            var db = await _factory.GetDatabaseAsync();
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
                _factory.GetLock().Release();
            }
        }

        /// <summary>
        /// Saves an archetype to the database.
        /// </summary>
        public async Task<int> SaveAsync(string name, string imgPath, uint trainerId)
        {
            var db = await _factory.GetDatabaseAsync();
            var archetype = new Archetype
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving archetype: {Name}", name);
                ModalErrorHandler modalErrorHandler = new();
                modalErrorHandler.HandleError(ex);
                return 0;
            }
            finally
            {
                _factory.GetLock().Release();
            }
        }

        /// <summary>
        /// Deletes an archetype from the database.
        /// </summary>
        public async Task<int> DeleteAsync(Archetype archetype)
        {
            var db = await _factory.GetDatabaseAsync();
            try
            {
                await _factory.GetLock().WaitAsync();
                int affected = 0;
                await db.RunInTransactionAsync(tran =>
                {
                    affected = tran.Delete(archetype);
                });
                return affected;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting archetype: {Name}", archetype.Name);
                ModalErrorHandler modalErrorHandler = new();
                modalErrorHandler.HandleError(ex);
                return 0;
            }
            finally
            {
                _factory.GetLock().Release();
            }
        }
    }
}
