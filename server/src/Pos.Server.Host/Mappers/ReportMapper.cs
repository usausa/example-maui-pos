namespace Pos.Server.Host.Mappers;

using Pos.Server.Models;
using Pos.Shared.Reports;

using Smart.Mapper;

public static partial class ReportMapper
{
    [Mapper]
    [MapProperty(nameof(SalesSummaryResponseRow.Key), nameof(SalesSummaryRow.GroupKey))]
    [MapProperty(nameof(SalesSummaryResponseRow.Label), nameof(SalesSummaryRow.GroupLabel))]
    public static partial SalesSummaryResponseRow ToSummaryRow(SalesSummaryRow row);

    [Mapper]
    public static partial ProductSalesResponseRow ToProductRow(ProductSalesRow row);
}
