namespace Pos.Terminal.Services;

// UI スレッドからの書き込みを通信スレッドが読むため volatile
public sealed class ApiContext
{
    private volatile Uri? baseAddress;

    private volatile string? token;

    public Uri? BaseAddress
    {
        get => baseAddress;
        set => baseAddress = value;
    }

    // 端末のトークン (ペアリングで受け取り、要求に Bearer で付ける)。null = 未登録
    public string? Token
    {
        get => token;
        set => token = value;
    }

    // トークンが無効 (401) になった。通信は並行して走るので、トークンを外した最初の 1 回だけ知らせる
    public event EventHandler? Unauthorized;

    public void NotifyUnauthorized(string sentToken)
    {
        if (Interlocked.CompareExchange(ref token, null, sentToken) == sentToken)
        {
            Unauthorized?.Invoke(this, EventArgs.Empty);
        }
    }
}
