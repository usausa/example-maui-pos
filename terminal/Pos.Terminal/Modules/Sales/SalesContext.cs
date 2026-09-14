namespace Pos.Terminal.Modules.Sales;

using Pos.Terminal.Models.Cart;

// 販売の画面 (販売 → 会計 → 完了) の間で引き回す状態。ナビゲーションのパラメータで渡す
public sealed class SalesContext
{
    public SalesCart Cart { get; set; } = new();

    public Collection<CartPayment> Payments { get; } = [];

    public void Reset()
    {
        Cart = new SalesCart();
        Payments.Clear();
    }
}
