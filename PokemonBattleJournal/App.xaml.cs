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

                // Query "Other" archetype directly — bypasses the HTTP meta-deck call in GetAllAsync
                SQLiteAsyncConnection db = await factory.GetDatabaseAsync();
                SemaphoreSlim sem = factory.GetLock();
                Archetype? other;
                await sem.WaitAsync();
                try
                {
                    other = await db.Table<Archetype>().Where(a => a.Name == "Other").FirstOrDefaultAsync();
                    if (other == null)
                    {
                        other = new Archetype { Name = "Other", ImagePath = "ball_icon.png" };
                        await db.InsertAsync(other);
                    }
                }
                finally { sem.Release(); }

                for (int i = 0; i < 3; i++)
                {
                    MatchEntry match = new()
                    {
                        TrainerId = trainer.Id,
                        PlayingId = other.Id,
                        AgainstId = other.Id,
                        Result = MatchResult.Win,
                        DatePlayed = DateTime.UtcNow,
                        StartTime = DateTime.UtcNow,
                        EndTime = DateTime.UtcNow.AddMinutes(20)
                    };
                    Game game = new() { Result = MatchResult.Win, Turn = 1, Notes = $"DebugSeed-{i + 1}" };
                    await factory.Matches.SaveAsync(match, [game]);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Debug seed failed");
            }
        }
#endif
    }
}