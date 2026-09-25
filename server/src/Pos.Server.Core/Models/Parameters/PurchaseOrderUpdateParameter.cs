namespace Pos.Server.Models.Parameters;

using Pos.Server.Models.Entity;

// 発注の変更 (店舗は変えない。明細は全体を置き換える)
public sealed class PurchaseOrderUpdateParameter
{
    public Guid SupplierId { get; set; }

    public DateOnly? ExpectedDate { get; set; }

    public string? Note { get; set; }

    public IReadOnlyList<PurchaseOrderLineEntity> Lines { get; set; } = [];

    public int Version { get; set; }
}
