namespace Pos.Server.Host.Models.Forms;

using System.Collections.ObjectModel;

using Pos.Domain.Logic;
using Pos.Server.Models.Entity;
using Pos.Server.Models.Parameters;
using Pos.Server.Models.Views;

// 受注の登録・変更 (店舗・担当・種別は登録のときだけ)
public sealed class OrderForm
{
    // 変更のとき
    public Guid? Id { get; init; }

    public string? OrderNo { get; init; }

    public int Version { get; init; }

    public Guid? StoreId { get; set; }

    public Guid? StaffId { get; set; }

    public OrderType Type { get; set; } = OrderType.BackOrder;

    public CustomerEntity? Customer { get; set; }

    // 会員のときは省略でき、会員の名前を使う
    public string? CustomerName { get; set; }

    public string? Phone { get; set; }

    public DateTime? RequestedDate { get; set; }

    public string? Note { get; set; }

    public Collection<OrderFormLine> Lines { get; } = [];

    public bool IsNew => Id is null;

    public decimal Total => Lines.Sum(static x => x.Amount);

    // 変更用のフォーム。明細の商品は受注時点のスナップショットから起こす
    public static OrderForm FromDetail(OrderDetailView detail, CustomerEntity? customer)
    {
        var order = detail.Order;
        var form = new OrderForm
        {
            Id = order.Id,
            OrderNo = order.OrderNo,
            Version = order.Version,
            StoreId = order.StoreId,
            StaffId = order.StaffId,
            Type = order.Type,
            Customer = customer,
            CustomerName = order.CustomerName,
            Phone = order.Phone,
            RequestedDate = order.RequestedDate?.ToDateTime(TimeOnly.MinValue),
            Note = order.Note
        };
        foreach (var line in detail.Lines)
        {
            form.Lines.Add(new OrderFormLine
            {
                Id = line.Id,
                Product = new ProductEntity { Id = line.ProductId, Code = line.ProductCode, Name = line.ProductName, Price = line.UnitPrice },
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                Note = line.Note
            });
        }

        return form;
    }

    // 登録の内容 (Id は呼び出し側が採番する。受注日時は省略して登録時刻にする)
    public static OrderDetailView ToDetail(OrderForm form, Guid id) =>
        new()
        {
            Order = new OrderEntity
            {
                Id = id,
                StoreId = form.StoreId ?? Guid.Empty,
                StaffId = form.StaffId ?? Guid.Empty,
                CustomerId = form.Customer?.Id,
                CustomerName = form.CustomerName ?? string.Empty,
                Phone = form.Phone,
                Type = form.Type,
                RequestedDate = ToDateOnly(form.RequestedDate),
                Note = form.Note
            },
            Lines = ToLines(form)
        };

    public static OrderUpdateParameter ToParameter(OrderForm form) =>
        new()
        {
            CustomerId = form.Customer?.Id,
            CustomerName = form.CustomerName,
            Phone = form.Phone,
            RequestedDate = ToDateOnly(form.RequestedDate),
            Note = form.Note,
            Lines = ToLines(form),
            Version = form.Version
        };

    private static List<OrderLineEntity> ToLines(OrderForm form) =>
        form.Lines.Select(static x => new OrderLineEntity
        {
            Id = x.Id,
            ProductId = x.Product?.Id ?? Guid.Empty,
            ProductCode = x.Product?.Code ?? string.Empty,
            ProductName = x.Product?.Name ?? string.Empty,
            Quantity = x.Quantity,
            UnitPrice = x.UnitPrice,
            Note = x.Note
        }).ToList();

    private static DateOnly? ToDateOnly(DateTime? value) => value is null ? null : DateOnly.FromDateTime(value.Value);
}

public sealed class OrderFormLine
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public ProductEntity? Product { get; set; }

    public decimal Quantity { get; set; } = 1m;

    public decimal UnitPrice { get; set; }

    public string? Note { get; set; }

    public decimal Amount => OrderLogic.LineAmount(UnitPrice, Quantity);
}
