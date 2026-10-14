namespace Pos.Terminal.Extender;

public sealed class PopupActivationPlugin : IPopupPlugin
{
    private readonly INavigator navigator;

    private INavigationLifecycleSupport? target;

    private int depth;

    public PopupActivationPlugin(INavigator navigator)
    {
        this.navigator = navigator;
    }

    public void Extend(ContentView view)
    {
    }

    public void OnOpening(ContentView view)
    {
        depth++;
        if (depth == 1)
        {
            // 最初のポップアップを開くときだけ後ろの画面を止める (重ねて開いても止めたまま)
            target = navigator.CurrentTarget as INavigationLifecycleSupport;
            target?.OnDeactivated();
        }
    }

    public void OnClosed(ContentView view)
    {
        depth--;
        if (depth == 0)
        {
            // 最後のポップアップを閉じたら、開いている間に遷移していなければ後ろの画面を戻す
            if ((target is not null) && ReferenceEquals(navigator.CurrentTarget, target))
            {
                target.OnActivated();
            }

            target = null;
        }
    }
}
