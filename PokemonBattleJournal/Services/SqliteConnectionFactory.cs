using SQLite;

namespace PokemonBattleJournal.Services;
public class SqliteConnectionFactory
{
    private static SQLiteAsyncConnection _database;
    private static readonly SemaphoreSlim _semaphore = new(1, 1);

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

