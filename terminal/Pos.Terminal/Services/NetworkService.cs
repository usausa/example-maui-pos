namespace Pos.Terminal.Services;

// オンライン限定の操作 (会員照会・他店在庫・レシート番号検索など) の実行。接続確認とエラー通知を共通化する
public sealed class NetworkService
{
    private readonly ILogger<NetworkService> log;

    private readonly IDialog dialog;

    private readonly DeviceState deviceState;

    private readonly HttpService httpService;

    public NetworkService(
        ILogger<NetworkService> log,
        IDialog dialog,
        DeviceState deviceState,
        HttpService httpService)
    {
        this.log = log;
        this.dialog = dialog;
        this.deviceState = deviceState;
        this.httpService = httpService;
    }

    public bool IsConnected => deviceState.NetworkState.IsConnected();

    // 404 は呼び出し側が扱う (notifyNotFound = false なら通知しない)
    public async ValueTask<ApiResult<T>> ExecuteAsync<T>(Func<HttpService, ValueTask<ApiResult<T>>> func, bool notify = true, bool notifyNotFound = false)
    {
        if (!IsConnected)
        {
            if (notify)
            {
                await dialog.InformationAsync("ネットワークに接続されていません。");
            }

            return new ApiResult<T>(ApiStatus.Unavailable, 0, default, null, null);
        }

        ApiResult<T> result;
        using (dialog.Indicator())
        {
            result = await func(httpService);
        }

        if (result.IsSuccess)
        {
            return result;
        }

        log.WarnApiFailed(result.Status, (int)result.StatusCode, result.ErrorCode, result.Exception);

        if (notify && (!result.IsNotFound || notifyNotFound))
        {
            await dialog.InformationAsync(result.Message);
        }

        return result;
    }
}
