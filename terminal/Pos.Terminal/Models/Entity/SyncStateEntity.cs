namespace Pos.Terminal.Models.Entity;

using Smart.Data.Accessor.Attributes;

// Key / Value (最終同期の serverTime、レシート連番など)
[Name("SyncState")]
public sealed class SyncStateEntity
{
    [Key]
    public string Key { get; set; } = default!;

    public string Value { get; set; } = default!;
}
