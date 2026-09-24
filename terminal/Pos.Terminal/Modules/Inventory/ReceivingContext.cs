namespace Pos.Terminal.Modules.Inventory;

// 検品中の伝票と数えた数。スキャン画面との行き来で失わないよう、ViewModel の [Scope] プロパティに Scope プラグインが注入する
public sealed class ReceivingContext
{
    public ReceivingDocument? Document { get; private set; }

    // 明細 ID → 届いた数 (数えた明細だけ)
    public Dictionary<Guid, decimal> Counts { get; } = [];

    public void Start(ReceivingDocument document)
    {
        Document = document;
        Counts.Clear();
    }

    public void Clear()
    {
        Document = null;
        Counts.Clear();
    }
}
