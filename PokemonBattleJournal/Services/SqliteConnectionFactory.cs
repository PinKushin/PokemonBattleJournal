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

        try
        {
            await _semaphore.WaitAsync();
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
        try
        {
            await _semaphore.WaitAsync();
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
        try
        {
            await _semaphore.WaitAsync();
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
        Trainer trainer = new() { Name = trainerName };
        try
        {
            int affected = 0;
            await _semaphore.WaitAsync();
            await _database.RunInTransactionAsync(tran =>
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
        try
        {
            int affected = 0;
            await _semaphore.WaitAsync();
            await _database.RunInTransactionAsync(async tran =>
            {
                // Load all matches for this trainer
                var matches = await _database.Table<MatchEntry>()
                    .Where(m => m.TrainerId == trainer.Id)
                    .ToListAsync();

                // Delete each match and its related records
                foreach (var match in matches)
                {
                    affected += await DeleteMatchEntryAsync(match);
                }

                // Delete archetypes
                var archetypes = await _database.Table<Archetype>()
                    .Where(a => a.TrainerId == trainer.Id)
                    .ToListAsync();
                foreach (var archetype in archetypes)
                {
                    affected += tran.Delete(archetype);
                }

                // Delete trainer tags
                var tags = await _database.Table<Tags>()
                    .Where(t => t.TrainerId == trainer.Id)
                    .ToListAsync();
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
            _logger.LogError(ex, "Error deleting trainer and related records");
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
    /// Saves an archetype to the database. If the archetype has an ID, it updates the existing record; otherwise, it inserts a new record.
    /// </summary>
    /// <param name="name">The name of the archetype to save.</param>
    /// <param name="imgPath">The image path of the archetype to save.</param>
    /// <param name="trainerId">The ID of the trainer who created the archetype.</param>
    /// <returns>The number of rows affected.</returns>
    public virtual async Task<int> SaveArchetypeAsync(string name, string imgPath, uint trainerId)
    {
        await InitAsync();
        Archetype archetype = new() { Name = name, ImagePath = imgPath, TrainerId = trainerId };
        try
        {
            await _semaphore.WaitAsync();
            int affected = 0;
            await _database.RunInTransactionAsync(tran =>
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
        try
        {
            await _semaphore.WaitAsync();
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
        try
        {
            await _semaphore.WaitAsync();
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
        try
        {
            await _semaphore.WaitAsync();
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
        try
        {
            int affected = 0;
            await _semaphore.WaitAsync();
            await _database.RunInTransactionAsync(tran =>
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
        try
        {
            await _semaphore.WaitAsync();
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
        try
        {
            await _semaphore.WaitAsync();
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
        try
        {
            int del = 0;
            await _semaphore.WaitAsync();
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
        try
        {
            int affected = 0;
            await _semaphore.WaitAsync();
            await _database.RunInTransactionAsync(tran =>
            {
                // Save the game first
                if (game.Id != 0)
                {
                    affected = tran.Update(game);
                }
                else
                {
                    affected = tran.Insert(game);
                }

                // Now handle the tags
                if (game.Tags != null)
                {
                    foreach (var tag in game.Tags)
                    {
                        tag.GameId = game.Id;
                        if (tag.Id != 0)
                        {
                            affected += tran.Update(tag);
                        }
                        else
                        {
                            affected += tran.Insert(tag);
                        }
                    }
                }
            });
            return affected;
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
        try
        {
            await _semaphore.WaitAsync();
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
        try
        {
            await _semaphore.WaitAsync();
            var game = await _database.GetWithChildrenAsync<Game>(id, true);
            if (game != null)
            {
                // Explicitly load tags
                game.Tags = await _database.Table<Tags>()
                    .Where(t => t.GameId == game.Id)
                    .ToListAsync();
            }
            return game;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading game and tags");
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
        try
        {
            int affected = 0;
            await _semaphore.WaitAsync();
            await _database.RunInTransactionAsync(async tran =>
            {
                // Delete associated tags first
                if (game.Tags != null)
                {
                    foreach (var tag in game.Tags)
                    {
                        affected += tran.Delete(tag);
                    }
                }
                // Then delete the game
                affected += tran.Delete(game);
            });
            return affected;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting game and tags");
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
        try
        {
            int affected = 0;
            await _semaphore.WaitAsync();
            await _database.RunInTransactionAsync(async tran =>
            {
                if (matchEntry.Id != 0)
                {
                    _logger.LogInformation("Updating match entry: {MatchEntryId}", matchEntry.Id);
                    affected += tran.Update(matchEntry);
                }
                else
                {
                    _logger.LogInformation("Inserting match entry: {@MatchEntry}", matchEntry);
                    _logger.LogInformation("Playing: {PlayingName} \nAgainst: {AgainstName}", matchEntry.Playing?.Name, matchEntry.Against?.Name);
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
            var entries = await _database.GetAllWithChildrenAsync<MatchEntry>(e => e.TrainerId == trainerId, true);

            foreach (var entry in entries)
            {
                // Load related archetypes
                if (entry.PlayingId is not 0)
                {
                    entry.Playing = await _database.GetAsync<Archetype>(entry.PlayingId);
                }
                if (entry.AgainstId is not 0)
                {
                    entry.Against = await _database.GetAsync<Archetype>(entry.AgainstId);
                }

                // Load related games and their tags
                if (entry.Game1Id.HasValue)
                {
                    entry.Game1 = await _database.GetWithChildrenAsync<Game>(entry.Game1Id.Value);
                    if (entry.Game1 != null)
                    {
                        entry.Game1.Tags = await _database.Table<Tags>()
                            .Where(t => t.GameId == entry.Game1.Id)
                            .ToListAsync();
                        _logger.LogDebug("Game1 Tags loaded: {@Tags}", entry.Game1.Tags);
                    }
                }
                if (entry.Game2Id != 0)
                {
                    entry.Game2 = await _database.GetWithChildrenAsync<Game>(entry.Game2Id);
                    if (entry.Game2 != null)
                    {
                        entry.Game2.Tags = await _database.Table<Tags>()
                            .Where(t => t.GameId == entry.Game2.Id)
                            .ToListAsync();
                        _logger.LogDebug("Game2 Tags loaded: {@Tags}", entry.Game2.Tags);
                    }
                }
                if (entry.Game3Id != 0)
                {
                    entry.Game3 = await _database.GetWithChildrenAsync<Game>(entry.Game3Id);
                    if (entry.Game3 != null)
                    {
                        entry.Game3.Tags = await _database.Table<Tags>()
                            .Where(t => t.GameId == entry.Game3.Id)
                            .ToListAsync();
                        _logger.LogDebug("Game3 Tags loaded: {@Tags}", entry.Game3.Tags);
                    }
                }
            }
            _logger.LogInformation("Loaded {Count} match entries with games and tags", entries.Count);
            return entries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting match entries for trainer {TrainerId}", trainerId);
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
        try
        {
            int affected = 0;
            await _semaphore.WaitAsync();
            await _database.RunInTransactionAsync(async tran =>
            {
                // Delete associated games and their tags
                if (matchEntry.Game1 != null)
                    affected += await DeleteGameAsync(matchEntry.Game1);
                if (matchEntry.Game2 != null)
                    affected += await DeleteGameAsync(matchEntry.Game2);
                if (matchEntry.Game3 != null)
                    affected += await DeleteGameAsync(matchEntry.Game3);

                // Then delete the match entry
                affected += tran.Delete(matchEntry);
            });
            return affected;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting match entry and related records");
            ModalErrorHandler errorHandler = new();
            errorHandler.HandleError(ex);
            return 0;
        }
        finally
        {
            _semaphore.Release();
        }
    }

}

