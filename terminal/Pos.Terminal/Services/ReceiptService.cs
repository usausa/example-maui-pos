namespace Pos.Terminal.Services;

using Pos.Contract.Transactions;

// 取引からレシート画像 (PNG) を組み立てる
public sealed class ReceiptService
{
    private readonly DataAccessor accessor;

    private readonly Session session;

    public ReceiptService(
        DataAccessor accessor,
        Session session)
    {
        this.accessor = accessor;
        this.session = session;
    }

    public async ValueTask<byte[]> BuildAsync(TransactionResponseItem transaction)
    {
        var staff = await accessor.QueryStaffAsync(transaction.StaffId);
        var methods = (await accessor.QueryPaymentMethodListAsync()).ToDictionary(static x => x.Id, static x => x.Name);
        var text = ReceiptTextBuilder.Build(transaction, session.Store, session.Terminal?.Name ?? string.Empty, staff?.Name ?? string.Empty, methods);
        return ReceiptImageBuilder.Build(text);
    }
}
