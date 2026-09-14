namespace Pos.Server.Services;

using Pos.Server.Accessors;

// 起動時のデータベース準備: PRAGMA (WAL) → スキーマ → 後から増えた列 → 初期データ
public sealed class DatabaseService
{
    private readonly IDbProvider provider;
    private readonly DatabaseAccessor databaseAccessor;
    private readonly MasterAccessor masterAccessor;
    private readonly ProductAccessor productAccessor;
    private readonly CustomerAccessor customerAccessor;
    private readonly ShiftAccessor shiftAccessor;
    private readonly TransactionAccessor transactionAccessor;
    private readonly InventoryAccessor inventoryAccessor;
    private readonly TimeProvider timeProvider;

    public DatabaseService(
        IDbProvider provider,
        DatabaseAccessor databaseAccessor,
        MasterAccessor masterAccessor,
        ProductAccessor productAccessor,
        CustomerAccessor customerAccessor,
        ShiftAccessor shiftAccessor,
        TransactionAccessor transactionAccessor,
        InventoryAccessor inventoryAccessor,
        TimeProvider timeProvider)
    {
        this.provider = provider;
        this.databaseAccessor = databaseAccessor;
        this.masterAccessor = masterAccessor;
        this.productAccessor = productAccessor;
        this.customerAccessor = customerAccessor;
        this.shiftAccessor = shiftAccessor;
        this.transactionAccessor = transactionAccessor;
        this.inventoryAccessor = inventoryAccessor;
        this.timeProvider = timeProvider;
    }

    public async ValueTask InitializeAsync(CancellationToken cancellationToken)
    {
        await provider.UsingAsync(con => databaseAccessor.ExecutePragmaAsync(con, cancellationToken), cancellationToken);

        masterAccessor.Create();
        productAccessor.Create();
        customerAccessor.Create();
        shiftAccessor.Create();
        transactionAccessor.Create();
        inventoryAccessor.Create();

        // 後から増えた列 (既存の DB に足す。足したときは初期データ相当の値を入れる)
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (await provider.UsingAsync(con => MasterAccessor.EnsurePaymentMethodShortNameAsync(con, cancellationToken), cancellationToken))
        {
            await InitialData.BackfillPaymentMethodShortNamesAsync(masterAccessor, now, cancellationToken);
        }

        await InitialData.SeedAsync(provider, masterAccessor, productAccessor, customerAccessor, inventoryAccessor, now, cancellationToken);
    }
}
