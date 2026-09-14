namespace Pos.Server.Accessors;

using Pos.Server.Models.Entity;

[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class CustomerAccessor
{
    [Execute]
    public partial void Create();

    //--------------------------------------------------------------------------------
    // Customer
    //--------------------------------------------------------------------------------

    // keyword は呼び出し側でエスケープ済みの LIKE パターン (%...%)
    [ExecuteScalar]
    public partial ValueTask<long> CountAsync(string? keyword, string? code, string? phone, DateTime? updatedSince, bool includeDeleted, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<CustomerEntity>> QueryListAsync(string? keyword, string? code, string? phone, DateTime? updatedSince, bool includeDeleted, string sort, int limit, int offset, CancellationToken cancellationToken);

    [QueryFirst]
    [SelectSingle(typeof(CustomerEntity), Table = "Customers")]
    public partial ValueTask<CustomerEntity?> QueryAsync(Guid id, CancellationToken cancellationToken);

    [QueryFirst]
    public partial ValueTask<CustomerEntity?> QueryByCodeAsync(string code, CancellationToken cancellationToken);

    // 残高がマイナスの会員 (管理画面の警告)
    [Query]
    public partial ValueTask<List<CustomerEntity>> QueryNegativePointListAsync(int limit, CancellationToken cancellationToken);

    [Execute]
    [Insert(typeof(CustomerEntity), Table = "Customers")]
    public partial ValueTask<int> InsertAsync(CustomerEntity entity, CancellationToken cancellationToken);

    // PointBalance は更新しない (ポイントは AddPointsAsync で加減算する)。Version が一致する行だけ更新する
    [Execute]
    public partial ValueTask<int> UpdateAsync(
        Guid id,
        string code,
        string name,
        string? kana,
        string? phone,
        string? email,
        string? postalCode,
        string? address,
        DateOnly? birthDate,
        string? note,
        DateTime updatedAt,
        int version,
        CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> DeleteAsync(Guid id, DateTime updatedAt, CancellationToken cancellationToken);

    //--------------------------------------------------------------------------------
    // Points
    //--------------------------------------------------------------------------------

    // 残高を加減算し、処理後残高を返す (取引登録・取消・手動調整と同じトランザクションで使う)
    [ExecuteScalar]
    public partial ValueTask<int> AddPointsAsync(DbTransaction tx, Guid id, int delta, DateTime updatedAt, CancellationToken cancellationToken);

    [Execute]
    [Insert(typeof(PointHistoryEntity), Table = "PointHistories")]
    public partial ValueTask<int> InsertPointHistoryAsync(DbTransaction tx, PointHistoryEntity entity, CancellationToken cancellationToken);

    [ExecuteScalar]
    public partial ValueTask<long> CountPointHistoryAsync(Guid customerId, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<PointHistoryEntity>> QueryPointHistoryListAsync(Guid customerId, int limit, int offset, CancellationToken cancellationToken);
}
