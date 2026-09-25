namespace Pos.Server.Host.Models.Forms;

using System.Collections.ObjectModel;

using Pos.Server.Models.Entity;
using Pos.Server.Models.Parameters;
using Pos.Server.Models.Views;

// 発注の登録と変更 (変更は下書きのときだけで、店舗は変えない)
public sealed class PurchaseOrderForm
{
    // 変更する発注 (登録は null)
    public Guid? Id { get; set; }

    public int Version { get; set; }

    public Guid? StoreId { get; set; }

    public Guid? SupplierId { get; set; }

    // 希望納期
    public DateTime? ExpectedDate { get; set; }

    public string? Note { get; set; }

    public Collection<PurchaseOrderFormLine> Lines { get; } = [];

    public static PurchaseOrderForm FromDetail(PurchaseOrderDetailView detail)
    {
        var order = detail.PurchaseOrder;
        var form = new PurchaseOrderForm
        {
            Id = order.Id,
            Version = order.Version,
            StoreId = order.StoreId,
            SupplierId = order.SupplierId,
            ExpectedDate = order.ExpectedDate?.ToDateTime(TimeOnly.MinValue),
            Note = order.Note
        };
        foreach (var line in detail.Lines)
        {
            form.Lines.Add(new PurchaseOrderFormLine
            {
                Product = new ProductEntity { Id = line.ProductId, Code = line.ProductCode, Name = line.ProductName },
                Quantity = line.Quantity,
                Cost = line.Cost
            });
        }

        return form;
    }

    // 登録の内容 (Id・番号・状態・明細の写しはサービスが設定する)
    public static PurchaseOrderEntity ToEntity(PurchaseOrderForm form) =>
        new()
        {
            StoreId = form.StoreId ?? Guid.Empty,
            SupplierId = form.SupplierId ?? Guid.Empty,
            ExpectedDate = ToDate(form.ExpectedDate),
            Note = form.Note
        };

    public static PurchaseOrderUpdateParameter ToParameter(PurchaseOrderForm form) =>
        new()
        {
            SupplierId = form.SupplierId ?? Guid.Empty,
            ExpectedDate = ToDate(form.ExpectedDate),
            Note = form.Note,
            Lines = ToLines(form),
            Version = form.Version
        };

    public static IReadOnlyList<PurchaseOrderLineEntity> ToLines(PurchaseOrderForm form) =>
        form.Lines.Select(static x => new PurchaseOrderLineEntity
        {
            ProductId = x.Product?.Id ?? Guid.Empty,
            Quantity = x.Quantity,
            Cost = x.Cost
        }).ToList();

    private static DateOnly? ToDate(DateTime? value) => value is null ? null : DateOnly.FromDateTime(value.Value);
}

public sealed class PurchaseOrderFormLine
{
    public ProductEntity? Product { get; set; }

    public decimal Quantity { get; set; } = 1m;

    // 仕入単価 (省略できる)
    public decimal? Cost { get; set; }
}
