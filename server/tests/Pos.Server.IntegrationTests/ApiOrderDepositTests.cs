namespace Pos.Server;

using System.Text.Json;

using Pos.Contract.Orders;
using Pos.Contract.PaymentMethods;
using Pos.Contract.Shifts;
using Pos.Contract.Transactions;
using Pos.Server.Host.Endpoints;

// 受注の前受金: 受取 → 会計で全額を充てる → 取消で戻る → 返金 → キャンセル。現金の前受金はシフトの予想現金に入る
public sealed class ApiOrderDepositTests : IClassFixture<TestApplicationFactory>
{
    private static readonly DateTime Now = new(2026, 9, 20, 2, 0, 0, DateTimeKind.Utc);

    private static readonly DateOnly BusinessDate = new(2026, 9, 20);

    private readonly TestApplicationFactory factory;

    private readonly JsonSerializerOptions options;

    public ApiOrderDepositTests(TestApplicationFactory factory)
    {
        this.factory = factory;
        options = factory.JsonOptions();
    }

    // 受け取った前受金は会計で充て、会計の取消で戻り、返すとキャンセルできる
    [Fact]
    public async Task DepositIsAppliedAtCheckoutAndRefundedBeforeCancel()
    {
        // Arrange
        var client = await factory.CreateAdminClientAsync();
        var shiftId = await OpenShiftAsync(client, TestData.MainStoreId, TestData.MainTerminal1Id);
        var order = await CreateOrderAsync(client);
        var deposit = DepositRequest(shiftId, TestData.MainTerminal1Id, TestData.CashPaymentMethodId, 500m);

        // Act / Assert: 受取は 201、再送は 200、同じ id で内容違いは 409、2 つ目の前受金は 422
        using var depositResponse = await client.PostJsonAsync($"{ApiRoutes.Orders}/{order.Id}/deposit", deposit, options);
        var deposited = await depositResponse.ReadAsAsync<OrderResponseItem>(HttpStatusCode.Created, options);
        Assert.Equal(500m, deposited.DepositAmount);
        var received = Assert.Single(deposited.Deposits);
        Assert.Equal((OrderDepositType.Receive, PaymentKind.Cash, 500m, shiftId), (received.Type, received.Kind, received.Amount, received.ShiftId));
        using var resendResponse = await client.PostJsonAsync($"{ApiRoutes.Orders}/{order.Id}/deposit", deposit, options);
        Assert.Single((await resendResponse.ReadAsAsync<OrderResponseItem>(HttpStatusCode.OK, options)).Deposits);
        var mismatch = DepositRequest(shiftId, TestData.MainTerminal1Id, TestData.CashPaymentMethodId, 600m);
        mismatch.Id = deposit.Id;
        using var mismatchResponse = await client.PostJsonAsync($"{ApiRoutes.Orders}/{order.Id}/deposit", mismatch, options);
        await mismatchResponse.ReadProblemAsync(HttpStatusCode.Conflict, "DUPLICATE_ID_MISMATCH", options);
        using var secondResponse = await client.PostJsonAsync($"{ApiRoutes.Orders}/{order.Id}/deposit", DepositRequest(shiftId, TestData.MainTerminal1Id, TestData.CardPaymentMethodId, 100m), options);
        await secondResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "ORDER_DEPOSIT_INVALID", options);

        // Act / Assert: 前受金があるうちはキャンセルできない。現金の前受金は予想現金に入る
        using var cancelHeldResponse = await client.PostJsonAsync($"{ApiRoutes.Orders}/{order.Id}/cancel", new OrderCancelRequest(), options);
        await cancelHeldResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "ORDER_DEPOSIT_INVALID", options);
        var summary = await client.GetJsonAsync<ShiftSummaryResponse>($"{ApiRoutes.Shifts}/{shiftId}/summary", options);
        Assert.Equal((500m, 0m, 500m), (summary.Cash.DepositCashIn, summary.Cash.DepositCashOut, summary.Cash.ExpectedCash));

        // Act / Assert: 会計は前受金の全額を充てる (充てないと 422)。完了した受注の前受金は 0
        using var withoutDepositResponse = await PostSaleAsync(client, shiftId, "S001-01-000301", order.Id, 0m);
        await withoutDepositResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "PAYMENT_MISMATCH", options);
        using var saleResponse = await PostSaleAsync(client, shiftId, "S001-01-000302", order.Id, 500m);
        var sale = await saleResponse.ReadAsAsync<TransactionResponseItem>(HttpStatusCode.Created, options);
        Assert.Equal(500m, sale.Payments.Single(static x => x.Kind == PaymentKind.Deposit).Amount);
        var completed = await client.GetJsonAsync<OrderResponseItem>($"{ApiRoutes.Orders}/{order.Id}", options);
        Assert.Equal((OrderStatus.Completed, 0m), (completed.Status, completed.DepositAmount));
        summary = await client.GetJsonAsync<ShiftSummaryResponse>($"{ApiRoutes.Shifts}/{shiftId}/summary", options);
        Assert.Equal((1500m, 500m, 2000m), (summary.Cash.CashSales, summary.Cash.DepositCashIn, summary.Cash.ExpectedCash));

        // Act / Assert: 会計を取り消すと引き渡し待ちに戻り、前受金も戻る
        using var voidResponse = await client.PostJsonAsync($"{ApiRoutes.Transactions}/{sale.Id}/void", new TransactionVoidRequest { StaffId = TestData.ManagerStaffId, Reason = "受注の取り違え", VoidedAt = Now.AddMinutes(30) }, options);
        await voidResponse.ReadAsAsync<TransactionResponseItem>(HttpStatusCode.OK, options);
        var reopened = await client.GetJsonAsync<OrderResponseItem>($"{ApiRoutes.Orders}/{order.Id}", options);
        Assert.Equal((OrderStatus.Arrived, 500m), (reopened.Status, reopened.DepositAmount));

        // Act / Assert: 返金は受け取った方法で全額。返したらキャンセルできる
        var refund = RefundRequest(shiftId, TestData.MainTerminal1Id);
        using var refundResponse = await client.PostJsonAsync($"{ApiRoutes.Orders}/{order.Id}/deposit/refund", refund, options);
        var refunded = await refundResponse.ReadAsAsync<OrderResponseItem>(HttpStatusCode.Created, options);
        Assert.Equal(0m, refunded.DepositAmount);
        var returned = refunded.Deposits.Single(static x => x.Type == OrderDepositType.Refund);
        Assert.Equal((TestData.CashPaymentMethodId, PaymentKind.Cash, 500m), (returned.PaymentMethodId, returned.Kind, returned.Amount));
        using var refundResendResponse = await client.PostJsonAsync($"{ApiRoutes.Orders}/{order.Id}/deposit/refund", refund, options);
        await refundResendResponse.ReadAsAsync<OrderResponseItem>(HttpStatusCode.OK, options);
        using var refundAgainResponse = await client.PostJsonAsync($"{ApiRoutes.Orders}/{order.Id}/deposit/refund", RefundRequest(shiftId, TestData.MainTerminal1Id), options);
        await refundAgainResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "ORDER_DEPOSIT_INVALID", options);
        using var cancelResponse = await client.PostJsonAsync($"{ApiRoutes.Orders}/{order.Id}/cancel", new OrderCancelRequest { Reason = "お客様都合" }, options);
        Assert.Equal(OrderStatus.Cancelled, (await cancelResponse.ReadAsAsync<OrderResponseItem>(HttpStatusCode.OK, options)).Status);
        using var depositCancelledResponse = await client.PostJsonAsync($"{ApiRoutes.Orders}/{order.Id}/deposit", DepositRequest(shiftId, TestData.MainTerminal1Id, TestData.CashPaymentMethodId, 100m), options);
        await depositCancelledResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "ORDER_STATUS_INVALID", options);

        // Act / Assert: 精算で前受金の現金を確定する (取り消した会計は入らない)
        using var closeResponse = await client.PostJsonAsync($"{ApiRoutes.Shifts}/{shiftId}/close", new ShiftCloseRequest { ClosedAt = Now.AddHours(8), ClosedByStaffId = TestData.MainCashierStaffId, ActualCash = 0m }, options);
        var closed = await closeResponse.ReadAsAsync<ShiftResponseItem>(HttpStatusCode.OK, options);
        Assert.Equal((0m, 0m), (closed.ExpectedCash, closed.Difference));
        Assert.Equal((500m, 500m), (closed.Totals.DepositCashIn, closed.Totals.DepositCashOut));
    }

    // 金額・支払方法・シフト・店舗の誤りは受け付けない。前受金の支払方法は 1 件だけ有効にできる
    [Fact]
    public async Task InvalidDepositIsRejected()
    {
        // Arrange
        var client = await factory.CreateAdminClientAsync();
        var shiftId = await OpenShiftAsync(client, TestData.MainStoreId, TestData.MainTerminal2Id);
        var branchShiftId = await OpenShiftAsync(client, TestData.BranchStoreId, TestData.BranchTerminalId);
        var order = await CreateOrderAsync(client);
        var url = $"{ApiRoutes.Orders}/{order.Id}/deposit";

        // Act
        using var zeroResponse = await client.PostJsonAsync(url, DepositRequest(shiftId, TestData.MainTerminal2Id, TestData.CashPaymentMethodId, 0m), options);
        using var overResponse = await client.PostJsonAsync(url, DepositRequest(shiftId, TestData.MainTerminal2Id, TestData.CashPaymentMethodId, order.Total + 1m), options);
        using var pointsResponse = await client.PostJsonAsync(url, DepositRequest(shiftId, TestData.MainTerminal2Id, TestData.PointsPaymentMethodId, 100m), options);
        using var unknownShiftResponse = await client.PostJsonAsync(url, DepositRequest(Guid.NewGuid(), TestData.MainTerminal2Id, TestData.CashPaymentMethodId, 100m), options);
        using var otherTerminalResponse = await client.PostJsonAsync(url, DepositRequest(shiftId, TestData.MainTerminal1Id, TestData.CashPaymentMethodId, 100m), options);
        using var otherStoreResponse = await client.PostJsonAsync(url, DepositRequest(branchShiftId, TestData.BranchTerminalId, TestData.CashPaymentMethodId, 100m), options);
        using var missingOrderResponse = await client.PostJsonAsync($"{ApiRoutes.Orders}/{Guid.NewGuid()}/deposit", DepositRequest(shiftId, TestData.MainTerminal2Id, TestData.CashPaymentMethodId, 100m), options);
        using var secondMethodResponse = await client.PostJsonAsync(ApiRoutes.PaymentMethods, new PaymentMethodCreateRequest { Code = "DEPOSIT2", Name = "前受金 2", Kind = PaymentKind.Deposit, IsActive = true, SortOrder = 99 }, options);

        // Assert
        await zeroResponse.ReadProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_ERROR", options);
        await overResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "ORDER_DEPOSIT_INVALID", options);
        await pointsResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "ORDER_DEPOSIT_INVALID", options);
        await unknownShiftResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "SHIFT_NOT_FOUND", options);
        await otherTerminalResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "SHIFT_TERMINAL_MISMATCH", options);
        await otherStoreResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "ORDER_NOT_FOUND", options);
        await missingOrderResponse.ReadProblemAsync(HttpStatusCode.NotFound, "NOT_FOUND", options);
        await secondMethodResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "VALIDATION_ERROR", options);
        Assert.Empty((await client.GetJsonAsync<OrderResponseItem>($"{ApiRoutes.Orders}/{order.Id}", options)).Deposits);
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private static OrderDepositRequest DepositRequest(Guid shiftId, Guid terminalId, Guid paymentMethodId, decimal amount) => new()
    {
        Id = Guid.NewGuid(),
        ShiftId = shiftId,
        TerminalId = terminalId,
        StaffId = TestData.MainCashierStaffId,
        PaymentMethodId = paymentMethodId,
        Amount = amount,
        OccurredAt = Now.AddMinutes(5)
    };

    private static OrderDepositRefundRequest RefundRequest(Guid shiftId, Guid terminalId) => new()
    {
        Id = Guid.NewGuid(),
        ShiftId = shiftId,
        TerminalId = terminalId,
        StaffId = TestData.MainCashierStaffId,
        OccurredAt = Now.AddHours(1)
    };

    // 取り置き (引き渡し待ちから始まる) の SD カード 2 枚 = 4,000 円
    private async Task<OrderResponseItem> CreateOrderAsync(HttpClient client)
    {
        var request = new OrderCreateRequest
        {
            Id = Guid.NewGuid(),
            StoreId = TestData.MainStoreId,
            TerminalId = TestData.MainTerminal1Id,
            StaffId = TestData.MainCashierStaffId,
            CustomerName = "前受 太郎",
            Type = OrderType.Hold,
            OrderedAt = Now,
            Lines =
            [
                new OrderCreateRequestLine { Id = Guid.NewGuid(), LineNo = 1, ProductId = TestData.SdCardProductId, ProductCode = "SD-64", ProductName = "SD カード 64GB", Quantity = 2m, UnitPrice = 2000m }
            ]
        };
        using var response = await client.PostJsonAsync(ApiRoutes.Orders, request, options);
        return await response.ReadAsAsync<OrderResponseItem>(HttpStatusCode.Created, options);
    }

    private async Task<Guid> OpenShiftAsync(HttpClient client, Guid storeId, Guid terminalId)
    {
        var request = new ShiftOpenRequest { Id = Guid.NewGuid(), StoreId = storeId, TerminalId = terminalId, BusinessDate = BusinessDate, OpenedAt = Now, OpenedByStaffId = TestData.AdminStaffId, OpeningCash = 0m };
        using var response = await client.PostJsonAsync(ApiRoutes.Shifts, request, options);
        return (await response.ReadAsAsync<ShiftResponseItem>(HttpStatusCode.Created, options)).Id;
    }

    // SD カード 2,000 円 (内税 10%、ポイントなし) を、前受金 (deposit) と残りの現金で
    private async Task<HttpResponseMessage> PostSaleAsync(HttpClient client, Guid shiftId, string receiptNo, Guid orderId, decimal deposit)
    {
        var payments = new List<TransactionCreateRequestPayment>();
        if (deposit > 0)
        {
            payments.Add(new TransactionCreateRequestPayment { Id = Guid.NewGuid(), SeqNo = 1, PaymentMethodId = TestData.DepositPaymentMethodId, Kind = PaymentKind.Deposit, Amount = deposit, TenderedAmount = deposit });
        }

        payments.Add(new TransactionCreateRequestPayment { Id = Guid.NewGuid(), SeqNo = payments.Count + 1, PaymentMethodId = TestData.CashPaymentMethodId, Kind = PaymentKind.Cash, Amount = 2000m - deposit, TenderedAmount = 2000m - deposit });
        var sale = new TransactionCreateRequest
        {
            Id = Guid.NewGuid(),
            Type = TransactionType.Sale,
            Status = TransactionStatus.Completed,
            StoreId = TestData.MainStoreId,
            TerminalId = TestData.MainTerminal1Id,
            StaffId = TestData.MainCashierStaffId,
            ShiftId = shiftId,
            ReceiptNo = receiptNo,
            BusinessDate = BusinessDate,
            TransactedAt = Now.AddMinutes(10),
            OrderId = orderId,
            Lines =
            [
                new TransactionCreateRequestLine { Id = Guid.NewGuid(), LineNo = 1, ProductId = TestData.SdCardProductId, ProductCode = "SD-64", ProductName = "SD カード 64GB", CategoryId = TestData.AccessoryCategoryId, Kind = ProductKind.Goods, ListPrice = 2000m, UnitPrice = 2000m, Quantity = 1m, TaxRateId = TestData.StandardTaxRateId, TaxRate = 0.10m, TaxIncluded = true, PointRate = 0m }
            ],
            Payments = payments
        };
        using var calculateResponse = await client.PostJsonAsync($"{ApiRoutes.Transactions}/calculate", TransactionRequests.ToCalculateRequest(sale), options);
        TransactionRequests.Apply(sale, await calculateResponse.ReadAsAsync<TransactionCalculateResponse>(HttpStatusCode.OK, options));
        return await client.PostJsonAsync(ApiRoutes.Transactions, sale, options);
    }
}
