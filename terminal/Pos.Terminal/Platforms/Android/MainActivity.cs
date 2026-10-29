#pragma warning disable IDE0130
// ReSharper disable once CheckNamespace
namespace Pos.Terminal;

using Android.App;
using Android.Content.PM;
using Android.OS;

[Activity(
    Name = "pos.terminal.MainActivity",
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    AlwaysRetainTaskState = true,
    LaunchMode = LaunchMode.SingleTask,
    LockTaskMode = "if_whitelisted",
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density |
                           ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden | ConfigChanges.Navigation | ConfigChanges.Touchscreen |
                           ConfigChanges.Locale | ConfigChanges.LayoutDirection | ConfigChanges.FontScale | ConfigChanges.Mcc | ConfigChanges.Mnc,
    ScreenOrientation = ScreenOrientation.Portrait)]
public sealed class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        Theme?.ApplyStyle(_Microsoft.Android.Resource.Designer.ResourceConstant.Style.NoOverScrollTheme, true);

        base.OnCreate(savedInstanceState);
    }
}
