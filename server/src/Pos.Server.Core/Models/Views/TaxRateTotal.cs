namespace Pos.Server.Models.Views;

// 各項目は Host の Mapper (ソース生成) が読む
// ReSharper disable NotAccessedPositionalProperty.Global
// シフトの税率別集計 (返品は負として合算)
public sealed record TaxRateTotal(
    Guid TaxRateId,
    decimal Rate,
    bool TaxIncluded,
    decimal TaxableAmount,
    decimal TaxAmount);
