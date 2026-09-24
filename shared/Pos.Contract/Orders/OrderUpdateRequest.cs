namespace Pos.Contract.Orders;

// 受注の変更 (PUT /orders/{id})。未完了のときだけ。種別は変えない。明細は全体を置き換える
public sealed class OrderUpdateRequest : IValidatableObject
{
    public Guid? CustomerId { get; set; }

    [MaxLength(Length.Name)]
    public string? CustomerName { get; set; }

    [MaxLength(Length.Phone)]
    public string? Phone { get; set; }

    public DateOnly? RequestedDate { get; set; }

    [MaxLength(Length.Note)]
    public string? Note { get; set; }

    [Required]
    [MinLength(1)]
    public IReadOnlyList<OrderUpdateRequestLine> Lines { get; set; } = default!;

    public int Version { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if ((CustomerId is null) && String.IsNullOrWhiteSpace(CustomerName))
        {
            yield return new ValidationResult("会員か宛名を指定してください", [nameof(CustomerName)]);
        }
    }
}

public sealed class OrderUpdateRequestLine
{
    public Guid Id { get; set; }

    public int LineNo { get; set; }

    public Guid ProductId { get; set; }

    [Required]
    [MaxLength(Length.Code)]
    public string ProductCode { get; set; } = default!;

    [Required]
    [MaxLength(Length.Name)]
    public string ProductName { get; set; } = default!;

    [Range(0.01, double.MaxValue)]
    public decimal Quantity { get; set; }

    [Range(0, double.MaxValue)]
    public decimal UnitPrice { get; set; }

    [MaxLength(Length.LineNote)]
    public string? Note { get; set; }
}
