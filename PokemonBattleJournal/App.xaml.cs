namespace PokemonBattleJournal
{

    public partial class App : Application
    {
        private readonly ILogger<App> _logger;
        private readonly AppShell _shell;

        public App(ILogger<App> logger, AppShell shell, ISqliteConnectionFactory factory)
        {
            _logger = logger;
            _shell = shell;
            InitializeComponent();
#if DEBUG
            Task.Run(() => SeedDebugDataAsync(factory, _logger)).GetAwaiter().GetResult();
#endif
        }

        protected override Window CreateWindow(IActivationState? activationState) => new(_shell);

#if DEBUG
        private static async Task SeedDebugDataAsync(ISqliteConnectionFactory factory, ILogger logger)
        {
            try
            {
                var trainers = await factory.Trainers.GetAllAsync();
                var existing = trainers.FirstOrDefault(t => t.Name == "UITestTrainer");
                if (existing != null)
                {
                    // Activate if a previous crash left it inactive before AppShellViewModel could
                    if (!existing.IsActive)
                        await factory.Trainers.SetActiveAsync(existing);
                    return;
                }

                await factory.Trainers.SaveAsync("UITestTrainer");
                Trainer? trainer = await factory.Trainers.GetByNameAsync("UITestTrainer");
                if (trainer == null) return;

                // Mark as active so GetActiveAsync() returns it — prevents the first-boot
                // DisplayPromptAsync from firing (which crashes on WinUI before XamlRoot is set)
                await factory.Trainers.SetActiveAsync(trainer);

                // Seed archetypes directly — bypasses the HTTP meta-deck call in GetAllAsync
                SQLiteAsyncConnection db = await factory.GetDatabaseAsync();
                SemaphoreSlim sem = factory.GetLock();

                Archetype other, charizard, regidrago, miraidon;
                await sem.WaitAsync();
                try
                {
                    other = await SeedArchetypeAsync(db, "Other", "ball_icon.png");
                    charizard = await SeedArchetypeAsync(db, "Charizard", "charizard.png");
                    regidrago = await SeedArchetypeAsync(db, "Regidrago", "regidrago.png");
                    miraidon = await SeedArchetypeAsync(db, "Miraidon", "miraidon.png");
                }
                finally { sem.Release(); }

                // Seed tags for the active trainer
                Tags lucky, earlyStart;
                await sem.WaitAsync();
                try
                {
                    lucky = await SeedTagAsync(db, "Lucky", trainer.Id);
                    earlyStart = await SeedTagAsync(db, "Early Start", trainer.Id);
                }
                finally { sem.Release(); }

                // BO1 matches — mixed results, varied archetypes and dates
                DateTime baseDate = DateTime.UtcNow.AddDays(-7);

                (Archetype playing, Archetype against, MatchResult result, uint turn, DateTime date, Tags? tag)[] bo1 =
                [
                    (other,     charizard, MatchResult.Win,  1u, baseDate,             null),
                    (charizard, regidrago, MatchResult.Win,  2u, baseDate.AddDays(1),  null),
                    (regidrago, miraidon,  MatchResult.Win,  1u, baseDate.AddDays(2),  null),
                    (other,     regidrago, MatchResult.Loss, 2u, baseDate.AddDays(3),  lucky),
                    (charizard, miraidon,  MatchResult.Loss, 1u, baseDate.AddDays(4),  null),
                    (miraidon,  other,     MatchResult.Tie,  2u, baseDate.AddDays(5),  earlyStart),
                ];

                for (int i = 0; i < bo1.Length; i++)
                {
                    var (p, a, result, turn, date, tag) = bo1[i];
                    MatchEntry match = new()
                    {
                        TrainerId = trainer.Id,
                        PlayingId = p.Id,
                        AgainstId = a.Id,
                        Result = result,
                        DatePlayed = date,
                        StartTime = date,
                        EndTime = date.AddMinutes(15 + i * 3)
                    };
                    Game game = new()
                    {
                        Result = result,
                        Turn = turn,
                        Notes = $"DebugSeed-BO1-{i + 1}",
                        Tags = tag != null ? [tag] : null
                    };
                    await factory.Matches.SaveAsync(match, [game]);
                }

                // BO3 Win: W+L+W
                await factory.Matches.SaveAsync(
                    new MatchEntry
                    {
                        TrainerId = trainer.Id,
                        PlayingId = other.Id,
                        AgainstId = charizard.Id,
                        Result = MatchResult.Win,
                        DatePlayed = baseDate.AddDays(6),
                        StartTime = baseDate.AddDays(6),
                        EndTime = baseDate.AddDays(6).AddMinutes(45)
                    },
                    [
                        new Game { Result = MatchResult.Win,  Turn = 1, Notes = "BO3-G1", Tags = [lucky] },
                        new Game { Result = MatchResult.Loss, Turn = 2, Notes = "BO3-G2" },
                        new Game { Result = MatchResult.Win,  Turn = 1, Notes = "BO3-G3" }
                    ]);

                // BO3 Loss: L+W+L
                await factory.Matches.SaveAsync(
                    new MatchEntry
                    {
                        TrainerId = trainer.Id,
                        PlayingId = regidrago.Id,
                        AgainstId = miraidon.Id,
                        Result = MatchResult.Loss,
                        DatePlayed = baseDate.AddDays(7),
                        StartTime = baseDate.AddDays(7),
                        EndTime = baseDate.AddDays(7).AddMinutes(50)
                    },
                    [
                        new Game { Result = MatchResult.Loss, Turn = 2, Notes = "BO3-G1" },
                        new Game { Result = MatchResult.Win,  Turn = 1, Notes = "BO3-G2" },
                        new Game { Result = MatchResult.Loss, Turn = 2, Notes = "BO3-G3", Tags = [earlyStart] }
                    ]);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Debug seed failed");
            }
        }

        private static async Task<Archetype> SeedArchetypeAsync(SQLiteAsyncConnection db, string name, string imagePath)
        {
            Archetype? existing = await db.Table<Archetype>().Where(a => a.Name == name).FirstOrDefaultAsync();
            if (existing != null) return existing;
            Archetype arch = new() { Name = name, ImagePath = imagePath };
            await db.InsertAsync(arch);
            return arch;
        }

        private static async Task<Tags> SeedTagAsync(SQLiteAsyncConnection db, string name, uint trainerId)
        {
            Tags? existing = await db.Table<Tags>().Where(t => t.Name == name).FirstOrDefaultAsync();
            if (existing != null) return existing;
            Tags tag = new() { Name = name, TrainerId = trainerId };
            await db.InsertAsync(tag);
            return tag;
        }
#endif
    }
}