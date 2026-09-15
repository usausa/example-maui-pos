namespace Pos.Terminal.Usecases;

using Pos.Contract.Sync;

public sealed record SetupTarget(StoreResponseItem Store, TerminalResponseItem Terminal);

// 初期設定: 接続先を切り替えて店舗・端末を確認し、設定を保存して初回同期する
public sealed class SetupUsecase
{
    private readonly Settings settings;

    private readonly ApiContext apiContext;

    private readonly NetworkService network;

    private readonly SyncService sync;

    public SetupUsecase(
        Settings settings,
        ApiContext apiContext,
        NetworkService network,
        SyncService sync)
    {
        this.settings = settings;
        this.apiContext = apiContext;
        this.network = network;
        this.sync = sync;
    }

    // 通信の失敗は NetworkService が通知する (Target も Error も null)
    public async ValueTask<(SetupTarget? Target, string? Error)> VerifyAsync(Uri endPoint, Guid storeId, Guid terminalId)
    {
        apiContext.BaseAddress = endPoint;
        var store = await network.ExecuteAsync(h => h.GetStoreAsync(storeId), notifyNotFound: true);
        if (!store.IsSuccess)
        {
            return (null, null);
        }

        var terminal = await network.ExecuteAsync(h => h.GetTerminalAsync(terminalId), notifyNotFound: true);
        if (!terminal.IsSuccess)
        {
            return (null, null);
        }

        if (terminal.Content!.StoreId != storeId)
        {
            return (null, "端末が店舗に属していません。");
        }

        return (new SetupTarget(store.Content!, terminal.Content), null);
    }

    // 設定を保存して初回同期 (全件)
    public ValueTask<ApiResult<SyncMastersResponse>> ApplyAsync(Uri endPoint, Guid storeId, Guid terminalId, IProgress<string>? progress)
    {
        settings.ApiEndPoint = endPoint.ToString();
        settings.StoreId = storeId;
        settings.TerminalId = terminalId;
        return sync.SyncMastersAsync(true, progress, CancellationToken.None);
    }
}
