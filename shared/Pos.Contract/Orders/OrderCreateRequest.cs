namespace Pos.Contract.Orders;

// 受注の登録 (POST /orders)。同じ id の再送は既存を返す。会員か宛名のどちらかが必要
public sealed class OrderCreateRequest : IValidatableObject
{
    // 端末 / 管理画面が採番
    public Guid Id { get; set; }

    public Guid StoreId { get; set; }

    // 管理画面で登録したときは null
    public Guid? TerminalId { get; set; }

    public Guid StaffId { get; set; }

    public Guid? CustomerId { get; set; }

    // 会員のときは省略でき、会員の名前を使う
    [MaxLength(Length.Name)]
    public string? CustomerName { get; set; }

    [MaxLength(Length.Phone)]
    public string? Phone { get; set; }

    public OrderType Type { get; set; }

    // 希望日 (取り寄せの入荷予定・取り置きの期限)
    public DateOnly? RequestedDate { get; set; }

    [MaxLength(Length.Note)]
    public string? Note { get; set; }

    public DateTime OrderedAt { get; set; }

    [Required]
    [MinLength(1)]
    public IReadOnlyList<OrderCreateRequestLine> Lines { get; set; } = default!;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if ((CustomerId is null) && String.IsNullOrWhiteSpace(CustomerName))
        {
            yield return new ValidationResult("会員か宛名を指定してください", [nameof(CustomerName)]);
        }
    }
}

public sealed class OrderCreateRequestLine
{
    public Guid Id { get; set; }

    // 1 始まり
    public int LineNo { get; set; }

    public Guid ProductId { get; set; }

    // 受注時点のスナップショット
    [Required]
    [MaxLength(Length.Code)]
    public string ProductCode { get; set; } = default!;

    [Required]
    [MaxLength(Length.Name)]
    public string ProductName { get; set; } = default!;

    [Range(0.01, double.MaxValue)]
    public decimal Quantity { get; set; }

    // 約束した単価 (会計ではこの単価を目安にする)
    [Range(0, double.MaxValue)]
    public decimal UnitPrice { get; set; }

    [MaxLength(Length.LineNote)]
    public string? Note { get; set; }
}
