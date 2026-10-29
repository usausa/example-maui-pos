namespace Pos.Terminal.Extender;

using Smart.Navigation.Plugins;

public sealed partial class NavigationFocusPlugin : PluginBase
{
    public override void OnNavigatedTo(IPluginContext pluginContext, INavigationContext navigationContext, object view, object? target)
    {
        // 前の画面で開いたソフトキーボードは、画面を移っても Android が閉じないので、移った後に閉じる
        HideSoftInput();
    }

    private static partial void HideSoftInput();
}
