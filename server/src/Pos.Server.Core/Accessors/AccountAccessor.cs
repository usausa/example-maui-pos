namespace Pos.Server.Accessors;

using Pos.Server.Models.Entity;

[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class AccountAccessor
{
    [ExecuteScalar]
    public partial ValueTask<long> CountAsync(CancellationToken cancellationToken);

    // ログイン ID 順
    [Query]
    public partial ValueTask<List<AccountEntity>> QueryAllAsync(CancellationToken cancellationToken);

    [QueryFirst]
    [SelectSingle(typeof(AccountEntity))]
    public partial ValueTask<AccountEntity?> QueryAsync(Guid id, CancellationToken cancellationToken);

    [QueryFirst]
    public partial ValueTask<AccountEntity?> QueryByNameAsync(string name, CancellationToken cancellationToken);

    [Execute]
    [Insert(typeof(AccountEntity))]
    public partial ValueTask<int> InsertAsync(AccountEntity entity, CancellationToken cancellationToken);

    [QueryFirst]
    public partial ValueTask<AccountEntity?> UpdateAsync(Guid id, AccountRole role, bool isActive, DateTime updatedAt, int version, CancellationToken cancellationToken);

    [QueryFirst]
    public partial ValueTask<AccountEntity?> UpdatePasswordAsync(Guid id, byte[] password, DateTime updatedAt, int version, CancellationToken cancellationToken);

    // 版を変えない (ログイン中のセッションを無効にしない)
    [Execute]
    public partial ValueTask<int> UpdateLastLoginAsync(Guid id, DateTime lastLoginAt, CancellationToken cancellationToken);

    [Execute]
    [Delete(typeof(AccountEntity))]
    public partial ValueTask<int> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
