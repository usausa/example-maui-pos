namespace Pos.Terminal.Modules.Setup;

using System.Diagnostics.CodeAnalysis;

using BarcodeScanning;

// 初期設定: 上のカメラで管理画面の設定 QR (サーバ URL + ペアリングコード) を読むと、SetupUsecase で登録・初回同期する。
// QR がなければ URL を入れてコードを電卓で入力する。登録済みの端末 (設定・同期からの登録し直し) は、確かめてから登録し、登録せずに設定・同期へ戻れる
public sealed partial class SetupViewModel : AppViewModelBase
{
    private readonly IDialog dialog;

    private readonly IPopupNavigator popupNavigator;

    private readonly Settings settings;

    private readonly Session session;

    private readonly CredentialService credential;

    private readonly SetupUsecase setup;

    // 設定・同期の戻り先 (登録し直しから戻るときに引き継ぐ)
    private ViewId? settingReturnTo;

    // 同じ QR は読み直さない (失敗した QR を送り直して、ペアリングの試行回数の上限に掛からないように)
    private string lastScanned = string.Empty;

    private bool registering;

    public BarcodeController Controller { get; } = new();

    public EntryController ApiEndPoint { get; } = new();

    [ObservableProperty]
    public partial string? PairingCode { get; set; }

    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool CanBack { get; set; }

    public IObserveCommand DetectCommand { get; }

    public IObserveCommand InputPairingCodeCommand { get; }

    // 初回と登録の解除の後は根の画面: 戻るはプラットフォームに任せる (タスクを背面へ)
    public override bool HandlesBack => CanBack;

    public SetupViewModel(
        IDialog dialog,
        IPopupNavigator popupNavigator,
        Settings settings,
        Session session,
        CredentialService credential,
        SetupUsecase setup)
    {
        this.dialog = dialog;
        this.popupNavigator = popupNavigator;
        this.settings = settings;
        this.session = session;
        this.credential = credential;
        this.setup = setup;

        // 設定 QR だけを読む (商品のバーコードを拾わない)
        Controller.BarcodeFormat = BarcodeFormats.QRCode;
        Controller.TapToFocus = true;
        Controller.VibrationOnDetect = true;

        DetectCommand = MakeAsyncCommand<IReadOnlySet<BarcodeResult>>(DetectAsync);
        InputPairingCodeCommand = MakeAsyncCommand(InputPairingCodeAsync);
    }

    public override async Task OnNavigatedToAsync(INavigationContext context)
    {
        CanBack = credential.IsRegistered;
        settingReturnTo = context.Parameter.GetCallerReturnTo();

        // 登録し直しのときは前の接続先を残す (コードは毎回発行し直す)
        ApiEndPoint.Text = settings.ApiEndPoint;
        PairingCode = null;

        await Navigator.PostActionAsync(PrepareAsync);
    }

    private async Task PrepareAsync()
    {
        if (await Permissions.RequestCameraAsync())
        {
            Controller.Enable = true;
        }
        else
        {
            Message = "カメラの権限がありません。";
        }
    }

    public override Task OnNavigatingFromAsync(INavigationContext context)
    {
        Controller.Enable = false;
        Controller.TorchOn = false;
        return Task.CompletedTask;
    }

    //--------------------------------------------------------------------------------
    // Scan
    //--------------------------------------------------------------------------------

    private Task DetectAsync(IReadOnlySet<BarcodeResult> results)
    {
        if ((results.Count == 0) || registering)
        {
            return Task.CompletedTask;
        }

        var value = results.First().DisplayValue;
        if (String.IsNullOrEmpty(value) || (value == lastScanned))
        {
            return Task.CompletedTask;
        }

        lastScanned = value;

        // 設定 QR でなければ、入力中の欄は変えずに知らせるだけにする
        var parser = new SettingParser(value);
        var code = parser.GetString(nameof(PairingCode));
        if (!TryGetEndPoint(parser.GetString(nameof(Settings.ApiEndPoint)), out var endPoint) || !IsPairingCode(code))
        {
            Message = "設定 QR ではありません。";
            return Task.CompletedTask;
        }

        ApiEndPoint.Text = endPoint.ToString();
        PairingCode = code;
        return RegisterAsync(endPoint, code);
    }

    //--------------------------------------------------------------------------------
    // Register
    //--------------------------------------------------------------------------------

    private async Task InputPairingCodeAsync()
    {
        PairingCode = await popupNavigator.InputPairingCodeAsync(PairingCode) ?? PairingCode;
    }

    // 読み取りと F4 の登録。コードは一度だけ使え、登録し直すと今の登録は使えなくなるので、登録済みなら確かめる
    private async Task RegisterAsync(Uri endPoint, string code)
    {
        registering = true;
        Controller.PauseScanning = true;
        Message = string.Empty;
        try
        {
            if (CanBack && !await dialog.AskAsync(ReRegisterMessage(), "登録し直し", "登録"))
            {
                return;
            }

            var paired = await setup.PairAsync(endPoint, code);
            if (paired is null)
            {
                Message = "登録できませんでした。";
                return;
            }

            PairingCode = null;

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
        finally
        {
            registering = false;
            Controller.PauseScanning = false;
        }
    }

    // 未送信は同じ端末として登録し直したときだけ送れる
    private string ReRegisterMessage() =>
        session.UnsentCount > 0
            ? $"この端末を登録し直しますか？\n今の登録は使えなくなります。\n未送信が {session.UnsentCount} 件あります。別の端末やサーバとして登録すると送信できません。"
            : "この端末を登録し直しますか？\n今の登録は使えなくなります。";

    // http / https の絶対 URL。末尾は / にそろえる (API の相対パスをつなぐため)
    private static bool TryGetEndPoint(string? text, [NotNullWhen(true)] out Uri? endPoint)
    {
        if (Uri.TryCreate(text?.Trim(), UriKind.Absolute, out var uri) && ((uri.Scheme == Uri.UriSchemeHttp) || (uri.Scheme == Uri.UriSchemeHttps)))
        {
            endPoint = uri.ToString().EndsWith('/') ? uri : new Uri(uri + "/");
            return true;
        }

        endPoint = null;
        return false;
    }

    private static bool IsPairingCode([NotNullWhen(true)] string? code) =>
        (code is not null) && (code.Length == Length.PairingCodeDigits) && code.All(Char.IsAsciiDigit);

    //--------------------------------------------------------------------------------
    // Function
    //--------------------------------------------------------------------------------

    protected override Task OnNotifyBackAsync() =>
        Navigator.ForwardAsync(ViewId.Setting, Parameters.Make().WithReturnTo(settingReturnTo ?? ViewId.Menu));

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    protected override Task OnNotifyFunction2()
    {
        Controller.ToggleTorch();
        return Task.CompletedTask;
    }

    protected override Task OnNotifyFunction3() => InputPairingCodeAsync();

    protected override async Task OnNotifyFunction4()
    {
        if (!TryGetEndPoint(ApiEndPoint.Text, out var endPoint))
        {
            await dialog.InformationAsync("サーバ URL を入力してください。");
            ApiEndPoint.Focus();
            return;
        }

        var code = PairingCode;
        if (!IsPairingCode(code))
        {
            await dialog.InformationAsync($"管理画面で発行したペアリングコード ({Length.PairingCodeDigits} 桁) を入力してください。");
            return;
        }

        await RegisterAsync(endPoint, code);
    }
}
