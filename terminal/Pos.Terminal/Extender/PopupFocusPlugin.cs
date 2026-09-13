namespace Pos.Terminal.Extender;

using CommunityToolkit.Maui.Views;

using Pos.Terminal.Behaviors;

public sealed class PopupFocusPlugin : IPopupPlugin
{
    public void Extend(ContentView view)
    {
        if ((view is Popup popup) && !Focus.GetSuppressDefaultFocus(view))
        {
            popup.Opened += (_, _) =>
            {
                Application.Current?.Dispatcher.Dispatch(() => view.Content?.SetDefaultFocus());
            };
        }
    }
}
