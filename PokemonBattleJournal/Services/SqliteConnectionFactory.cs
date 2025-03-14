using SQLite;

namespace PokemonBattleJournal.Services
{
    public class SqliteConnectionFactory
    {
        SQLiteAsyncConnection _database;
        async Task InitAsync()
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

        public virtual async Task<List<Trainer>> GetTrainersAsync()
        {
            await InitAsync();
            return await _database.Table<Trainer>().ToListAsync();
        }

        public virtual async Task<Trainer> GetTrainerByNameAsync(string name)
        {
            await InitAsync();
            return await _database.Table<Trainer>().Where(i => i.Name == name).FirstOrDefaultAsync();
        }

        public virtual async Task<int> SaveTrainerAsync(Trainer trainer)
        {
            await InitAsync();
            if (trainer.Id != 0)
            {
                return await _database.UpdateAsync(trainer);
            }
            else
            {
                return await _database.InsertAsync(trainer);
            }
        }

        public virtual async Task<int> DeleteTrainerAsync(Trainer trainer)
        {
            await InitAsync();
            return await _database.DeleteAsync(trainer);
        }

        public virtual async Task SaveMatchEntryAsync(MatchEntry matchEntry)
        {
            await InitAsync();
            if (matchEntry.Id != 0)
            {
                await _database.UpdateAsync(matchEntry);
            }
            else
            {
                await _database.InsertAsync(matchEntry);
            }
        }

        public virtual async Task<List<MatchEntry>> GetMatchEntriesAsync()
        {
            await InitAsync();
            return await _database.Table<MatchEntry>().ToListAsync();
        }

        public virtual async Task<List<MatchEntry>> GetMatchEntriesByTrainerIdAsync(uint trainerId)
        {
            await InitAsync();
            return await _database.Table<MatchEntry>().Where(i => i.TrainerId == trainerId).ToListAsync();
        }

        public virtual async Task<MatchEntry> GetMatchEntryByIdAsync(uint id)
        {
            await InitAsync();
            return await _database.Table<MatchEntry>().Where(i => i.Id == id).FirstOrDefaultAsync();
        }

        public virtual async Task<int> DeleteMatchEntryAsync(MatchEntry matchEntry)
        {
            await InitAsync();
            return await _database.DeleteAsync(matchEntry);
        }

        public virtual async Task SaveArchetypeAsync(Archetype archetype)
        {
            await InitAsync();
            if (archetype.Id != 0)
            {
                await _database.UpdateAsync(archetype);
            }
            else
            {
                await _database.InsertAsync(archetype);
            }
        }

        public virtual async Task<List<Archetype>> GetArchetypesAsync()
        {
            await InitAsync();
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

        public virtual async Task<Archetype> GetArchetypeByIdAsync(uint id)
        {
            await InitAsync();
            return await _database.Table<Archetype>().Where(i => i.Id == id).FirstOrDefaultAsync();
        }

        public virtual async Task<int> DeleteArchetypeAsync(Archetype archetype)
        {
            await InitAsync();
            return await _database.DeleteAsync(archetype);
        }

        public virtual async Task SaveTagAsync(Tags tag)
        {
            await InitAsync();
            if (tag.Id != 0)
            {
                await _database.UpdateAsync(tag);
            }
            else
            {
                await _database.InsertAsync(tag);
            }
        }

        public virtual async Task<List<Tags>> GetTagsAsync()
        {
            await InitAsync();
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

        public virtual async Task<Tags> GetTagByIdAsync(uint id)
        {
            await InitAsync();
            return await _database.Table<Tags>().Where(i => i.Id == id).FirstOrDefaultAsync();
        }

        public virtual async Task<int> DeleteTagAsync(Tags tag)
        {
            await InitAsync();
            return await _database.DeleteAsync(tag);
        }

        public virtual async Task SaveGameAsync(Game game)
        {
            await InitAsync();
            if (game.Id != 0)
            {
                await _database.UpdateAsync(game);
            }
            else
            {
                await _database.InsertAsync(game);
            }
        }

        public virtual async Task<List<Game>> GetGamesAsync()
        {
            await InitAsync();
            return await _database.Table<Game>().ToListAsync();
        }

        public virtual async Task<Game> GetGameByIdAsync(uint id)
        {
            await InitAsync();
            return await _database.Table<Game>().Where(i => i.Id == id).FirstOrDefaultAsync();
        }

        public virtual async Task<int> DeleteGameAsync(Game game)
        {
            await InitAsync();
            return await _database.DeleteAsync(game);
        }
    }
}
