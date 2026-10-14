namespace Pos.Server.Host.Models.Queries;

// スキャン用の商品の 1 件取得の条件。barcode か code のどちらかが要る (両方あれば barcode で引く)
public sealed record ProductLookupQuery(
    [FromQuery(Name = "barcode")] string? Barcode,
    [FromQuery(Name = "code")] string? Code) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (String.IsNullOrEmpty(Barcode) && String.IsNullOrEmpty(Code))
        {
            yield return new ValidationResult("barcode または code を指定してください", [nameof(Barcode), nameof(Code)]);
        }
    }
}
