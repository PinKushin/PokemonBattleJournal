using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;

namespace PokemonBattleJournal
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("FluentSystemIcons-Regular.ttf", FluentUI.FontFamily);
                });

            #if DEBUG
    		builder.Logging.AddDebug();
            builder.Services.AddLogging(configure => configure.AddDebug());
            #endif
            builder.Services.AddSingleton<MainPage>();
            builder.Services.AddSingleton<MainPageViewModel>();

            builder.Services.AddSingleton<AboutPage>();
            builder.Services.AddSingleton<AboutPageViewModel>();

            return builder.Build();
        }
    }
}
