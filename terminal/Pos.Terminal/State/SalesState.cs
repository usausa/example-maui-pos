namespace Pos.Terminal.State;

using Pos.Domain.Sales;
using Pos.Shared.Transactions;
using Pos.Terminal.Models.Sales;

// 販売 (T-10 〜 T-22) と返品 (T-40 〜 T-42) の画面間で共有する状態
public sealed class SalesState
{
    // 販売
    public Cart Cart { get; set; } = new();

    public Collection<CartPayment> Payments { get; } = [];

    // 会計完了の結果 (T-21 / T-22)
    public TransactionResponse? Completed { get; set; }

    public SalesResult? CompletedResult { get; set; }

    // 返品
    public TransactionResponse? ReturnOriginal { get; set; }

    public Collection<(TransactionResponseLine Line, decimal Quantity)> ReturnLines { get; } = [];

    public string? ReturnReason { get; set; }

    public void ResetSale()
    {
        Cart = new Cart();
        Payments.Clear();
    }

    public void ResetReturn()
    {
        ReturnOriginal = null;
        ReturnLines.Clear();
        ReturnReason = null;
        Payments.Clear();
    }
}
