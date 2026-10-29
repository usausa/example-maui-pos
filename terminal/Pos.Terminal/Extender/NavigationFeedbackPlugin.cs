namespace Pos.Terminal.Extender;

using Smart.Maui;
using Smart.Navigation.Plugins;

public sealed class NavigationFeedbackPlugin : PluginBase
{
    public override void OnNavigatedTo(IPluginContext pluginContext, INavigationContext navigationContext, object view, object? target)
    {
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
            if (page.Handler?.PlatformView is Android.Views.View platformView)
            {
                platformView.JumpDrawablesToCurrentState();
            }
        });
#endif
    }
}
