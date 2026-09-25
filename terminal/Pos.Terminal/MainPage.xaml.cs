namespace Pos.Terminal;

using Pos.Terminal.Modules;
using Pos.Terminal.Shell;

public sealed partial class MainPage
{
    private readonly ILogger<MainPage> log;

    public MainPage(ILogger<MainPage> log)
    {
        this.log = log;

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

        // 待たずに進めるが、例外はログに残す (観測されないまま次の起動で異常終了として知らせないように)
        context.Navigator.NotifyAsync(ShellEvent.Back).ContinueWith(
            t => log.WarnUnhandledNavigationError(t.Exception!),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
        return true;
    }
}
