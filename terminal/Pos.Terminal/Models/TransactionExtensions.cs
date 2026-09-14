namespace Pos.Terminal.Models;

using Pos.Contract.Transactions;
using Pos.Terminal.Models.Entity;

public static class TransactionExtensions
{
    public static bool IsCompleted(this TransactionStatus status) => status == TransactionStatus.Completed;

    public static bool IsVoided(this TransactionStatus status) => status == TransactionStatus.Voided;

    public static bool IsSale(this TransactionType type) => type == TransactionType.Sale;

    public static bool IsReturn(this TransactionType type) => type == TransactionType.Return;

    public static bool IsOpen(this ShiftStatus status) => status == ShiftStatus.Open;

    public static bool IsFailed(this OutboxStatus status) => status == OutboxStatus.Failed;

    // 返品できる明細が残っているか
    public static bool HasReturnableLine(this TransactionResponseItem transaction) =>
        transaction.Lines.Any(static x => x.Quantity > x.ReturnedQuantity);

    // 完了した販売で、返品できる明細が残っているもの
    public static bool IsReturnable(this TransactionResponseItem transaction) =>
        transaction.Type.IsSale() && transaction.Status.IsCompleted() && transaction.HasReturnableLine();
}
