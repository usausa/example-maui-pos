namespace Pos.Server.Models.Views;

// 各項目は Host の Mapper (ソース生成) が読む
// ReSharper disable NotAccessedPositionalProperty.Global
// シフトの部門別集計
public sealed record CategoryTotal(
    Guid CategoryId,
    string Name,
    decimal Quantity,
    decimal NetAmount);
