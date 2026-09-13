namespace Pos.Terminal.Modules.Setup;

using Pos.Shared.Sync;

// T-00 初期設定: 設定 QR または手入力でサーバ・店舗・端末を決め、初回同期する
public sealed partial class SetupViewModel : AppViewModelBase
{
    private readonly IDialog dialog;

    private readonly Settings settings;

    private readonly ApiContext apiContext;

    private readonly NetworkOperator network;

    private readonly SyncWorker syncWorker;

    public EntryController ApiEndPoint { get; } = new();

    public EntryController StoreId { get; } = new();

    public EntryController TerminalId { get; } = new();

    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    public SetupViewModel(
        IDialog dialog,
        Settings settings,
        ApiContext apiContext,
        NetworkOperator network,
        SyncWorker syncWorker)
    {
        this.dialog = dialog;
        this.settings = settings;
        this.apiContext = apiContext;
        this.network = network;
        this.syncWorker = syncWorker;
    }

    public override Task OnNavigatedToAsync(INavigationContext context)
    {
        var scanned = context.Parameter.GetScanResult();
        if (scanned is not null)
        {
            var parser = new SettingParser(scanned);
            ApiEndPoint.Text = parser.GetString(nameof(Settings.ApiEndPoint));
            StoreId.Text = parser.GetString(nameof(Settings.StoreId));
            TerminalId.Text = parser.GetString(nameof(Settings.TerminalId));
            Message = String.IsNullOrEmpty(ApiEndPoint.Text) ? "設定 QR ではありません。" : "QR を読み取りました。「開始」で接続を確認します。";
        }
        else
        {
            ApiEndPoint.Text = settings.ApiEndPoint;
            StoreId.Text = settings.StoreId?.ToString() ?? string.Empty;
            TerminalId.Text = settings.TerminalId?.ToString() ?? string.Empty;
        }

        return Task.CompletedTask;
    }

    protected override Task OnNotifyBackAsync()
    {
        AndroidHelper.MoveTaskToBack();
        return Task.CompletedTask;
    }

    protected override Task OnNotifyFunction2() =>
        Navigator.ForwardAsync(ViewId.Scan, Parameters.Make().WithScan(ScanMode.Setup, ViewId.Setup));

    protected override Task OnNotifyFunction3()
    {
        ApiEndPoint.Focus();
        return Task.CompletedTask;
    }

    protected override async Task OnNotifyFunction4()
    {
        if (!Uri.TryCreate(ApiEndPoint.Text?.Trim(), UriKind.Absolute, out var uri) || ((uri.Scheme != Uri.UriSchemeHttp) && (uri.Scheme != Uri.UriSchemeHttps)))
        {
            await dialog.InformationAsync("サーバ URL を入力してください。");
            ApiEndPoint.Focus();
            return;
        }

        if (!Guid.TryParse(StoreId.Text?.Trim(), out var storeId))
        {
            await dialog.InformationAsync("店舗 ID を入力してください。");
            StoreId.Focus();
            return;
        }

        if (!Guid.TryParse(TerminalId.Text?.Trim(), out var terminalId))
        {
            await dialog.InformationAsync("端末 ID を入力してください。");
            TerminalId.Focus();
            return;
        }

        // 接続先を切り替えて店舗・端末を確認する
        var endPoint = uri.ToString().EndsWith('/') ? uri.ToString() : uri + "/";
        apiContext.BaseAddress = new Uri(endPoint);

        var store = await network.ExecuteAsync(h => h.GetStoreAsync(storeId), notifyNotFound: true);
        if (!store.IsSuccess)
        {
            return;
        }

        var terminal = await network.ExecuteAsync(h => h.GetTerminalAsync(terminalId), notifyNotFound: true);
        if (!terminal.IsSuccess)
        {
            return;
        }

        if (terminal.Content!.StoreId != storeId)
        {
            await dialog.InformationAsync("端末が店舗に属していません。");
            return;
        }

        Message = $"店舗: {store.Content!.Name}\n端末: {terminal.Content.Name}";
        if (!await dialog.AskAsync($"店舗: {store.Content.Name}\n端末: {terminal.Content.Name}\nこの設定で開始しますか？", "接続確認", "開始"))
        {
            return;
        }

        settings.ApiEndPoint = endPoint;
        settings.StoreId = storeId;
        settings.TerminalId = terminalId;

        // 初回同期 (全件)
        ApiResult<SyncMastersResponse> result;
        using (var loading = dialog.Loading("同期しています..."))
        {
            result = await syncWorker.SyncMastersAsync(true, new Progress<string>(loading.Update), CancellationToken.None);
        }

        if (!result.IsSuccess)
        {
            await dialog.InformationAsync("同期に失敗しました。\n" + result.Message);
            return;
        }

        await Navigator.ForwardAsync(ViewId.StaffSelect);
    }
}
