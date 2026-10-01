namespace Pos.Terminal.Usecases;

using Pos.Contract.Sync;

// 初期設定: 管理画面で発行したペアリングコードで端末を登録し、トークンと店舗・端末を保存して初回同期する
public sealed class SetupUsecase
{
    private readonly IAppInfo appInfo;

    private readonly IDeviceInfo deviceInfo;

    private readonly Settings settings;

    private readonly Session session;

    private readonly ApiContext apiContext;

    private readonly CredentialService credential;

    private readonly NetworkService network;

    private readonly SyncService sync;

    public SetupUsecase(
        IAppInfo appInfo,
        IDeviceInfo deviceInfo,
        Settings settings,
        Session session,
        ApiContext apiContext,
        CredentialService credential,
        NetworkService network,
        SyncService sync)
    {
        this.appInfo = appInfo;
        this.deviceInfo = deviceInfo;
        this.settings = settings;
        this.session = session;
        this.apiContext = apiContext;
        this.credential = credential;
        this.network = network;
        this.sync = sync;
    }

    // 新しい接続先へペアリングし、成功してから接続先とトークンを切り替える。コードの不一致・期限切れ・通信の失敗は NetworkService が通知する (null)。
    // 登録し直すときは、失敗したら今の登録に戻す。ローカル DB と未送信はそのまま使い、同じ店舗・端末番号の端末なら再登録後に送る
    public async ValueTask<TerminalPairResponse?> PairAsync(Uri endPoint, string pairingCode)
    {
        var request = new TerminalPairRequest
        {
            PairingCode = pairingCode,
            DeviceName = $"{deviceInfo.Manufacturer} {deviceInfo.Model}".Trim(),
            AppVersion = appInfo.VersionString
        };
        var suspended = credential.Suspend();
        var result = await network.ExecuteAsync(h => h.PairAsync(endPoint, request));
        if (!result.IsSuccess)
        {
            credential.Resume(suspended);
            return null;
        }

        // トークンを外している間に接続先を切り替える (古いトークンを新しい接続先へ送らないように)
        var paired = result.Content!;
        var previousStoreId = settings.StoreId;
        var previousTerminalId = settings.TerminalId;
        var sameRegister = IsSameRegister(paired);
        apiContext.BaseAddress = endPoint;
        settings.ApiEndPoint = endPoint.ToString();
        settings.StoreId = paired.Store.Id;
        settings.TerminalId = paired.Terminal.Id;

        // 店舗コード・端末番号が同じ端末 (新しいサーバの同じレジ) なら、前の登録の未送信をその端末のものにしてから送信を再開する。
        // 番号の違う端末には付け替えない (取引と在庫を別の端末・店舗のものにしない)
        if ((previousStoreId is { } fromStoreId) && (previousTerminalId is { } fromTerminalId) && (fromTerminalId != paired.Terminal.Id) && sameRegister)
        {
            await sync.ReassignOutboxAsync(fromStoreId, fromTerminalId, paired.Store.Id, paired.Terminal.Id);
        }

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

    // 今の登録 (Session) とレシート番号の店舗コード・端末番号が同じか
    private bool IsSameRegister(TerminalPairResponse paired) =>
        (session.Store?.Code == paired.Store.Code) && (session.Terminal?.TerminalNo == paired.Terminal.TerminalNo);
}
