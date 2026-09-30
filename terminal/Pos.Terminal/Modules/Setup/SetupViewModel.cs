namespace Pos.Terminal.Modules.Setup;

// 初期設定: 設定 QR (サーバ URL + ペアリングコード) を読むか、URL を入れてコードを電卓で入力し、SetupUsecase で登録・初回同期する。
// 登録済みの端末 (設定・同期からの登録し直し) は、登録せずに設定・同期へ戻れる
public sealed partial class SetupViewModel : AppViewModelBase
{
    private readonly IDialog dialog;

    private readonly IPopupNavigator popupNavigator;

    private readonly Settings settings;

    private readonly CredentialService credential;

    private readonly SetupUsecase setup;

    // 設定・同期の戻り先 (スキャンとの往復でも引き継ぐ)
    private ViewId? settingReturnTo;

    public EntryController ApiEndPoint { get; } = new();

    [ObservableProperty]
    public partial string? PairingCode { get; set; }

    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool CanBack { get; set; }

    public IObserveCommand InputPairingCodeCommand { get; }

    // 初回と登録の解除の後は根の画面: 戻るはプラットフォームに任せる (タスクを背面へ)
    public override bool HandlesBack => CanBack;

    public SetupViewModel(
        IDialog dialog,
        IPopupNavigator popupNavigator,
        Settings settings,
        CredentialService credential,
        SetupUsecase setup)
    {
        this.dialog = dialog;
        this.popupNavigator = popupNavigator;
        this.settings = settings;
        this.credential = credential;
        this.setup = setup;

        InputPairingCodeCommand = MakeAsyncCommand(InputPairingCodeAsync);
    }

    public override Task OnNavigatedToAsync(INavigationContext context)
    {
        CanBack = credential.IsRegistered;
        settingReturnTo = context.Parameter.GetCallerReturnTo();

        var scanned = context.Parameter.GetScanResult();
        if (scanned is not null)
        {
            var parser = new SettingParser(scanned);
            ApiEndPoint.Text = parser.GetString(nameof(Settings.ApiEndPoint));
            PairingCode = parser.GetString(nameof(PairingCode));
            Message = String.IsNullOrEmpty(ApiEndPoint.Text) || String.IsNullOrEmpty(PairingCode)
                ? "設定 QR ではありません。"
                : "QR を読み取りました。「登録」で端末を登録します。";
        }
        else
        {
            // 登録し直しのときは前の接続先を残す (コードは毎回発行し直す)
            ApiEndPoint.Text = settings.ApiEndPoint;
            PairingCode = null;
        }

        return Task.CompletedTask;
    }

    private async Task InputPairingCodeAsync()
    {
        PairingCode = await popupNavigator.InputPairingCodeAsync(PairingCode) ?? PairingCode;
    }

    protected override Task OnNotifyBackAsync() =>
        Navigator.ForwardAsync(ViewId.Setting, Parameters.Make().WithReturnTo(settingReturnTo ?? ViewId.Menu));

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    protected override Task OnNotifyFunction2() =>
        Navigator.ForwardAsync(ViewId.Scan, Parameters.Make().WithScan(ScanMode.Setup, ViewId.Setup, settingReturnTo));

    protected override Task OnNotifyFunction3() => InputPairingCodeAsync();

    protected override async Task OnNotifyFunction4()
    {
        if (!Uri.TryCreate(ApiEndPoint.Text?.Trim(), UriKind.Absolute, out var uri) || ((uri.Scheme != Uri.UriSchemeHttp) && (uri.Scheme != Uri.UriSchemeHttps)))
        {
            await dialog.InformationAsync("サーバ URL を入力してください。");
            ApiEndPoint.Focus();
            return;
        }

        if ((PairingCode is null) || (PairingCode.Length != Length.PairingCodeDigits))
        {
            await dialog.InformationAsync($"管理画面で発行したペアリングコード ({Length.PairingCodeDigits} 桁) を入力してください。");
            return;
        }

        // 接続先を切り替えて登録する (コードは一度だけ使える)
        var endPoint = new Uri(uri.ToString().EndsWith('/') ? uri.ToString() : uri + "/");
        var paired = await setup.PairAsync(endPoint, PairingCode);
        if (paired is null)
        {
            return;
        }

        PairingCode = null;
        Message = $"店舗: {paired.Store.Name}\n端末: {paired.Terminal.Name}";

        // 初回同期 (全件)
        ApiResult<Pos.Contract.Sync.SyncMastersResponse> result;
        using (var loading = dialog.Loading("同期しています..."))
        {
            result = await setup.ApplyAsync(new Progress<string>(loading.Update));
        }

        if (result.IsSuccess)
        {
            await dialog.Toast($"{paired.Store.Name} {paired.Terminal.Name} として登録しました。");
        }
        else
        {
            await dialog.InformationAsync("登録しましたが、同期に失敗しました。\n設定・同期から同期してください。\n" + result.Message);
        }

        await Navigator.ForwardAsync(ViewId.StaffSelect);
    }
}
