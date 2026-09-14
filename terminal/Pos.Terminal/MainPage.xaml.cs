namespace Pos.Terminal;

using Pos.Terminal.Modules;
using Pos.Terminal.Shell;

public sealed partial class MainPage
{
    public MainPage()
    {
        InitializeComponent();
    }

    // 根の画面 (戻るを扱わない画面) ではプラットフォームの既定動作に任せる
    protected override bool OnBackButtonPressed()
    {
        if (BindingContext is not MainPageViewModel { BusyState.IsBusy: false } context)
        {
            return true;
        }

        if (context.Navigator.CurrentTarget is AppViewModelBase { HandlesBack: false })
        {
            return false;
        }

        context.Navigator.NotifyAsync(ShellEvent.Back);
        return true;
    }
}
