namespace Pos.Terminal.Services;

// 端末の登録 (ペアリングで受け取ったトークン)。トークンは SecureStorage に置き、通信用に ApiContext へ写す
public sealed class CredentialService
{
    private const string TokenKey = "TerminalToken";

    private readonly ISecureStorage secureStorage;

    private readonly Settings settings;

    private readonly ApiContext apiContext;

    public CredentialService(
        ISecureStorage secureStorage,
        Settings settings,
        ApiContext apiContext)
    {
        this.secureStorage = secureStorage;
        this.settings = settings;
        this.apiContext = apiContext;
    }

    // 接続先・店舗・端末が決まり、トークンを持っている
    public bool IsRegistered => settings.IsConfigured && (apiContext.Token is not null);

    // 起動時に読む
    public async ValueTask LoadAsync()
    {
        apiContext.Token = await secureStorage.GetAsync(TokenKey);
    }

    public async ValueTask SaveAsync(string token, DateTime pairedAt)
    {
        await secureStorage.SetAsync(TokenKey, token);
        apiContext.Token = token;
        settings.PairedAt = pairedAt;
    }

    // 登録の解除 (端末で解除したとき・サーバで解除されて 401 になったとき)。接続先・店舗・端末とローカル DB は残し、再登録後に未送信を送る
    public void Clear()
    {
        secureStorage.Remove(TokenKey);
        apiContext.Token = null;
        settings.PairedAt = null;
    }
}
