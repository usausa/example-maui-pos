namespace Pos.Terminal.Modules.Sales;

using Pos.Terminal.Models.Cart;

// 販売の画面 (販売・会計・会員選択・商品検索・配送・保留・スキャン) で共有する状態。
// ViewModel の [Scope] プロパティに Scope プラグインが注入し、どの画面からも参照されなくなると破棄する (会計完了やメニューへ戻ると新しいカートになる)
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
