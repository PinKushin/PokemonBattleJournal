using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Serilog;
using Syncfusion.Maui.Core.Hosting;
using Syncfusion.Maui.Toolkit.Hosting;

namespace PokemonBattleJournal
{
	public static class MauiProgram
	{
		public static MauiApp CreateMauiApp()
		{
			MauiAppBuilder builder = MauiApp.CreateBuilder();
			builder
				.ConfigureSyncfusionToolkit()
				.UseMauiApp<App>()
				.ConfigureSyncfusionCore()
				.UseMauiCommunityToolkit()
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
				})
				.UseSentry(options =>
				{
					options.Dsn = "https://03ff3f5afbfdbbd038a8903bc0e6d5e4@o4508805406851072.ingest.us.sentry.io/4508805409538048";

#if DEBUG
					options.Debug = true;
					options.TracesSampleRate = 1.0F;
					options.MaxBreadcrumbs = 1000;
#else
					options.Debug = false;
					options.TracesSampleRate = 0.1;
					options.MaxBreadcrumbs = 300;
#endif
				});

#if DEBUG
			builder.Logging.AddDebug();

#endif
			builder.Services.AddSerilog(new LoggerConfiguration()
				.WriteTo.Debug()
				.WriteTo.File(Path.Combine(FileHelper.GetAppDataPath(), "log.txt"), rollingInterval: RollingInterval.Day)
				.CreateLogger());
			//Link Pages and ViewModels
			//First Start Page
			//Trainer Page
			builder.Services.AddTransient<FirstStartPage>();
			builder.Services.AddTransient<FirstStartPageViewModel>();
			//Main Page
			builder.Services.AddSingleton<MainPage>();
			builder.Services.AddSingleton<MainPageViewModel>();

			//Read Journal Page
			builder.Services.AddSingleton<ReadJournalPage>();
			builder.Services.AddSingleton<ReadJournalPageViewModel>();

			//Trainer Page
			builder.Services.AddTransient<TrainerPage>();
			builder.Services.AddTransient<TrainerPageViewModel>();

			//Options Page
			builder.Services.AddSingleton<OptionsPage>();
			builder.Services.AddSingleton<OptionsPageViewModel>();

			//About Page
			builder.Services.AddSingleton<AboutPage>();
			builder.Services.AddSingleton<AboutPageViewModel>();

#if DEBUG
			//Developer Test Page
			builder.Services.AddSingleton<TestPage>();
			builder.Services.AddSingleton<TestPageViewModel>();
#endif
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