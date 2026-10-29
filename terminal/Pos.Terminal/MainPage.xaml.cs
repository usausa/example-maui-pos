namespace Pos.Terminal;

using Pos.Terminal.Shell;

public sealed partial class MainPage
{
    private readonly ILogger<MainPage> log;

    public MainPage(ILogger<MainPage> log)
    {
        this.log = log;

        InitializeComponent();
    }

    protected override bool OnBackButtonPressed()
    {
        if (BindingContext is MainPageViewModel { BusyState.IsBusy: false } context)
        {
            // 待たずに進めるが、例外はログに残す
            context.Navigator.NotifyAsync(ShellEvent.Back).ContinueWith(
                t => log.WarnUnhandledNavigationError(t.Exception!),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }

        return true;
    }
}
