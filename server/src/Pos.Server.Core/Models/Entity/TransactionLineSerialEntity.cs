namespace Pos.Server.Models.Entity;

public sealed class TransactionLineSerialEntity
{
    [Key]
    public Guid TransactionLineId { get; set; }

    [Key]
    public string SerialNumber { get; set; } = default!;
}
