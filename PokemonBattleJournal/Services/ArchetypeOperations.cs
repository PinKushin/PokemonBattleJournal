using System.Text.RegularExpressions;
using PokemonBattleJournal.Scraper.Interfaces;
using PokemonBattleJournal.Scraper.Models;

namespace PokemonBattleJournal.Services
{
    public class ArchetypeOperations : IArchetypeOperations
    {
        private readonly SqliteConnectionFactory _factory;
        private readonly ILogger _logger;
        private readonly ILimitlessMetaService _metaService;

        internal ArchetypeOperations(SqliteConnectionFactory factory, ILogger logger, ILimitlessMetaService metaService)
        {
            _factory = factory;
            _logger = logger;
            _metaService = metaService;
        }

        /// <summary>
        /// Gets all archetypes from the database, initializing defaults if none exist.
        /// </summary>
        public async Task<List<Archetype>> GetAllAsync()
        {
            // Fetch from network BEFORE acquiring the DB lock — Limitless HTTP can take seconds.
            // Wrap in its own try/catch: a network failure must not bubble to the outer catch,
            // which calls ModalErrorHandler — showing a ContentDialog during AppearingAsync
            // before the page's XamlRoot is composed crashes WinUI (0xc000027b).
            List<MetaDeck> metaDecks = [];
            try { metaDecks = await _metaService.GetTopDecksAsync(10); }
            catch (Exception ex) { _logger.LogWarning(ex, "Limitless fetch failed — using offline fallback"); }

            SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
            try
            {
                await _factory.GetLock().WaitAsync();
                // Always try to upsert current meta decks so new archetypes appear each launch
                if (metaDecks.Count > 0)
                {
#pragma warning disable S3267 // Loop body contains awaits; cannot be replaced with LINQ Select
                    foreach (MetaDeck deck in metaDecks)
                    {
                        string imagePath = TryResolveLocalSprite(deck.Name, deck.ImageUrl);
                        string? imagePath2 = deck.SecondaryImageUrl != null
                            ? TryResolveLocalSprite(deck.Name, deck.SecondaryImageUrl)
                            : null;
                        await db.ExecuteAsync(
                            "INSERT OR IGNORE INTO Archetype (Name, ImagePath, ImagePath2) VALUES (?, ?, ?)",
                            deck.Name, imagePath, imagePath2);
                        // Fix existing rows with CDN ImagePath
                        await db.ExecuteAsync(
                            "UPDATE Archetype SET ImagePath = ? WHERE Name = ? AND ImagePath LIKE 'http%'",
                            imagePath, deck.Name);
                        // Backfill ImagePath2 for rows that don't have it yet
                        if (imagePath2 != null)
                            await db.ExecuteAsync(
                                "UPDATE Archetype SET ImagePath2 = ? WHERE Name = ? AND ImagePath2 IS NULL",
                                imagePath2, deck.Name);
                    }
#pragma warning restore S3267
                }
                else if (await db.Table<Archetype>().CountAsync() == 0)
                {
                    // Offline and no existing data — seed hardcoded defaults
                    _ = await db.InsertAllAsync(new List<Archetype>
                    {
                        new() { Name = "Regidrago", ImagePath = "regidrago.png" },
                        new() { Name = "Charizard", ImagePath = "charizard.png" },
                        new() { Name = "Klawf", ImagePath = "klawf.png" },
                        new() { Name = "Snorlax Stall", ImagePath = "snorlax.png" },
                        new() { Name = "Raging Bolt", ImagePath = "raging_bolt.png" },
                        new() { Name = "Gardevoir", ImagePath = "gardevoir.png" },
                        new() { Name = "Miraidon", ImagePath = "miraidon.png" },
                        new() { Name = "Other", ImagePath = "substitute.png" }
                    });
                }
                // Always ensure "Other" exists as a catch-all
                await db.ExecuteAsync(
                    "INSERT OR IGNORE INTO Archetype (Name, ImagePath) VALUES (?, ?)",
                    "Other", "substitute.png");
                return await db.Table<Archetype>().ToListAsync();
            }
            catch (Exception ex)
            {
                // Log only — no dialog. ModalErrorHandler from AppearingAsync crashes WinUI (0xc000027b).
                _logger.LogError(ex, "Error getting archetypes");
                return [];
            }
            finally
            {
                _ = _factory.GetLock().Release();
            }
        }

        /// <summary>
        /// Gets an archetype by its ID.
        /// </summary>
        public async Task<Archetype?> GetByIdAsync(uint id)
        {
            SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
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
                _ = _factory.GetLock().Release();
            }
        }

        /// <summary>
        /// Saves an archetype to the database.
        /// </summary>
        public async Task<int> SaveAsync(string name, string imgPath, uint trainerId, string? imgPath2 = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Archetype name is required", nameof(name));
            }

            if (string.IsNullOrWhiteSpace(imgPath))
            {
                throw new ArgumentException("Archetype image path is required", nameof(imgPath));
            }

            if (trainerId == 0)
            {
                throw new ArgumentException("Trainer ID is required", nameof(trainerId));
            }

            _logger.LogDebug("SaveAsync: saving archetype {Name} for trainer {TrainerId}", name, trainerId);
            SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
            Archetype archetype = new()
            {
                Name = name,
                ImagePath = imgPath,
                ImagePath2 = imgPath2,
                TrainerId = trainerId
            };

            try
            {
                await _factory.GetLock().WaitAsync();
                int affected = 0;
                await db.RunInTransactionAsync(tran =>
                {
                    affected = tran.Insert(archetype);
                });
                return affected;
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Invalid data when saving archetype: {Message}", ex.Message);
                ModalErrorHandler modalErrorHandler = new();
                modalErrorHandler.HandleError(ex);
                return 0;
            }
            catch (SQLiteException ex)
            {
                _logger.LogError(ex, "Database error when saving archetype: {Message}", ex.Message);
                ModalErrorHandler modalErrorHandler = new();
                modalErrorHandler.HandleError(ex);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving archetype: {Name} - {Message}", name, ex.Message);
                ModalErrorHandler modalErrorHandler = new();
                modalErrorHandler.HandleError(ex);
                return 0;
            }
            finally
            {
                _ = _factory.GetLock().Release();
            }
        }

        private static string TryResolveLocalSprite(string deckName, string imageUrl = "")
        {
            if (!string.IsNullOrWhiteSpace(imageUrl) &&
                Uri.TryCreate(imageUrl, UriKind.Absolute, out Uri? uri))
            {
                string file = Path.GetFileName(uri.AbsolutePath);
                if (!string.IsNullOrEmpty(file))
                    return file.Replace('-', '_');
            }

            // Name-based fallback: take first Pokemon in multi-Pokemon names
            string name = deckName.Split(['&', '/'], StringSplitOptions.None)[0].Trim();
            name = Regex.Replace(name, @"\s+(ex|EX|GX|V|VMAX|VSTAR|VUNION|tera|Tera)$", "", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1)).Trim();
            return Regex.Replace(name.ToLowerInvariant(), @"\s+", "_", RegexOptions.None, TimeSpan.FromSeconds(1)) + ".png";
        }

        /// <summary>
        /// Deletes an archetype from the database.
        /// </summary>
        public async Task<int> DeleteAsync(Archetype archetype)
        {
            if (archetype == null)
            {
                throw new ArgumentNullException(nameof(archetype), "Archetype cannot be null");
            }

            if (archetype.Id == 0)
            {
                throw new ArgumentException("Archetype ID is required", nameof(archetype));
            }

            _logger.LogDebug("DeleteAsync: deleting archetype {Name} ({Id})", archetype.Name, archetype.Id);
            SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
            try
            {
                await _factory.GetLock().WaitAsync();

                int affected = 0;
                await db.RunInTransactionAsync(tran =>
                {
                    // Check if this archetype is used in any matches (must be inside transaction)
                    int matchCount = tran.ExecuteScalar<int>(
                        "SELECT COUNT(*) FROM MatchEntry WHERE PlayingId = ? OR AgainstId = ?",
                        archetype.Id, archetype.Id);

                    if (matchCount > 0)
                    {
                        _logger.LogWarning("Archetype {ArchetypeId} ({ArchetypeName}) is used in {Count} matches",
                            archetype.Id, archetype.Name, matchCount);
                        throw new InvalidOperationException(
                            $"Cannot delete archetype '{archetype.Name}' because it is used in {matchCount} matches");
                    }

                    affected = tran.Delete(archetype);

                    // Verify deletion
                    int remainingCount = tran.ExecuteScalar<int>(
                        "SELECT COUNT(*) FROM Archetype WHERE Id = ?", archetype.Id);
                    if (remainingCount > 0)
                    {
                        _logger.LogError("Archetype {ArchetypeId} was not deleted properly", archetype.Id);
                        throw new InvalidOperationException("Failed to delete archetype");
                    }
                });

                _logger.LogInformation("Successfully deleted archetype {ArchetypeId} ({ArchetypeName})",
                    archetype.Id, archetype.Name);
                return affected;
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Invalid data when deleting archetype: {Message}", ex.Message);
                ModalErrorHandler modalErrorHandler = new();
                modalErrorHandler.HandleError(ex);
                return 0;
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Cannot delete archetype: {Message}", ex.Message);
                ModalErrorHandler modalErrorHandler = new();
                modalErrorHandler.HandleError(ex);
                return 0;
            }
            catch (SQLiteException ex)
            {
                _logger.LogError(ex, "Database error when deleting archetype: {Message}", ex.Message);
                ModalErrorHandler modalErrorHandler = new();
                modalErrorHandler.HandleError(ex);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting archetype: {Name} - {Message}", archetype.Name, ex.Message);
                ModalErrorHandler modalErrorHandler = new();
                modalErrorHandler.HandleError(ex);
                return 0;
            }
            finally
            {
                _ = _factory.GetLock().Release();
            }
        }
    }
}
