namespace Pos.Server.Services;

using Pos.Server.Accessors;
using Pos.Server.Infrastructure.Security;
using Pos.Server.Models.Entity;

// 管理画面のアカウント。自分自身は削除・無効化・役割の変更をできない (管理者がいなくなるのを防ぐ)
public sealed class AccountService
{
    private readonly TimeProvider timeProvider;
    private readonly IDialect dialect;
    private readonly AccountAccessor accountAccessor;
    private readonly IPasswordProvider passwordProvider;

    public AccountService(
        TimeProvider timeProvider,
        IDialect dialect,
        AccountAccessor accountAccessor,
        IPasswordProvider passwordProvider)
    {
        this.timeProvider = timeProvider;
        this.dialect = dialect;
        this.accountAccessor = accountAccessor;
        this.passwordProvider = passwordProvider;
    }

    // アカウントがなければ初期の管理者を作る
    public async ValueTask InitializeAsync(string name, string password, CancellationToken cancellationToken)
    {
        if (await accountAccessor.CountAsync(cancellationToken) == 0)
        {
            await InsertAsync(name, password, AccountRole.Administrator, cancellationToken);
        }
    }

    // 無効なアカウントはログインできない。成功したら最終ログイン日時を記録する
    public async ValueTask<AccountEntity?> AuthenticateAsync(string name, string password, CancellationToken cancellationToken)
    {
        var account = await accountAccessor.QueryByNameAsync(name, cancellationToken);
        if ((account is null) || !account.IsActive || !passwordProvider.Match(password, account.Password))
        {
            return null;
        }

        account.LastLoginAt = timeProvider.GetUtcNow().UtcDateTime;
        await accountAccessor.UpdateLastLoginAsync(account.Id, account.LastLoginAt.Value, cancellationToken);
        return account;
    }

    // ログイン中のセッションが有効か (削除・無効化・役割やパスワードの変更で版が変わると無効)
    public async ValueTask<bool> IsSessionValidAsync(Guid id, int version, CancellationToken cancellationToken) =>
        await accountAccessor.QueryAsync(id, cancellationToken) is { IsActive: true } account && (account.Version == version);

    public ValueTask<List<AccountEntity>> QueryAllAsync(CancellationToken cancellationToken) =>
        accountAccessor.QueryAllAsync(cancellationToken);

    public ValueTask<AccountEntity?> QueryAsync(Guid id, CancellationToken cancellationToken) =>
        accountAccessor.QueryAsync(id, cancellationToken);

    public ValueTask<DataWriteStatus> InsertAsync(string name, string password, AccountRole role, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var entity = new AccountEntity
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            Password = passwordProvider.Generate(password),
            Role = role,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1
        };
        return ServiceHelper.InsertAsync(dialect, () => accountAccessor.InsertAsync(entity, cancellationToken));
    }

    // operatorId は操作しているアカウント (自分自身の役割・有効は変えられない)
    public async ValueTask<DataWriteResult<AccountEntity>> UpdateAsync(Guid id, AccountRole role, bool isActive, int version, Guid operatorId, CancellationToken cancellationToken)
    {
        if (id == operatorId)
        {
            return new DataWriteResult<AccountEntity>(DataWriteStatus.Invalid, null);
        }

        return await ServiceHelper.UpdateAsync(
            dialect,
            () => accountAccessor.UpdateAsync(id, role, isActive, timeProvider.GetUtcNow().UtcDateTime, version, cancellationToken),
            async () => await accountAccessor.QueryAsync(id, cancellationToken) is not null);
    }

    public ValueTask<DataWriteResult<AccountEntity>> UpdatePasswordAsync(Guid id, string password, int version, CancellationToken cancellationToken) =>
        ServiceHelper.UpdateAsync(
            dialect,
            () => accountAccessor.UpdatePasswordAsync(id, passwordProvider.Generate(password), timeProvider.GetUtcNow().UtcDateTime, version, cancellationToken),
            async () => await accountAccessor.QueryAsync(id, cancellationToken) is not null);

    public async ValueTask<DataWriteStatus> DeleteAsync(Guid id, Guid operatorId, CancellationToken cancellationToken)
    {
        if (id == operatorId)
        {
            return DataWriteStatus.Invalid;
        }

        return await accountAccessor.DeleteAsync(id, cancellationToken) > 0 ? DataWriteStatus.Success : DataWriteStatus.NotFound;
    }
}
