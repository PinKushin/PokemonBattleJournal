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

                // Use the real seeding path — same as production. GetAllAsync seeds offline
                // defaults when the DB is empty, so this also tests that path works correctly.
                List<Archetype> allArchetypes = await factory.Archetypes.GetAllAsync();
                Archetype? other     = allArchetypes.FirstOrDefault(a => a.Name == "Other");
                Archetype? charizard = allArchetypes.FirstOrDefault(a => a.Name == "Charizard");
                Archetype? regidrago = allArchetypes.FirstOrDefault(a => a.Name == "Regidrago");
                Archetype? miraidon  = allArchetypes.FirstOrDefault(a => a.Name == "Miraidon");
                if (other == null || charizard == null || regidrago == null || miraidon == null)
                {
                    logger.LogError("Debug seed: required archetypes missing after GetAllAsync — aborting match seed");
                    return;
                }

                // Seed global tags (GetAllAsync seeds them if absent) then look up a few for use below
                List<Tags> allTags = await factory.Tags.GetAllAsync();
                Tags? lucky     = allTags.FirstOrDefault(t => t.Name == "Lucky");
                Tags? earlyStart = allTags.FirstOrDefault(t => t.Name == "Early Start");
                Tags? unlucky   = allTags.FirstOrDefault(t => t.Name == "Unlucky");

                // BO1 matches — mixed results, varied archetypes and dates
                DateTime baseDate = DateTime.UtcNow.AddDays(-7);

                (Archetype playing, Archetype against, MatchResult result, uint turn, DateTime date, Tags? tag)[] bo1 =
                [
                    (other,     charizard, MatchResult.Win,  1u, baseDate,             null),
                    (charizard, regidrago, MatchResult.Win,  2u, baseDate.AddDays(1),  null),
                    (regidrago, miraidon,  MatchResult.Win,  1u, baseDate.AddDays(2),  lucky),
                    (other,     regidrago, MatchResult.Loss, 2u, baseDate.AddDays(3),  unlucky),
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
                        new Game { Result = MatchResult.Win,  Turn = 1, Notes = "BO3-G1", Tags = lucky != null ? [lucky] : null },
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
                        new Game { Result = MatchResult.Loss, Turn = 2, Notes = "BO3-G3", Tags = earlyStart != null ? [earlyStart] : null }
                    ]);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Debug seed failed");
            }
        }



#endif
    }
}