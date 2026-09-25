namespace Pos.Terminal.Services;

// 端末の登録 (ペアリングで受け取ったトークン)。トークンは SecureStorage に置き、通信用に ApiContext へ写す
public sealed partial class CredentialService
{
    private const string TokenKey = "TerminalToken";

    private readonly ILogger<CredentialService> log;

    private readonly ISecureStorage secureStorage;

    private readonly Settings settings;

    private readonly ApiContext apiContext;

    public CredentialService(
        ILogger<CredentialService> log,
        ISecureStorage secureStorage,
        Settings settings,
        ApiContext apiContext)
    {
        this.log = log;
        this.secureStorage = secureStorage;
        this.settings = settings;
        this.apiContext = apiContext;
    }

    // 接続先・店舗・端末が決まり、トークンを持っている
    public bool IsRegistered => settings.IsConfigured && (apiContext.Token is not null);

    // 起動時に読む
    public async ValueTask LoadAsync()
    {
        apiContext.Token = await GetTokenAsync();
    }

    public async ValueTask SaveAsync(string token, DateTime pairedAt)
    {
        await SetTokenAsync(token);
        apiContext.Token = token;
        settings.PairedAt = pairedAt;
    }

    // 登録の解除 (端末で解除したとき・サーバで解除されて 401 になったとき)。接続先・店舗・端末とローカル DB は残し、再登録後に未送信を送る
    public void Clear()
    {
        RemoveToken();
        apiContext.Token = null;
        settings.PairedAt = null;
    }

    //--------------------------------------------------------------------------------
    // SecureStorage
    //--------------------------------------------------------------------------------

    // キーストアの鍵が無効になると (画面ロックの変更など)、読み書きと削除が Java の例外になる。
    // 保存したトークンは取り戻せないので、保存領域ごと消して未登録として扱う (登録し直すと未送信の続きを送る)
    private async ValueTask<string?> GetTokenAsync()
    {
        try
        {
            return await secureStorage.GetAsync(TokenKey);
        }
        catch (Java.Lang.Throwable ex)
        {
            log.WarnSecureStorageReset(ex);
            ResetSecureStorage();
            settings.PairedAt = null;
            return null;
        }
    }

    private async ValueTask SetTokenAsync(string token)
    {
        try
        {
            await secureStorage.SetAsync(TokenKey, token);
        }
        catch (Java.Lang.Throwable ex)
        {
            log.WarnSecureStorageReset(ex);
            ResetSecureStorage();
            await secureStorage.SetAsync(TokenKey, token);
        }
    }

    private void RemoveToken()
    {
        try
        {
            secureStorage.Remove(TokenKey);
        }
        catch (Java.Lang.Throwable ex)
        {
            log.WarnSecureStorageReset(ex);
            ResetSecureStorage();
        }
    }

    private static partial void ResetSecureStorage();
}
