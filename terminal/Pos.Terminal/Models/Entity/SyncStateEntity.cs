namespace Pos.Terminal.Models.Entity;

// Key / Value (最終同期の serverTime、レシート連番など)
public sealed class SyncStateEntity
{
    [Key]
    public string Key { get; set; } = default!;

    public string Value { get; set; } = default!;
}
