namespace Pos.Terminal.Usecases;

using Pos.Contract.Orders;
using Pos.Terminal.Models.Cart;

// 受注 (取り寄せ・取り置き。オンライン限定): 販売のカートから登録し、引き渡し待ちの受注から会計のカートを組み立てる
public sealed class OrderUsecase
{
    private readonly Session session;

    private readonly DataAccessor accessor;

    private readonly NetworkService network;

    public OrderUsecase(
        Session session,
        DataAccessor accessor,
        NetworkService network)
    {
        this.session = session;
        this.accessor = accessor;
        this.network = network;
    }

    // 受注にする (カートの明細・単価・会員を送る)。店舗・端末・担当が決まっているときだけ呼ぶ
    public ValueTask<ApiResult<OrderResponseItem>> CreateAsync(SalesCart cart, OrderType type, string? customerName, string? phone, DateOnly? requestedDate, string? note)
    {
        var request = new OrderCreateRequest
        {
            Id = Guid.NewGuid(),
            StoreId = session.Store!.Id,
            TerminalId = session.Terminal!.Id,
            StaffId = session.Staff!.Id,
            CustomerId = cart.Customer?.Id,
            CustomerName = customerName,
            Phone = phone,
            Type = type,
            RequestedDate = requestedDate,
            Note = note,
            OrderedAt = DateTime.UtcNow,
            Lines = cart.Lines.Select(static (x, i) => new OrderCreateRequestLine
            {
                Id = Guid.NewGuid(),
                LineNo = i + 1,
                ProductId = x.Product.Id,
                ProductCode = x.Product.Code,
                ProductName = x.Product.Name,
                Quantity = x.Quantity,
                UnitPrice = x.UnitPrice,
                Note = x.Note
            }).ToList()
        };
        return network.ExecuteAsync(h => h.PostOrderAsync(request));
    }

    // 受注から会計のカートを作る。単価は売価を変更できる商品だけ受注の単価にする (それ以外はサーバが売価との一致を求める)。
    // 端末のマスタにない商品があれば、カートを作らずその商品名を返す
    public async ValueTask<(SalesCart? Cart, string? MissingProduct)> ToCartAsync(OrderResponseItem order)
    {
        var taxRates = (await accessor.QueryTaxRateListAsync()).ToDictionary(static x => x.Id);
        var cart = new SalesCart { OrderId = order.Id, OrderNo = order.OrderNo };
        foreach (var line in order.Lines)
        {
            var product = await accessor.QueryProductAsync(line.ProductId);
            if ((product is null) || !taxRates.TryGetValue(product.TaxRateId, out var taxRate))
            {
                return (null, line.ProductName);
            }

            var cartLine = cart.Add(product, taxRate, line.Quantity);
            if (product.AllowsPriceOverride)
            {
                cartLine.UnitPrice = line.UnitPrice;
            }
        }

        if (order.CustomerId is not null)
        {
            var customer = await network.ExecuteAsync(h => h.GetCustomerAsync(order.CustomerId.Value), notify: false);
            cart.Customer = customer.Content;
        }

        return (cart, null);
    }
}
