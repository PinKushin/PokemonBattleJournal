using SQLite;

namespace PokemonBattleJournal.Services;

/// <summary>
/// Provides methods for interacting with the SQLite database.
/// </summary>
public class SqliteConnectionFactory
{
    private static SQLiteAsyncConnection _database;
    private static readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>
    /// Initializes the SQLite database connection and creates tables if they do not exist.
    /// </summary>
    static async Task InitAsync()
    {
        if (_database is not null)
            return;

        await _semaphore.WaitAsync();
        try
        {
            if (_database is not null)
                return;

            _database = new SQLiteAsyncConnection(Constants.DatabasePath, Constants.Flags);
            await _database.CreateTableAsync<Trainer>();
            await _database.CreateTableAsync<Archetype>();
            await _database.CreateTableAsync<MatchEntry>();
            await _database.CreateTableAsync<Tags>();
            await _database.CreateTableAsync<Game>();
        }
        catch (Exception ex)
        {
            // Log the error
            ModalErrorHandler errorHandler = new ModalErrorHandler();
            errorHandler.HandleError(ex);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Retrieves a list of all trainers from the database.
    /// </summary>
    /// <returns>A list of trainers.</returns>
    public virtual async Task<List<Trainer>> GetTrainersAsync()
    {
        await InitAsync();
        await _semaphore.WaitAsync();
        try
        {
            return await _database.Table<Trainer>().ToListAsync();
        }
        catch (Exception ex)
        {
            // Log the error
            ModalErrorHandler errorHandler = new ModalErrorHandler();
            errorHandler.HandleError(ex);
            return new List<Trainer>(); // Return an empty list in case of an error
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Retrieves a trainer by name from the database.
    /// </summary>
    /// <param name="name">The name of the trainer.</param>
    /// <returns>The trainer with the specified name, or null if not found.</returns>
    public virtual async Task<Trainer?> GetTrainerByNameAsync(string name)
    {
        await InitAsync();
        await _semaphore.WaitAsync();
        try
        {
            return await _database.Table<Trainer>().Where(i => i.Name == name).FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            // Log the error
            ModalErrorHandler errorHandler = new ModalErrorHandler();
            errorHandler.HandleError(ex);
            return null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Saves a trainer to the database. If the trainer has an ID, it updates the existing record; otherwise, it inserts a new record.
    /// </summary>
    /// <param name="trainer">The trainer to save.</param>
    /// <returns>The number of rows affected.</returns>
    public virtual async Task<int> SaveTrainerAsync(Trainer trainer)
    {
        await InitAsync();
        await _semaphore.WaitAsync();
        try
        {
            int saved = 0;
            await _database.RunInTransactionAsync(tran =>
            {
                if (trainer.Id != 0)
                {
                    saved = tran.Update(trainer);
                }
                else
                {
                    saved = tran.Insert(trainer);
                }
            });
            return saved;
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
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Deletes a trainer from the database.
    /// </summary>
    /// <param name="trainer">The trainer to delete.</param>
    /// <returns>The number of rows affected.</returns>
    public virtual async Task<int> DeleteTrainerAsync(Trainer trainer)
    {
        await InitAsync();
        await _semaphore.WaitAsync();
        try
        {
            int del = 0;
            await _database.RunInTransactionAsync(tran =>
            {
                del = tran.Delete(trainer);
            });
            return del;
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
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Saves a match entry to the database. If the match entry has an ID, it updates the existing record; otherwise, it inserts a new record.
    /// </summary>
    /// <param name="matchEntry">The match entry to save.</param>
    /// <returns>The number of rows affected.</returns>
    public virtual async Task<int> SaveMatchEntryAsync(MatchEntry matchEntry)
    {
        await InitAsync();
        await _semaphore.WaitAsync();
        try
        {
            int saved = 0;
            await _database.RunInTransactionAsync(tran =>
            {
                if (matchEntry.Id != 0)
                {
                    saved = tran.Update(matchEntry);
                }
                else
                {
                    saved = tran.Insert(matchEntry);
                }
            });
            return saved;
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
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Retrieves a list of all match entries from the database.
    /// </summary>
    /// <returns>A list of match entries.</returns>
    public virtual async Task<List<MatchEntry>> GetMatchEntriesAsync()
    {
        await InitAsync();
        await _semaphore.WaitAsync();
        try
        {
            return await _database.Table<MatchEntry>().ToListAsync();
        }
        catch (Exception ex)
        {
            // Log the error
            ModalErrorHandler errorHandler = new ModalErrorHandler();
            errorHandler.HandleError(ex);
            return new List<MatchEntry>(); // Return an empty list in case of an error
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Retrieves a list of match entries by trainer ID from the database.
    /// </summary>
    /// <param name="trainerId">The ID of the trainer.</param>
    /// <returns>A list of match entries for the specified trainer.</returns>
    public virtual async Task<List<MatchEntry>> GetMatchEntriesByTrainerIdAsync(uint trainerId)
    {
        await InitAsync();
        await _semaphore.WaitAsync();
        try
        {
            return await _database.Table<MatchEntry>().Where(i => i.TrainerId == trainerId).ToListAsync();
        }
        catch (Exception ex)
        {
            // Log the error
            ModalErrorHandler errorHandler = new ModalErrorHandler();
            errorHandler.HandleError(ex);
            return new List<MatchEntry>(); // Return an empty list in case of an error
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Retrieves a match entry by ID from the database.
    /// </summary>
    /// <param name="id">The ID of the match entry.</param>
    /// <returns>The match entry with the specified ID, or null if not found.</returns>
    public virtual async Task<MatchEntry?> GetMatchEntryByIdAsync(uint id)
    {
        await InitAsync();
        await _semaphore.WaitAsync();
        try
        {
            return await _database.Table<MatchEntry>().Where(i => i.Id == id).FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            // Log the error
            ModalErrorHandler errorHandler = new ModalErrorHandler();
            errorHandler.HandleError(ex);
            return null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Deletes a match entry from the database.
    /// </summary>
    /// <param name="matchEntry">The match entry to delete.</param>
    /// <returns>The number of rows affected.</returns>
    public virtual async Task<int> DeleteMatchEntryAsync(MatchEntry matchEntry)
    {
        await InitAsync();
        await _semaphore.WaitAsync();
        try
        {
            int del = 0;
            await _database.RunInTransactionAsync(tran =>
            {
                del = tran.Delete(matchEntry);
            });
            return del;
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
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Saves an archetype to the database. If the archetype has an ID, it updates the existing record; otherwise, it inserts a new record.
    /// </summary>
    /// <param name="archetype">The archetype to save.</param>
    /// <returns>The number of rows affected.</returns>
    public virtual async Task<int> SaveArchetypeAsync(Archetype archetype)
    {
        await InitAsync();
        await _semaphore.WaitAsync();
        try
        {
            int saved = 0;
            await _database.RunInTransactionAsync(tran =>
            {
                if (archetype.Id != 0)
                {
                    saved = tran.Update(archetype);
                }
                else
                {
                    saved = tran.Insert(archetype);
                }
            });
            return saved;
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
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Retrieves a list of all archetypes from the database.
    /// </summary>
    /// <returns>A list of archetypes.</returns>
    public virtual async Task<List<Archetype>> GetArchetypesAsync()
    {
        await InitAsync();
        await _semaphore.WaitAsync();
        try
        {
            if (_database.Table<Archetype>().CountAsync().Result == 0)
            {
                await _database.InsertAllAsync(new List<Archetype>
                    {
                        new Archetype { Name = "Regidrago", ImagePath = "regidrago.png" },
                        new Archetype { Name = "Charizard", ImagePath = "charizard.png" },
                        new Archetype { Name = "Klawf", ImagePath = "klawf.png" },
                        new Archetype { Name = "Snorlax Stall", ImagePath = "snorlax.png" },
                        new Archetype { Name = "Raging Bolt", ImagePath = "raging_bolt.png" },
                        new Archetype { Name = "Gardevoir", ImagePath = "gardevoir.png" },
                        new Archetype { Name = "Miraidon", ImagePath = "miraidon.png" },
                        new Archetype { Name = "Other", ImagePath = "ball_icon.png" }
                    });
            }
            return await _database.Table<Archetype>().ToListAsync();
        }
        catch (Exception ex)
        {
            // Log the error
            ModalErrorHandler errorHandler = new();
            errorHandler.HandleError(ex);
            return new List<Archetype>(); // Return an empty list in case of an error
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Retrieves an archetype by ID from the database.
    /// </summary>
    /// <param name="id">The ID of the archetype.</param>
    /// <returns>The archetype with the specified ID, or null if not found.</returns>
    public virtual async Task<Archetype?> GetArchetypeByIdAsync(uint id)
    {
        await InitAsync();
        await _semaphore.WaitAsync();
        try
        {
            return await _database.Table<Archetype>().Where(i => i.Id == id).FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            // Log the error
            ModalErrorHandler errorHandler = new();
            errorHandler.HandleError(ex);
            return null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Deletes an archetype from the database.
    /// </summary>
    /// <param name="archetype">The archetype to delete.</param>
    /// <returns>The number of rows affected.</returns>
    public virtual async Task<int> DeleteArchetypeAsync(Archetype archetype)
    {
        await InitAsync();
        await _semaphore.WaitAsync();
        try
        {
            await _database.RunInTransactionAsync(tran =>
            {
                tran.Delete(archetype);
            });
            return 1;
        }
        catch (Exception ex)
        {
            // Log the error
            ModalErrorHandler errorHandler = new();
            errorHandler.HandleError(ex);
            return 0;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Saves a tag to the database. If the tag has an ID, it updates the existing record; otherwise, it inserts a new record.
    /// </summary>
    /// <param name="tag">The tag to save.</param>
    /// <returns>The number of rows affected.</returns>
    public virtual async Task<int> SaveTagAsync(Tags tag)
    {
        await InitAsync();
        await _semaphore.WaitAsync();
        try
        {
            int saved = 0;
            await _database.RunInTransactionAsync(tran =>
            {
                if (tag.Id != 0)
                {
                    saved = tran.Update(tag);
                }
                else
                {
                    saved = tran.Insert(tag);
                }
            });
            return saved;
        }
        catch (Exception ex)
        {
            // Log the error
            ModalErrorHandler errorHandler = new();
            errorHandler.HandleError(ex);
            return 0;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Retrieves a list of all tags from the database.
    /// </summary>
    /// <returns>A list of tags.</returns>
    public virtual async Task<List<Tags>> GetTagsAsync()
    {
        await InitAsync();
        await _semaphore.WaitAsync();
        try
        {
            if (_database.Table<Tags>().CountAsync().Result == 0)
            {
                await _database.InsertAllAsync(new List<Tags>
                    {
                        new Tags { Name = "Early Start" },
                        new Tags { Name = "Behind Early" },
                        new Tags { Name = "Donked Rival" },
                        new Tags { Name = "Got Donked" },
                        new Tags { Name = "Lucky" },
                        new Tags { Name = "Unlucky" },
                        new Tags { Name = "Never Punished" },
                        new Tags { Name = "Punished" }
                    });
            }
            return await _database.Table<Tags>().ToListAsync();
        }
        catch (Exception ex)
        {
            // Log the error
            ModalErrorHandler errorHandler = new();
            errorHandler.HandleError(ex);
            return new List<Tags>(); // Return an empty list in case of an error
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Retrieves a tag by ID from the database.
    /// </summary>
    /// <param name="id">The ID of the tag.</param>
    /// <returns>The tag with the specified ID, or null if not found.</returns>
    public virtual async Task<Tags?> GetTagByIdAsync(uint id)
    {
        await InitAsync();
        await _semaphore.WaitAsync();
        try
        {
            return await _database.Table<Tags>().Where(i => i.Id == id).FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            // Log the error
            ModalErrorHandler errorHandler = new();
            errorHandler.HandleError(ex);
            return null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Deletes a tag from the database.
    /// </summary>
    /// <param name="tag">The tag to delete.</param>
    /// <returns>The number of rows affected.</returns>
    public virtual async Task<int> DeleteTagAsync(Tags tag)
    {
        await InitAsync();
        await _semaphore.WaitAsync();
        try
        {
            int del = 0;
            await _database.RunInTransactionAsync(tran =>
            {
                del = tran.Delete(tag);
            });
            return del;
        }
        catch (Exception ex)
        {
            ModalErrorHandler errorHandler = new();
            errorHandler.HandleError(ex);
            return 0;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Saves a game to the database. If the game has an ID, it updates the existing record; otherwise, it inserts a new record.
    /// </summary>
    /// <param name="game">The game to save.</param>
    /// <returns>The number of rows affected.</returns>
    public virtual async Task<int> SaveGameAsync(Game game)
    {
        await InitAsync();
        await _semaphore.WaitAsync();
        try
        {
            int saved = 0;
            await _database.RunInTransactionAsync(tran =>
            {
                if (game.Id != 0)
                {
                    saved = tran.Update(game);
                }
                else
                {
                    saved = tran.Insert(game);
                }
            });
            return saved;
        }
        catch (Exception ex)
        {
            // Log the error
            ModalErrorHandler errorHandler = new();
            errorHandler.HandleError(ex);
            return 0;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Retrieves a list of all games from the database.
    /// </summary>
    /// <returns>A list of games.</returns>
    public virtual async Task<List<Game>> GetGamesAsync()
    {
        await InitAsync();
        await _semaphore.WaitAsync();
        try
        {
            return await _database.Table<Game>().ToListAsync();
        }
        catch (Exception ex)
        {
            ModalErrorHandler errorHandler = new();
            errorHandler.HandleError(ex);
            return new List<Game>();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Retrieves a game by ID from the database.
    /// </summary>
    /// <param name="id">The ID of the game.</param>
    /// <returns>The game with the specified ID, or null if not found.</returns>
    public virtual async Task<Game?> GetGameByIdAsync(uint id)
    {
        await InitAsync();
        await _semaphore.WaitAsync();
        try
        {
            return await _database.Table<Game>().Where(i => i.Id == id).FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            ModalErrorHandler errorHandler = new();
            errorHandler.HandleError(ex);
            return null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Deletes a game from the database.
    /// </summary>
    /// <param name="game">The game to delete.</param>
    /// <returns>The number of rows affected.</returns>
    public virtual async Task<int> DeleteGameAsync(Game game)
    {
        await InitAsync();
        await _semaphore.WaitAsync();
        try
        {
            int del = 0;
            await _database.RunInTransactionAsync(tran =>
            {
                del = tran.Delete(game);
            });
            return del;
        }
        catch (Exception ex)
        {
            ModalErrorHandler errorHandler = new();
            errorHandler.HandleError(ex);
            return 0;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Saves a match entry and its associated games to the database in a single transaction.
    /// </summary>
    /// <param name="matchEntry">The match entry to save.</param>
    /// <param name="games">The list of games associated with the match entry.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual async Task SaveMatchEntryWithGamesAsync(MatchEntry matchEntry, List<Game> games)
    {
        await InitAsync();
        await _semaphore.WaitAsync();
        try
        {
            await _database.RunInTransactionAsync(tran =>
            {
                if (matchEntry.Id != 0)
                {
                    tran.Update(matchEntry);
                }
                else
                {
                    tran.Insert(matchEntry);
                }

                foreach (var game in games)
                {
                    if (game.Id != 0)
                    {
                        tran.Update(game);
                    }
                    else
                    {
                        tran.Insert(game);
                    }
                }
            });
        }
        catch (Exception ex)
        {
            // Log the error
            ModalErrorHandler errorHandler = new ModalErrorHandler();
            errorHandler.HandleError(ex);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}

