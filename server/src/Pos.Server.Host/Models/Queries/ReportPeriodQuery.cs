namespace Pos.Server.Host.Models.Queries;

// レポート API の集計対象 (店舗と期間)。from / to の前後関係は入力検証で見る (どちらかが省略なら比較しない)。省略時の既定は ReportService.ResolvePeriod
public sealed record ReportPeriodQuery(Guid? StoreId, DateOnly? From, DateOnly? To) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (From > To)
        {
            yield return new ValidationResult("期間の指定が不正です", [nameof(To)]);
        }
    }
}
