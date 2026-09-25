namespace Pos.Server.Accessors;

using Pos.Server.Models.Entity;
using Pos.Server.Models.Views;

[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class TerminalTokenAccessor
{
    [Execute]
    [Insert(typeof(TerminalTokenEntity))]
    public partial ValueTask<int> InsertAsync(DbTransaction tx, TerminalTokenEntity entity, CancellationToken cancellationToken);

    // まだ使われていないペアリングコード
    [QueryFirst]
    public partial ValueTask<TerminalTokenEntity?> QueryByPairingCodeAsync(string pairingCode, CancellationToken cancellationToken);

    // 有効なトークン (解除されておらず、端末が有効で削除されていない)
    [QueryFirst]
    public partial ValueTask<TerminalTokenView?> QueryTokenAsync(byte[] tokenHash, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<TerminalRegistrationView>> QueryRegistrationListAsync(CancellationToken cancellationToken);

    // コードを消費してトークンを持たせる。戻り値 0 = 使用済み
    [Execute]
    public partial ValueTask<int> UpdatePairedAsync(DbTransaction tx, Guid id, byte[] tokenHash, string deviceName, DateTime pairedAt, CancellationToken cancellationToken);

    // 端末の有効なトークンをすべて失効させる
    [Execute]
    public partial ValueTask<int> UpdateRevokedAsync(DbTransaction tx, Guid terminalId, DateTime revokedAt, CancellationToken cancellationToken);

    // 端末の未使用のペアリングコードを消す
    [Execute]
    public partial ValueTask<int> DeletePendingAsync(DbTransaction tx, Guid terminalId, CancellationToken cancellationToken);
}
