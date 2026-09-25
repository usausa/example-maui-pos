namespace Pos.Terminal.Usecases;

using Pos.Contract.Orders;
using Pos.Terminal.Models.Cart;
using Pos.Terminal.Models.Entity;

// 受注 (取り寄せ・取り置き。オンライン限定): 販売のカートから登録し、引き渡し待ちの受注から会計のカートを組み立てる。
// 前受金はシフトで受け取り・返し、サーバが受け付けた記録を端末にも写す (精算の予想現金に入る)
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
        var cart = new SalesCart { OrderId = order.Id, OrderNo = order.OrderNo, DepositAmount = order.DepositAmount };
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

    //--------------------------------------------------------------------------------
    // Deposit
    //--------------------------------------------------------------------------------

    // 支払方法の名前を引くための一覧 (無効・削除済みも含む)
    public async ValueTask<Dictionary<Guid, PaymentMethodResponseItem>> QueryPaymentMethodsAsync() =>
        (await accessor.QueryPaymentMethodListAsync()).ToDictionary(static x => x.Id);

    // 前受金の受取。シフトが開設中のときだけ呼ぶ
    public async ValueTask<ApiResult<OrderResponseItem>> DepositAsync(OrderResponseItem order, PaymentMethodResponseItem method, decimal amount, string? reference)
    {
        var request = new OrderDepositRequest
        {
            Id = Guid.NewGuid(),
            ShiftId = session.CurrentShift!.Id,
            TerminalId = session.Terminal!.Id,
            StaffId = session.Staff!.Id,
            PaymentMethodId = method.Id,
            Amount = amount,
            Reference = reference,
            OccurredAt = DateTime.UtcNow
        };
        var result = await network.ExecuteAsync(h => h.PostOrderDepositAsync(order.Id, request));
        await SaveDepositAsync(result, request.Id);
        return result;
    }

    // 前受金の返金 (全額を受け取った方法で)。シフトが開設中のときだけ呼ぶ
    public async ValueTask<ApiResult<OrderResponseItem>> RefundDepositAsync(OrderResponseItem order)
    {
        var request = new OrderDepositRefundRequest
        {
            Id = Guid.NewGuid(),
            ShiftId = session.CurrentShift!.Id,
            TerminalId = session.Terminal!.Id,
            StaffId = session.Staff!.Id,
            OccurredAt = DateTime.UtcNow
        };
        var result = await network.ExecuteAsync(h => h.PostOrderDepositRefundAsync(order.Id, request));
        await SaveDepositAsync(result, request.Id);
        return result;
    }

    // サーバが受け付けた記録 (応答の受注の前受金) を写す
    private async ValueTask SaveDepositAsync(ApiResult<OrderResponseItem> result, Guid id)
    {
        if ((result is not { IsSuccess: true, Content: { } order }) || (order.Deposits.FirstOrDefault(x => x.Id == id) is not { } deposit))
        {
            return;
        }

        await accessor.InsertOrderDepositAsync(new LocalOrderDepositEntity
        {
            Id = deposit.Id,
            OrderId = order.Id,
            ShiftId = deposit.ShiftId,
            Type = deposit.Type,
            PaymentMethodId = deposit.PaymentMethodId,
            Kind = deposit.Kind,
            Amount = deposit.Amount,
            OccurredAt = deposit.OccurredAt
        });
    }
}
