namespace Pos.Server.Models.Entity;

[Name("ShiftDenominations")]
public sealed class ShiftDenominationEntity
{
    [Key]
    public Guid ShiftId { get; set; }

    [Key]
    public int Denomination { get; set; }

    public int Count { get; set; }
}
