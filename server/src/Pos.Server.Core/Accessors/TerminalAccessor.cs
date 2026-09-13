namespace Pos.Server.Accessors;

using Pos.Server.Infrastructure.Data;
using Pos.Server.Models.Entity;

[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class TerminalAccessor
{
    [Execute]
    public partial void Create();

    [ExecuteScalar]
    public partial ValueTask<long> CountAsync(Guid? storeId, DateTime? updatedSince, bool includeDeleted, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<TerminalEntity>> QueryListAsync(Guid? storeId, DateTime? updatedSince, bool includeDeleted, string sort, int limit, int offset, CancellationToken cancellationToken);

    [QueryFirst]
    [SelectSingle(typeof(TerminalEntity), Table = "Terminals")]
    public partial ValueTask<TerminalEntity?> QueryAsync(Guid id, CancellationToken cancellationToken);

    [Execute]
    [Insert(typeof(TerminalEntity), Table = "Terminals")]
    public partial ValueTask<int> InsertAsync(TerminalEntity entity, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> UpdateAsync(
        Guid id,
        Guid storeId,
        int terminalNo,
        string name,
        bool isActive,
        DateTime updatedAt,
        int version,
        CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> DeleteAsync(Guid id, DateTime updatedAt, CancellationToken cancellationToken);

    // 取引登録時: 最終レシート連番を max(現在値, 今回) に更新し、最終通信時刻を記録する
    [Execute]
    public partial ValueTask<int> UpdateLastReceiptSeqAsync(DbTransaction tx, Guid id, int receiptSeq, DateTime seenAt, CancellationToken cancellationToken);

    // 削除可否 (開設中シフトがあれば IN_USE)
    [ExecuteScalar]
    public partial ValueTask<long> CountOpenShiftAsync(Guid terminalId, CancellationToken cancellationToken);
}
