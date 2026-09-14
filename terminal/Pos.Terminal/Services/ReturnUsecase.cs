namespace Pos.Terminal.Services;

using Pos.Contract.Transactions;
using Pos.Domain.Logic;
using Pos.Terminal.Models.Cart;

// 返品: 元取引の検索 (オンラインならサーバの最新)、計算 (ReturnLogic)、返品取引の確定
public sealed class ReturnUsecase
{
    private readonly Session session;

    private readonly NetworkService network;

    private readonly SyncService sync;

    private readonly TransactionUsecase transaction;

    public ReturnUsecase(
        Session session,
        NetworkService network,
        SyncService sync,
        TransactionUsecase transaction)
    {
        this.session = session;
        this.network = network;
        this.sync = sync;
        this.transaction = transaction;
    }

    // オンラインならサーバの最新 (返品済数量) を使い、取れなければローカルの取引
    public async ValueTask<TransactionResponseItem?> FindOriginalAsync(Guid id)
    {
        if (network.IsConnected)
        {
            var result = await network.ExecuteAsync(h => h.GetTransactionAsync(id), notify: false);
            if (result.Content is not null)
            {
                return result.Content;
            }
        }

        return await transaction.QueryAsync(id);
    }

    public async ValueTask<TransactionResponseItem?> FindOriginalByReceiptNoAsync(string receiptNo)
    {
        if (network.IsConnected)
        {
            var result = await network.ExecuteAsync(h => h.LookupTransactionAsync(receiptNo), notify: false);
            if (result.Content is not null)
            {
                return result.Content;
            }
        }

        return await transaction.QueryByReceiptNoAsync(receiptNo);
    }

    public SalesResult Calculate(TransactionResponseItem original, IReadOnlyList<(TransactionResponseItemLine Line, decimal Quantity)> lines, IReadOnlyList<CartPayment> payments) =>
        ReturnLogic.Calculate(TransactionBuilder.ToReturnInput(original, lines, payments, session.TaxRounding));

    public IReadOnlyList<RuleError> Validate(TransactionResponseItem original, IReadOnlyList<(TransactionResponseItemLine Line, decimal Quantity)> lines, IReadOnlyList<CartPayment> payments) =>
        TransactionLogic.ValidateInput(TransactionBuilder.ToReturnInput(original, lines, payments, session.TaxRounding));

    // 返品の確定 (Validate が通った入力を渡す)。Session.CanTransact のときだけ呼ぶ
    public async ValueTask<TransactionResponseItem> CompleteAsync(TransactionResponseItem original, IReadOnlyList<(TransactionResponseItemLine Line, decimal Quantity)> lines, IReadOnlyList<CartPayment> payments, string? reason)
    {
        var result = Calculate(original, lines, payments);
        var receiptNo = await sync.NextReceiptNoAsync();
        var context = TransactionBuilder.CreateContext(session, receiptNo, DateTime.UtcNow);
        var request = TransactionBuilder.ToReturnRequest(original, lines, payments, result, context, reason);
        var response = TransactionBuilder.ToResponse(request);
        await transaction.RegisterAsync(request, response);
        return response;
    }
}
