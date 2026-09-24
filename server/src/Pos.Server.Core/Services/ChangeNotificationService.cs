namespace Pos.Server.Services;

public sealed class DataChangedEventArgs : EventArgs
{
    public DataChangeKind Kind { get; }

    public DataChangedEventArgs(DataChangeKind kind)
    {
        Kind = kind;
    }
}

// 変更の通知 (プロセス内)。取引・シフト・在庫・受注・日次締めを書いた後に発火し、管理画面 (Blazor Server の回線) に表示を読み直させる。
// 同じプロセスの中で完結するので SignalR は使わない。受け手は書き込みを止めないように、処理を後回しにしてすぐ戻る
public sealed class ChangeNotificationService
{
    public event EventHandler<DataChangedEventArgs>? Changed;

    public void Notify(DataChangeKind kind) => Changed?.Invoke(this, new DataChangedEventArgs(kind));
}
