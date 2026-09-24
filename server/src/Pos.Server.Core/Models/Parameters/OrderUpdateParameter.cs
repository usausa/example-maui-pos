namespace Pos.Server.Models.Parameters;

using Pos.Server.Models.Entity;

// 受注の変更 (種別は変えない。明細は全体を置き換える)
public sealed class OrderUpdateParameter
{
    public Guid? CustomerId { get; set; }

    public string? CustomerName { get; set; }

    public string? Phone { get; set; }

    public DateOnly? RequestedDate { get; set; }

    public string? Note { get; set; }

    public IReadOnlyList<OrderLineEntity> Lines { get; set; } = [];

    public int Version { get; set; }
}
