namespace Pos.Terminal.Models.Entity;

using Smart.Data.Accessor.Attributes;

// 会計途中の保留 (端末ローカルのみ)。Payload は Cart の JSON
[Name("HoldCarts")]
public sealed class HoldCartEntity
{
    [Key]
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public string Summary { get; set; } = default!;

    public decimal Total { get; set; }

    public string Payload { get; set; } = default!;
}
