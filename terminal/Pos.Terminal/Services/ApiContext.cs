namespace Pos.Terminal.Services;

// UI スレッドからの書き込みを通信スレッドが読むため volatile
public sealed class ApiContext
{
    private volatile Uri? baseAddress;

    public Uri? BaseAddress
    {
        get => baseAddress;
        set => baseAddress = value;
    }
}
