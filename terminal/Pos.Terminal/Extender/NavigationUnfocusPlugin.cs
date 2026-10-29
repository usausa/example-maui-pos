namespace Pos.Terminal.Extender;

using Smart.Navigation.Plugins;

// 離れる画面の入力欄のフォーカスを外し、キーボードを次の画面に残さない。
// 積む遷移で戻すフォーカスは NavigationFocusPlugin が先に控えるので、登録はその後にする
public sealed class NavigationUnfocusPlugin : PluginBase
{
    public override void OnNavigatingFrom(IPluginContext pluginContext, INavigationContext navigationContext, object? view, object? target)
    {
        if (view is IVisualTreeElement element)
        {
            UnfocusDescendants(element);
        }
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
}
