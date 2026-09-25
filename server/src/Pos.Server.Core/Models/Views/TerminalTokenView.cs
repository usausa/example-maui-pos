namespace Pos.Server.Models.Views;

// 有効なトークンの照合結果 (端末が有効で削除されていないものだけ)
public sealed class TerminalTokenView
{
    public Guid Id { get; set; }

    public Guid TerminalId { get; set; }

    public Guid StoreId { get; set; }

    public string TerminalName { get; set; } = default!;
}
