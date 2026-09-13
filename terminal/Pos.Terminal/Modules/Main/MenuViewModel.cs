namespace Pos.Terminal.Modules.Main;

public sealed partial class MenuViewModel : AppViewModelBase
{
    [ObservableProperty]
    public partial Version Version { get; set; }

    public IObserveCommand ForwardCommand { get; }

    public MenuViewModel(IAppInfo appInfo)
    {
        Version = appInfo.Version;

        ForwardCommand = MakeAsyncCommand<ViewId>(x => Navigator.ForwardAsync(x));
    }

    protected override Task OnNotifyBackAsync()
    {
        AndroidHelper.MoveTaskToBack();
        return Task.CompletedTask;
    }
}
