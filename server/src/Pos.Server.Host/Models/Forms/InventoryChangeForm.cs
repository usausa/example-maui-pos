namespace Pos.Server.Host.Models.Forms;

using Pos.Server.Models.Entity;
using Pos.Server.Models.Parameters;

using Smart.Mapper;

// 棚卸・調整の登録 (S-43)
public sealed partial class InventoryChangeForm
{
    public Guid? StoreId { get; set; }

    public ProductEntity? Product { get; set; }

    public InventoryChangeType Type { get; set; } = InventoryChangeType.PhysicalCount;

    // PhysicalCount は実数、Adjustment は増減
    public decimal Quantity { get; set; }

    public Guid? ReasonId { get; set; }

    public string? Reason { get; set; }

    public Guid? StaffId { get; set; }

    // フォーム → 登録内容 (Id / OccurredAt は呼び出し側が付ける)
    [Mapper]
    [MapUsing(nameof(InventoryChangeParameter.StoreId), nameof(ResolveStoreId))]
    [MapUsing(nameof(InventoryChangeParameter.ProductId), nameof(ResolveProductId))]
    public static partial InventoryChangeParameter ToParameter(InventoryChangeForm form);

    private static Guid ResolveStoreId(InventoryChangeForm form) => form.StoreId ?? Guid.Empty;

    private static Guid ResolveProductId(InventoryChangeForm form) => form.Product?.Id ?? Guid.Empty;
}
