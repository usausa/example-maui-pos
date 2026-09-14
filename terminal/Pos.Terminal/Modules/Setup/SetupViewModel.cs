namespace Pos.Terminal.Modules.Setup;

// 初期設定: 設定 QR または手入力でサーバ・店舗・端末を決め、SetupUsecase で確認・保存・初回同期する
public sealed partial class SetupViewModel : AppViewModelBase
{
    private readonly IDialog dialog;

    private readonly Settings settings;

    private readonly SetupUsecase setup;

    public EntryController ApiEndPoint { get; } = new();

    public EntryController StoreId { get; } = new();

    public EntryController TerminalId { get; } = new();

    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    // 根の画面: 戻るはプラットフォームに任せる (タスクを背面へ)
    public override bool HandlesBack => false;

    public SetupViewModel(
        IDialog dialog,
        Settings settings,
        SetupUsecase setup)
    {
        this.dialog = dialog;
        this.settings = settings;
        this.setup = setup;
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
        var endPoint = new Uri(uri.ToString().EndsWith('/') ? uri.ToString() : uri + "/");
        var (target, error) = await setup.VerifyAsync(endPoint, storeId, terminalId);
        if (error is not null)
        {
            await dialog.InformationAsync(error);
            return;
        }

        if (target is null)
        {
            return;
        }

        Message = $"店舗: {target.Store.Name}\n端末: {target.Terminal.Name}";
        if (!await dialog.AskAsync($"店舗: {target.Store.Name}\n端末: {target.Terminal.Name}\nこの設定で開始しますか？", "接続確認", "開始"))
        {
            return;
        }

        // 設定を保存して初回同期 (全件)
        ApiResult<Pos.Contract.Sync.SyncMastersResponse> result;
        using (var loading = dialog.Loading("同期しています..."))
        {
            result = await setup.ApplyAsync(endPoint, storeId, terminalId, new Progress<string>(loading.Update));
        }

        if (!result.IsSuccess)
        {
            await dialog.InformationAsync("同期に失敗しました。\n" + result.Message);
            return;
        }

        await Navigator.ForwardAsync(ViewId.StaffSelect);
    }
}
