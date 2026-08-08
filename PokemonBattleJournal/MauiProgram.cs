using CommunityToolkit.Maui;
using LiveChartsCore.SkiaSharpView.Maui;
using PokemonBattleJournal.Interfaces;
using PokemonBattleJournal.Logging;
using PokemonBattleJournal.Scraper.Interfaces;
using PokemonBattleJournal.Scraper.Services;
using PokemonBattleJournal.Services.Export;
using PokemonBattleJournal.Services.Import;
using PokemonBattleJournal.Services.Restore;
using Serilog;
using SkiaSharp.Views.Maui.Controls.Hosting;
using System.Runtime.Versioning;

namespace PokemonBattleJournal
{
    public static class MauiProgram
    {
        [SupportedOSPlatform("android21.0")]
        [SupportedOSPlatform("ios15.0")]
        [SupportedOSPlatform("maccatalyst15.0")]
        [SupportedOSPlatform("windows10.0.17763.0")]
        public static MauiApp CreateMauiApp()
        {
            MauiAppBuilder builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseSkiaSharp()
                .UseMauiCommunityToolkit()
                .UseLiveCharts()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");

                    fonts.AddFont("Saira-Regular.ttf", "SairaRegular");
                    fonts.AddFont("Saira-Bold.ttf", "SairaBold");
                    fonts.AddFont("Saira-Black.ttf", "SairaBlack");

                    fonts.AddFont("Charm-Regular.ttf", "CharmRegular");
                    fonts.AddFont("Charm-Bold.ttf", "CharmBold");

                    fonts.AddFont("Doto_Rounded-Black.ttf", "DotoRoundedBlack");

                    fonts.AddFont("PokemonSolid.ttf", "PokemonSolid");
                    fonts.AddFont("PokemonGb-RAeo.ttf", "PokemonGB");
                    fonts.AddFont("pokemon_tcg_symbols_font_by_icycatelf_dah5i8h.ttf", "PokemonSymbols");

                    fonts.AddFont("FluentSystemIcons-Regular.ttf", FluentUI.FontFamily);
                    fonts.AddFont("Segoe-UI.ttf", "Segoe UI");
                })
                .UseSentry(options =>
                {
                    options.Dsn = "https://03ff3f5afbfdbbd038a8903bc0e6d5e4@o4508805406851072.ingest.us.sentry.io/4508805409538048";

                    // --- Privacy guards, set EXPLICITLY even though these are the SDK defaults.
                    //
                    // Both are what a generic "instrument Sentry properly" walkthrough tells you
                    // to turn on, and both would defeat everything SentryRedactingSink does. A
                    // screenshot of MainPage shows trainer names, deck names and match notes; a
                    // view hierarchy carries the text of every label. For a SaaS app that advice
                    // is sound because the vendor already holds the data — here the device is the
                    // only copy, so an attachment is the FIRST copy to leave.
                    //
                    // Written down rather than left to the default so that turning one on is a
                    // deliberate act against a stated reason, not a one-line default flip.
                    options.AttachScreenshot = false;
                    options.SendDefaultPii = false;

                    // Release health: crash-free session rate, grouped per release. Free, and it
                    // carries no user content — a session is a start time, a duration and an
                    // outcome. Needs an explicit Release or every build looks like the same one.
                    options.AutoSessionTracking = true;
                    options.Release = $"PokemonBattleJournal@{AppInfo.Current.VersionString}+{AppInfo.Current.BuildString}";

#if DEBUG
                    options.Debug = true;
                    options.TracesSampleRate = 1.0F;
                    options.MaxBreadcrumbs = 1000;
                    // Debug builds (dev machines, CI emulators) tag as development so
                    // Sentry alert rules can be scoped to production-only — keeps CI/test
                    // error events out of email while still recording them.
                    options.Environment = "development";
#else
                    options.Debug = false;
                    options.TracesSampleRate = 0.1;
                    options.MaxBreadcrumbs = 300;
                    options.Environment = "production";
#endif
                });
            string logsDir = Path.Combine(FileHelper.GetAppDataPath(), "Logs");
            Directory.CreateDirectory(logsDir);
            Serilog.ILogger serilogLogger = new LoggerConfiguration()
                .WriteTo.Debug()
                .WriteTo.File(Path.Combine(logsDir, "log.txt"),
                rollingInterval: RollingInterval.Day)
                // Sentry sink, behind the redactor. The two sinks above stay on the device and
                // keep every value; this one leaves it, so it only carries property values whose
                // type cannot express user content. Levels and reasoning live in
                // SentryRedactingSinkExtensions.RedactedSentry — the only place they are set.
                .WriteTo.RedactedSentry()
            .CreateLogger();
#if DEBUG
            builder.Logging.AddDebug();

#endif
            builder.Services.AddSerilog(serilogLogger);
            builder.Services.AddHttpClient<HttpMetaDeckFetcher>();
            builder.Services.AddSingleton<IMetaDeckFetcher, HttpMetaDeckFetcher>();
            builder.Services.AddSingleton<IMetaDeckParser, LimitlessDeckParser>();
            builder.Services.AddSingleton<IMetaServiceFactory, MetaServiceFactory>();
            builder.Services.AddSingleton<ILimitlessMetaService>(sp =>
                sp.GetRequiredService<IMetaServiceFactory>().Create());
            // Concrete handler shows a modal; everything depends on the interface so tests
            // and any future non-UI host can substitute it. Previously new()'d at 54 call
            // sites, which made every catch path untestable — see project_error_handler_di.
            builder.Services.AddSingleton<IErrorHandler, ModalErrorHandler>();
            // Tracing. Registered as an interface for the same reason IErrorHandler is: a direct
            // SentrySdk call inside a service makes that service untestable without initialising
            // the SDK. See IPerformanceMonitor for why its surface is numbers-only.
            builder.Services.AddSingleton<IPerformanceMonitor, SentryPerformanceMonitor>();
            builder.Services.AddSingleton<ISqliteConnectionFactory>(sp =>
            {
                ILoggerFactory loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                ILogger<SqliteConnectionFactory> logger = loggerFactory.CreateLogger<SqliteConnectionFactory>();
                ILimitlessMetaService metaService = sp.GetRequiredService<ILimitlessMetaService>();
                IErrorHandler errorHandler = sp.GetRequiredService<IErrorHandler>();
                // The MAUI subclass, because it is the only one that can resolve an app-data
                // path. Everything else about the connection lives in Core.
                return new MauiSqliteConnectionFactory(logger, metaService, errorHandler);
            });
            builder.Services.AddSingleton<IMatchResultsCalculatorFactory, MatchResultCalculatorFactory>();
            builder.Services.AddSingleton<IMatchAnalysisService, MatchAnalysisService>();
            builder.Services.AddSingleton<ITrainerSwitchService, TrainerSwitchService>();
            builder.Services.AddSingleton<ITrainerHillImportService, TrainerHillImportService>();
            builder.Services.AddSingleton<IExportService, ExportService>();
            builder.Services.AddSingleton<IRestoreService, RestoreService>();
            builder.Services.AddSingleton<AppShellViewModel>();
            builder.Services.AddSingleton<AppShell>();

            //Link Pages and ViewModels

            // MainPage is a singleton for two reasons that genuinely hold — unlike the two
            // pages below, do NOT make this transient:
            //  1. AppShellViewModel is a singleton and injects MainPageViewModel to read
            //     HasUnsavedData before switching trainer. A transient view model would hand
            //     the shell its own permanently-empty instance (a captive dependency), so the
            //     "you have unsaved match data" prompt would silently never fire and a
            //     half-entered match would be lost without warning. It fails silently.
            //  2. The in-progress entry form surviving navigation is a feature: start logging
            //     a match, check the journal, come back, form intact.
            // This is also why the MainPage UI tests need a [TearDown] to reset state.
            builder.Services.AddSingleton<MainPage>();
            builder.Services.AddSingleton<MainPageViewModel>();

            // Read Journal and Trainer pages are singletons. The original reason (b21e9f4,
            // "so the match list / charts stay warm between navigations") NO LONGER HOLDS:
            // both pages fire AppearingCommand on every Appearing and AppearingAsync re-queries
            // the database and rebuilds the charts, so nothing is actually reused, and the
            // 500ms pre-warm that commit added was removed again in 8e39188.
            //
            // What DOES currently depend on the singleton lifetime is the event subscription.
            // These view models do `_switchService.TrainerChanged += OnTrainerChanged` in their
            // constructor and never unsubscribe, and the event holds a strong reference. As
            // singletons that is harmless — one instance, subscribed once. As transients, every
            // navigation would leak a view model and add another handler, so one trainer switch
            // would trigger a full reload per past visit.
            //
            // So: fix the subscription (weak event or unsubscribe on Disappearing) BEFORE
            // changing these back to transient. See ROADMAP.md.
            builder.Services.AddSingleton<ReadJournalPage>();
            builder.Services.AddSingleton<ReadJournalPageViewModel>();

            builder.Services.AddSingleton<TrainerPage>();
            builder.Services.AddSingleton<TrainerPageViewModel>();

            //Options Page
            builder.Services.AddTransient<OptionsPage>();
            builder.Services.AddTransient<OptionsPageViewModel>();

            //About Page
            builder.Services.AddTransient<AboutPage>();
            builder.Services.AddTransient<AboutPageViewModel>();
#if WINDOWS

            Microsoft.Maui.Controls.Handlers.Items.CollectionViewHandler.Mapper.AppendToMapping("DisableMultiselectCheckbox",
            (handler, view) =>
            {
                handler.PlatformView.IsMultiSelectCheckBoxEnabled = false;
            });

#endif

            return builder.Build();
        }
    }
}