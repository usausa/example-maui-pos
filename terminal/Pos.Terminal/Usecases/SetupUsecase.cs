namespace Pos.Terminal.Usecases;

using Pos.Contract.Sync;

// 初期設定: 管理画面で発行したペアリングコードで端末を登録し、トークンと店舗・端末を保存して初回同期する
public sealed class SetupUsecase
{
    private readonly IAppInfo appInfo;

    private readonly IDeviceInfo deviceInfo;

    private readonly Settings settings;

    private readonly ApiContext apiContext;

    private readonly CredentialService credential;

    private readonly NetworkService network;

    private readonly SyncService sync;

    public SetupUsecase(
        IAppInfo appInfo,
        IDeviceInfo deviceInfo,
        Settings settings,
        ApiContext apiContext,
        CredentialService credential,
        NetworkService network,
        SyncService sync)
    {
        this.appInfo = appInfo;
        this.deviceInfo = deviceInfo;
        this.settings = settings;
        this.apiContext = apiContext;
        this.credential = credential;
        this.network = network;
        this.sync = sync;
    }

    // 接続先を切り替えてペアリングする。コードの不一致・期限切れ・通信の失敗は NetworkService が通知する (null)。
    // 同じ端末の登録し直しならローカル DB と未送信はそのまま使い、再登録後に送る
    public async ValueTask<TerminalPairResponse?> PairAsync(Uri endPoint, string pairingCode)
    {
        apiContext.BaseAddress = endPoint;
        var request = new TerminalPairRequest
        {
            PairingCode = pairingCode,
            DeviceName = $"{deviceInfo.Manufacturer} {deviceInfo.Model}".Trim(),
            AppVersion = appInfo.VersionString
        };
        var result = await network.ExecuteAsync(h => h.PairAsync(request));
        if (!result.IsSuccess)
        {
            return null;
        }

        var paired = result.Content!;
        settings.ApiEndPoint = endPoint.ToString();
        settings.StoreId = paired.Store.Id;
        settings.TerminalId = paired.Terminal.Id;
        await credential.SaveAsync(paired.Token, DateTime.UtcNow);
        return paired;
    }

    // 初回同期 (全件)。終われば未送信の送信も再開する
    public async ValueTask<ApiResult<SyncMastersResponse>> ApplyAsync(IProgress<string>? progress)
    {
        var result = await sync.SyncMastersAsync(true, progress, CancellationToken.None);
        sync.Trigger();
        return result;
    }
}
