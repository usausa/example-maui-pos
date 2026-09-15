namespace Pos.Server.Services;

using Pos.Server.Accessors;

// 起動時のデータベース準備: PRAGMA (WAL) → スキーマ → 後から増えた列 → 初期データ (SQL ファイル)
public sealed class DatabaseService
{
    private readonly IDbProvider provider;
    private readonly GenericAccessor genericAccessor;
    private readonly MasterAccessor masterAccessor;
    private readonly ProductAccessor productAccessor;
    private readonly CustomerAccessor customerAccessor;
    private readonly ShiftAccessor shiftAccessor;
    private readonly TransactionAccessor transactionAccessor;
    private readonly InventoryAccessor inventoryAccessor;
    private readonly TimeProvider timeProvider;

    public DatabaseService(
        IDbProvider provider,
        GenericAccessor genericAccessor,
        MasterAccessor masterAccessor,
        ProductAccessor productAccessor,
        CustomerAccessor customerAccessor,
        ShiftAccessor shiftAccessor,
        TransactionAccessor transactionAccessor,
        InventoryAccessor inventoryAccessor,
        TimeProvider timeProvider)
    {
        this.provider = provider;
        this.genericAccessor = genericAccessor;
        this.masterAccessor = masterAccessor;
        this.productAccessor = productAccessor;
        this.customerAccessor = customerAccessor;
        this.shiftAccessor = shiftAccessor;
        this.transactionAccessor = transactionAccessor;
        this.inventoryAccessor = inventoryAccessor;
        this.timeProvider = timeProvider;
    }

    // initialDataPath: 会社設定がない (= 空の) DB へ流し込む SQL ファイル
    public async ValueTask InitializeAsync(string initialDataPath, CancellationToken cancellationToken)
    {
        await provider.UsingAsync(con => genericAccessor.ExecutePragmaAsync(con, cancellationToken), cancellationToken);

        masterAccessor.Create();
        productAccessor.Create();
        customerAccessor.Create();
        shiftAccessor.Create();
        transactionAccessor.Create();
        inventoryAccessor.Create();

        // 後から増えた列 (既存の DB に足す。足したときは初期データ相当の値を入れる)
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (await provider.UsingAsync(con => GenericAccessor.EnsurePaymentMethodShortNameAsync(con, cancellationToken), cancellationToken))
        {
            await genericAccessor.BackfillPaymentMethodShortNamesAsync(now, cancellationToken);
        }

        // 会社設定がなければ (= 空の DB) 初期データを投入する
        if (await masterAccessor.QuerySettingsAsync(cancellationToken) is null)
        {
            var sql = await File.ReadAllTextAsync(initialDataPath, cancellationToken);
            await provider.UsingTxAsync(async (_, tx) =>
            {
                await genericAccessor.ExecuteScriptAsync(tx, sql, now, cancellationToken);
                await tx.CommitAsync(cancellationToken);
            }, cancellationToken);
        }
    }
}
