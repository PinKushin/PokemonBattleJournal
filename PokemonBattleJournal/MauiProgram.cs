using Syncfusion.Maui.Core.Hosting;
using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;

using Syncfusion.Maui.Toolkit.Hosting;
namespace PokemonBattleJournal
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
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
                });
			
            #if DEBUG
    		builder.Logging.AddDebug();
            builder.Services.AddLogging(configure => configure.AddDebug());
            #endif
            //Link Pages and ViewModels
            //Main Page
            builder.Services.AddSingleton<MainPage>();
            builder.Services.AddSingleton<MainPageViewModel>();

            //Read Journal Page
            builder.Services.AddSingleton<ReadJournalPage>();
            builder.Services.AddSingleton<ReadJournalPageViewModel>();

            //Trainer Page
            builder.Services.AddSingleton<TrainerPage>();
            builder.Services.AddSingleton<TrainerPageViewModel>();

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
