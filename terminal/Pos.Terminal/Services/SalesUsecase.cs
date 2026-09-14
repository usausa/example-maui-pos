namespace Pos.Terminal.Services;

using System.Text.Json;

using Pos.Contract.Transactions;
using Pos.Domain.Logic;
using Pos.Terminal.Models.Cart;
using Pos.Terminal.Models.Entity;

// 販売: 計算 (Pos.Domain の SalesLogic、サーバと同じ)、会計の確定、保留
public sealed class SalesUsecase
{
    private readonly DataAccessor accessor;

    private readonly Session session;

    private readonly SyncService sync;

    private readonly TransactionUsecase transaction;

    public SalesUsecase(
        DataAccessor accessor,
        Session session,
        SyncService sync,
        TransactionUsecase transaction)
    {
        this.accessor = accessor;
        this.session = session;
        this.sync = sync;
        this.transaction = transaction;
    }

    public SalesResult Calculate(SalesCart cart, IReadOnlyList<CartPayment> payments) =>
        SalesLogic.Calculate(TransactionBuilder.ToSalesInput(cart, payments, session.TaxRounding, session.PointBasis));

    public IReadOnlyList<RuleError> Validate(SalesCart cart, IReadOnlyList<CartPayment> payments) =>
        TransactionLogic.ValidateInput(TransactionBuilder.ToSalesInput(cart, payments, session.TaxRounding, session.PointBasis));

    // 会計の確定 (Validate が通った入力を渡す)。Session.CanTransact のときだけ呼ぶ
    public async ValueTask<TransactionResponseItem> CompleteAsync(SalesCart cart, IReadOnlyList<CartPayment> payments)
    {
        var result = Calculate(cart, payments);
        var receiptNo = await sync.NextReceiptNoAsync();
        var context = TransactionBuilder.CreateContext(session, receiptNo, DateTime.UtcNow);
        var request = TransactionBuilder.ToRequest(cart, payments, result, context);
        var response = TransactionBuilder.ToResponse(request);
        if (cart.Customer is not null)
        {
            response.PointsBalanceAfter = cart.Customer.PointBalance - result.PointsRedeemed + result.PointsEarned;
        }

        await transaction.RegisterAsync(request, response);
        return response;
    }

    //--------------------------------------------------------------------------------
    // Hold (端末ローカルのみ)
    //--------------------------------------------------------------------------------

    public ValueTask<List<HoldCartEntity>> QueryHoldListAsync() => accessor.QueryHoldCartListAsync();

    public async ValueTask HoldAsync(SalesCart cart)
    {
        await accessor.InsertHoldCartAsync(new HoldCartEntity
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            Summary = cart.Summary,
            Total = Calculate(cart, []).Total,
            Payload = JsonSerializer.Serialize(cart, HttpService.JsonOptions)
        });
    }

    // 呼び出し (読めたら保留から消す)
    public async ValueTask<SalesCart?> RecallAsync(HoldCartEntity hold)
    {
        var cart = JsonSerializer.Deserialize<SalesCart>(hold.Payload, HttpService.JsonOptions);
        if (cart is null)
        {
            return null;
        }

        await accessor.DeleteHoldCartAsync(hold.Id);
        return cart;
    }

    public async ValueTask DiscardHoldAsync(Guid id)
    {
        await accessor.DeleteHoldCartAsync(id);
    }
}
