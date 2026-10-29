namespace Pos.Terminal.Extender;

using Smart.Navigation.Plugins;

public sealed partial class NavigationFocusPlugin : PluginBase
{
    public override void OnNavigatingFrom(IPluginContext pluginContext, INavigationContext navigationContext, object? view, object? target)
    {
        // 離れる画面の入力欄のフォーカスを外す (戻ったときもフォーカスを戻さず、キーボードを出さない)
        if (view is IVisualTreeElement element)
        {
            UnfocusDescendants(element);
        }
    }

    public override void OnNavigatedTo(IPluginContext pluginContext, INavigationContext navigationContext, object view, object? target)
    {
        // 前の画面で開いたソフトキーボードは、画面を移っても Android が閉じないので、移った後に閉じる
        HideSoftInput();
    }

    private static void UnfocusDescendants(IVisualTreeElement element)
    {
        foreach (var child in element.GetVisualChildren())
        {
            if (child is VisualElement { IsFocused: true } visual)
            {
                visual.Unfocus();
            }

            UnfocusDescendants(child);
        }
    }

    private static partial void HideSoftInput();
}
