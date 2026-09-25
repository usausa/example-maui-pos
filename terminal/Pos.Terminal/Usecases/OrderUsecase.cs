namespace Pos.Terminal.Usecases;

using Pos.Contract.Orders;
using Pos.Domain.Logic;
using Pos.Terminal.Models.Cart;

// 受注 (取り寄せ・取り置き。オンライン限定): 販売のカートから登録し、引き渡し待ちの受注から会計のカートを組み立てる。
// 前受金はシフトで受け取り・返し、サーバが受け付けた記録のうち今のシフトの分を端末にも写す (精算の予想現金に入る)
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

    // 受注を読む。今のシフトで受け取った・返した前受金は、通信が途中で切れて写せなかった分もここで写す
    public async ValueTask<ApiResult<OrderResponseItem>> LoadAsync(Guid id)
    {
        var result = await network.ExecuteAsync(h => h.GetOrderAsync(id), notifyNotFound: true);
        await SaveShiftDepositsAsync(result);
        return result;
    }

    // 会計の画面に入るとき、オンラインなら受注を読み直して前受金を最新にする (保留の間などに返金されていることがある)。
    // 会計できない受注 (キャンセル・他の端末で会計済み) なら理由を返し、オフラインや通信の失敗はカートの値のまま進める
    public async ValueTask<RuleError?> RefreshOrderAsync(SalesCart cart)
    {
        if ((cart.OrderId is null) || !network.IsConnected)
        {
            return null;
        }

        var result = await network.ExecuteAsync(h => h.GetOrderAsync(cart.OrderId.Value), notify: false);
        if (result.IsNotFound)
        {
            return new RuleError(ErrorCode.OrderNotFound, RuleReason.OrderNotFound);
        }

        if (result is not { IsSuccess: true, Content: { } order })
        {
            return null;
        }

        await SaveShiftDepositsAsync(result);
        cart.DepositAmount = order.DepositAmount;
        return OrderLogic.ValidateCheckout(new OrderFact { Id = order.Id, StoreId = order.StoreId, Status = order.Status, DepositBalance = order.DepositAmount }, session.Store!.Id);
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
        await SaveShiftDepositsAsync(result);
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
        await SaveShiftDepositsAsync(result);
        return result;
    }

    // 応答の受注の前受金のうち、今のシフトの分を写す (写し済みの分は変わらない)
    private async ValueTask SaveShiftDepositsAsync(ApiResult<OrderResponseItem> result)
    {
        if ((result is not { IsSuccess: true, Content: { } order }) || (session.CurrentShift is not { } shift))
        {
            return;
        }

        foreach (var deposit in order.Deposits.Where(x => x.ShiftId == shift.Id))
        {
            await accessor.InsertOrderDepositAsync(deposit.Id, order.Id, deposit.ShiftId, deposit.Type, deposit.PaymentMethodId, deposit.Kind, deposit.Amount, deposit.OccurredAt);
        }
    }
}
