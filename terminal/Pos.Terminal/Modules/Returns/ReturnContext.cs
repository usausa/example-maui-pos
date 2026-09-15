namespace Pos.Terminal.Modules.Returns;

using Pos.Contract.Transactions;

// 返品の画面 (返品・明細選択・返金・スキャン) で共有する状態。ViewModel の [Scope] プロパティに Scope プラグインが注入する
public sealed class ReturnContext
{
    public TransactionResponseItem? Original { get; set; }

    public Collection<(TransactionResponseItemLine Line, decimal Quantity)> Lines { get; } = [];

    public string? Reason { get; set; }

    public void Reset()
    {
        Original = null;
        Lines.Clear();
        Reason = null;
    }
}
