namespace Pos.Server.Models.Views;

// 部門ごとの所属商品数 (削除済みを除く)
public sealed record CategoryProductCountView(Guid CategoryId, long Count);
