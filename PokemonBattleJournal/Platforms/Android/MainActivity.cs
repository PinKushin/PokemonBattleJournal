using global::Android.App;
using global::Android.Content.PM;
using global::Android.Runtime;

namespace PokemonBattleJournal.Platforms.Android
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    [Register("com.PinKushin.PokemonBattleJournal.MainActivity")]
    public class MainActivity : MauiAppCompatActivity
    {
    }
}
