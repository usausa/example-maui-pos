namespace Pos.Terminal.Extender;

using Smart.Maui;
using Smart.Navigation.Plugins;

public sealed class NavigationFeedbackPlugin : PluginBase
{
    public override void OnNavigatedTo(IPluginContext pluginContext, INavigationContext navigationContext, object view, object? target)
    {
        // Android で画面を移ったとき、下端のボタンを押した反応が新しい画面で続かないように止める
#if ANDROID
        if (view is not Element element)
        {
            return;
        }

        var page = element.FindParent<Page>();
        if (page is null)
        {
            return;
        }

        Application.Current?.Dispatcher.Dispatch(() =>
        {
            // ViewGroup の JumpDrawablesToCurrentState は子孫にも伝わる
            if (page.Handler?.PlatformView is Android.Views.View platformView)
            {
                platformView.JumpDrawablesToCurrentState();
            }
        });
#endif
    }
}
