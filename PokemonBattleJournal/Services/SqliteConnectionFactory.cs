using Microsoft.Extensions.Logging;
using SQLite;
using SQLiteNetExtensionsAsync.Extensions;

namespace PokemonBattleJournal.Services;

/// <summary>
/// Provides methods for interacting with the SQLite database.
/// </summary>
public class SqliteConnectionFactory
{
    private static SQLiteAsyncConnection _database;
    private static readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly ILogger<SqliteConnectionFactory> _logger;

    public SqliteConnectionFactory(ILogger<SqliteConnectionFactory> logger)
    {
        _logger = logger;
    }

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
    /// <param name="trainerName">The name of a trainer to save.</param>
    /// <returns>The number of rows affected.</returns>
    public virtual async Task<int> SaveTrainerAsync(string trainerName)
    {
        await InitAsync();
        await _semaphore.WaitAsync();
        Trainer trainer = new() { Name = trainerName };
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
    /// Saves an archetype to the database. If the archetype has an ID, it updates the existing record; otherwise, it inserts a new record.
    /// </summary>
    /// <param name="name">The name of the archetype to save.</param>
    /// <param name="imgPath">The image path of the archetype to save.</param>
    /// <param name="trainerId">The ID of the trainer who created the archetype.</param>
    /// <returns>The number of rows affected.</returns>
    public virtual async Task<int> SaveArchetypeAsync(string name, string imgPath, uint trainerId)
    {
        await InitAsync();
        await _semaphore.WaitAsync();
        Archetype archetype = new() { Name = name, ImagePath = imgPath, TrainerId = trainerId };
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
    /// <param name="tagTxt">The tag to save.</param>
    /// <param name="trainerId">The ID of the trainer who created with the tag.</param>
    /// <returns>The number of rows affected.</returns>
    public virtual async Task<int> SaveTagAsync(string tagTxt, uint trainerId)
    {
        await InitAsync();
        Tags tag = new() { Name = tagTxt, TrainerId = trainerId };
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
    /// <returns>The number of rows affected, or 0 if the operation failed.</returns>
    public virtual async Task<int> SaveMatchEntryAsync(MatchEntry matchEntry, List<Game> games)
    {
        await InitAsync();
        await _semaphore.WaitAsync();
        try
        {
            int affected = 0;
            await _database.RunInTransactionAsync(async tran =>
            {
                if (matchEntry.Id != 0)
                {
                    _logger.LogInformation("Updating match entry: {MatchEntryId}", matchEntry.Id);
                    affected += tran.Update(matchEntry);
                }
                else
                {
                    _logger.LogInformation
                    (
                        "Inserting match entry: {@MatchEntry}",
                        matchEntry
                    );
                    _logger.LogInformation
                    (
                        "Playing: {PlayingName} \nAgainst: {AgainstName}",
                        matchEntry.Playing?.Name,
                        matchEntry.Against?.Name
                    );
                    affected += tran.Insert(matchEntry);
                }

                // Update game relationships with the match entry
                foreach (var game in games)
                {
                    _logger.LogInformation("Creating game: {@Game}", game);
                    game.MatchEntryId = matchEntry.Id;
                    affected += await SaveGameAsync(game);
                }

                // Update match entry relationships with games
                if (games.Count > 0)
                {
                    matchEntry.Game1Id = games[0].Id;
                    _logger.LogInformation("Update Match Game Ids: \nGame1Id: {Game1Id}", matchEntry.Game1Id);
                    if (games.Count > 1)
                    {
                        _logger.LogInformation("{Game2Id}", matchEntry.Game2Id);
                        matchEntry.Game2Id = games[1].Id;
                    }
                    if (games.Count > 2)
                    {
                        _logger.LogInformation("{Game3Id}", matchEntry.Game3Id);
                        matchEntry.Game3Id = games[2].Id;
                    }
                    affected += tran.Update(matchEntry);
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
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Retrieves a match entry with all its related data (games, archetypes, etc.) by ID.
    /// </summary>
    /// <param name="id">The ID of the match entry.</param>
    /// <returns>The match entry with all related data, or null if not found.</returns>
    public virtual async Task<MatchEntry?> GetMatchEntryByIdAsync(uint id)
    {
        await InitAsync();
        await _semaphore.WaitAsync();
        try
        {
            var matchEntry = await _database.GetWithChildrenAsync<MatchEntry>(id, true);
            if (matchEntry != null)
            {


                // Load related games
                if (matchEntry.Game1Id.HasValue)
                    matchEntry.Game1 = await GetGameByIdAsync(matchEntry.Game1Id.Value);
                if (matchEntry.Game2Id != 0)
                    matchEntry.Game2 = await GetGameByIdAsync(matchEntry.Game2Id);
                if (matchEntry.Game3Id != 0)
                    matchEntry.Game3 = await GetGameByIdAsync(matchEntry.Game3Id);
            }
            return matchEntry;
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
            var entries = await _database.GetAllWithChildrenAsync<MatchEntry>(recursive: true);
            entries = entries.Where(e => e.TrainerId == trainerId).ToList();

            foreach (var entry in entries)
            {

                if (entry.Game1Id.HasValue)
                    entry.Game1 = await _database.GetWithChildrenAsync<Game>(entry.Game1Id.Value);
                if (entry.Game2Id != 0)
                    entry.Game2 = await _database.GetWithChildrenAsync<Game>(entry.Game2Id);
                if (entry.Game3Id != 0)
                    entry.Game3 = await _database.GetWithChildrenAsync<Game>(entry.Game3Id);
            }

            return entries;
        }
        catch (Exception ex)
        {
            ModalErrorHandler errorHandler = new();
            errorHandler.HandleError(ex);
            return new List<MatchEntry>();
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

}

