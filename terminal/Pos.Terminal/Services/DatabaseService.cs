namespace Pos.Terminal.Services;

using Pos.Terminal.Helpers.Data;

using Smart.Data;

// ローカル DB の初期化 (PRAGMA・テーブル作成・後から増えた列の追加)
public sealed class DatabaseService
{
    private readonly IDbProvider provider;

    private readonly DataAccessor accessor;

    public DatabaseService(
        IDbProvider provider,
        DataAccessor accessor)
    {
        this.provider = provider;
        this.accessor = accessor;
    }

    public ValueTask InitializeAsync() =>
        provider.UsingAsync(async con =>
        {
            await accessor.ExecutePragmaAsync(con);
            await accessor.CreateTablesAsync(con);

            // 後から増えた列 (マスタは次の同期で埋まる)
            await SchemaHelper.EnsureColumnAsync(con, "PaymentMethods", "ShortName", "TEXT");
        });
}
