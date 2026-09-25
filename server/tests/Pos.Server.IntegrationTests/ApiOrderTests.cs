namespace Pos.Server;

using System.Globalization;
using System.Text.Json;

using Pos.Contract.Customers;
using Pos.Contract.Orders;
using Pos.Contract.Shifts;
using Pos.Contract.Transactions;
using Pos.Server.Host.Endpoints;

// 受注: 取り寄せを登録 → 変更 → 入荷前の会計は 422 → 入荷 → 会計で完了 → 取消で引き渡し待ちに戻る → キャンセル。取り置きは引き渡し待ちから始まる
public sealed class ApiOrderTests : IClassFixture<TestApplicationFactory>
{
    private static readonly DateTime Now = new(2026, 9, 13, 2, 0, 0, DateTimeKind.Utc);

    private static readonly DateOnly BusinessDate = new(2026, 9, 13);

    private static readonly Guid AccessoryCategoryId = new("00000000-0000-0000-0004-000000000017");

    private readonly TestApplicationFactory factory;

    private readonly JsonSerializerOptions options;

    public ApiOrderTests(TestApplicationFactory factory)
    {
        this.factory = factory;
        options = factory.JsonOptions();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // 取り寄せの状態遷移と会計・取消との紐付け
    [Fact]
    public async Task BackOrderArrivesCompletesByCheckoutAndReopensByVoid()
    {
        // Arrange
        var client = await factory.CreateAdminClientAsync();
        var customer = await client.GetJsonAsync<CustomerResponseItem>($"{ApiRoutes.Customers}/{TestData.Customer1Id}", options);
        var shiftId = await OpenShiftAsync(client);
        var create = CreateOrderRequest(OrderType.BackOrder, TestData.Customer1Id, null, null);

        // Act / Assert: 登録 (会員の名前・電話を使う)、再送は 200、同じ id で内容違いは 409
        using var createResponse = await client.PostJsonAsync(ApiRoutes.Orders, create, options);
        var order = await createResponse.ReadAsAsync<OrderResponseItem>(HttpStatusCode.Created, options);
        Assert.StartsWith("S001-O-", order.OrderNo, StringComparison.Ordinal);
        Assert.Equal(OrderStatus.Ordered, order.Status);
        Assert.Equal(customer.Name, order.CustomerName);
        Assert.Equal(customer.Phone, order.Phone);
        Assert.Equal(80000m, order.Total);
        Assert.Null(order.ArrivedAt);
        using var resendResponse = await client.PostJsonAsync(ApiRoutes.Orders, create, options);
        Assert.Equal(order.OrderNo, (await resendResponse.ReadAsAsync<OrderResponseItem>(HttpStatusCode.OK, options)).OrderNo);
        var mismatch = CreateOrderRequest(OrderType.Hold, TestData.Customer1Id, null, null);
        mismatch.Id = create.Id;
        using var mismatchResponse = await client.PostJsonAsync(ApiRoutes.Orders, mismatch, options);
        await mismatchResponse.ReadProblemAsync(HttpStatusCode.Conflict, "DUPLICATE_ID_MISMATCH", options);

        // Act / Assert: 変更 (明細を置き換える)。古い版は 409
        var update = new OrderUpdateRequest
        {
            CustomerId = TestData.Customer1Id,
            Phone = "090-1111-2222",
            RequestedDate = BusinessDate.AddDays(7),
            Note = "入荷したら電話",
            Lines =
            [
                new OrderUpdateRequestLine { Id = Guid.NewGuid(), LineNo = 1, ProductId = TestData.CameraProductId, ProductCode = "CAM-X100", ProductName = "デジタルカメラ X-100", Quantity = 1m, UnitPrice = 80000m },
                new OrderUpdateRequestLine { Id = Guid.NewGuid(), LineNo = 2, ProductId = TestData.SdCardProductId, ProductCode = "SD-64", ProductName = "SD カード 64GB", Quantity = 2m, UnitPrice = 2000m }
            ],
            Version = order.Version
        };
        using var updateResponse = await client.PutJsonAsync($"{ApiRoutes.Orders}/{order.Id}", update, options);
        var updated = await updateResponse.ReadAsAsync<OrderResponseItem>(HttpStatusCode.OK, options);
        Assert.Equal(84000m, updated.Total);
        Assert.Equal(2, updated.Lines.Count);
        Assert.Equal("090-1111-2222", updated.Phone);
        Assert.Equal(order.Version + 1, updated.Version);
        using var staleResponse = await client.PutJsonAsync($"{ApiRoutes.Orders}/{order.Id}", update, options);
        await staleResponse.ReadProblemAsync(HttpStatusCode.Conflict, "VERSION_MISMATCH", options);

        // Act / Assert: 入荷前の会計は 422、入荷後は会計で完了になる
        using var notReadyResponse = await PostSaleAsync(client, shiftId, "S001-01-000201", order.Id);
        await notReadyResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "ORDER_NOT_READY", options);
        using var arriveResponse = await client.PostAsync(new Uri($"{ApiRoutes.Orders}/{order.Id}/arrive", UriKind.Relative), null, Token);
        var arrived = await arriveResponse.ReadAsAsync<OrderResponseItem>(HttpStatusCode.OK, options);
        Assert.Equal(OrderStatus.Arrived, arrived.Status);
        Assert.NotNull(arrived.ArrivedAt);
        using var arriveAgainResponse = await client.PostAsync(new Uri($"{ApiRoutes.Orders}/{order.Id}/arrive", UriKind.Relative), null, Token);
        await arriveAgainResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "ORDER_STATUS_INVALID", options);
        using var saleResponse = await PostSaleAsync(client, shiftId, "S001-01-000202", order.Id);
        var sale = await saleResponse.ReadAsAsync<TransactionResponseItem>(HttpStatusCode.Created, options);
        Assert.Equal(order.Id, sale.OrderId);
        Assert.Equal(order.OrderNo, sale.OrderNo);
        var completed = await client.GetJsonAsync<OrderResponseItem>($"{ApiRoutes.Orders}/{order.Id}", options);
        Assert.Equal(OrderStatus.Completed, completed.Status);
        Assert.Equal(sale.Id, completed.TransactionId);
        Assert.Equal(order.OrderNo, (await client.GetJsonAsync<TransactionResponseItem>($"{ApiRoutes.Transactions}/{sale.Id}", options)).OrderNo);

        // Act / Assert: 会計した取引を取り消すと引き渡し待ちに戻り、キャンセルできる
        using var voidResponse = await client.PostJsonAsync($"{ApiRoutes.Transactions}/{sale.Id}/void", new TransactionVoidRequest { StaffId = TestData.ManagerStaffId, Reason = "受注の取り違え", VoidedAt = Now.AddMinutes(30) }, options);
        await voidResponse.ReadAsAsync<TransactionResponseItem>(HttpStatusCode.OK, options);
        var reopened = await client.GetJsonAsync<OrderResponseItem>($"{ApiRoutes.Orders}/{order.Id}", options);
        Assert.Equal(OrderStatus.Arrived, reopened.Status);
        Assert.Null(reopened.TransactionId);
        using var cancelResponse = await client.PostJsonAsync($"{ApiRoutes.Orders}/{order.Id}/cancel", new OrderCancelRequest { Reason = "お客様都合" }, options);
        var cancelled = await cancelResponse.ReadAsAsync<OrderResponseItem>(HttpStatusCode.OK, options);
        Assert.Equal(OrderStatus.Cancelled, cancelled.Status);
        Assert.Equal("お客様都合", cancelled.CancelReason);
        using var cancelAgainResponse = await client.PostJsonAsync($"{ApiRoutes.Orders}/{order.Id}/cancel", new OrderCancelRequest(), options);
        await cancelAgainResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "ORDER_STATUS_INVALID", options);
        update.Version = cancelled.Version;
        using var updateCancelledResponse = await client.PutJsonAsync($"{ApiRoutes.Orders}/{order.Id}", update, options);
        await updateCancelledResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "ORDER_STATUS_INVALID", options);
        using var missingOrderResponse = await PostSaleAsync(client, shiftId, "S001-01-000203", Guid.NewGuid());
        await missingOrderResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "ORDER_NOT_FOUND", options);
    }

    // 取り置きは引き渡し待ちから始まり、受注番号は店舗ごとの連番。入力の誤りは 400 / 422
    [Fact]
    public async Task HoldStartsArrivedAndListFilters()
    {
        // Arrange
        var client = await factory.CreateAdminClientAsync();
        var name = $"T{Guid.NewGuid():N}"[..10];
        var first = CreateOrderRequest(OrderType.Hold, null, name, "03-0000-0000");
        var second = CreateOrderRequest(OrderType.BackOrder, null, name, null);

        // Act
        using var firstResponse = await client.PostJsonAsync(ApiRoutes.Orders, first, options);
        using var secondResponse = await client.PostJsonAsync(ApiRoutes.Orders, second, options);
        using var noNameResponse = await client.PostJsonAsync(ApiRoutes.Orders, CreateOrderRequest(OrderType.Hold, null, null, null), options);
        var unknownProduct = CreateOrderRequest(OrderType.Hold, null, name, null);
        unknownProduct.Lines[0].ProductId = Guid.NewGuid();
        using var unknownProductResponse = await client.PostJsonAsync(ApiRoutes.Orders, unknownProduct, options);

        // Assert
        var hold = await firstResponse.ReadAsAsync<OrderResponseItem>(HttpStatusCode.Created, options);
        var backOrder = await secondResponse.ReadAsAsync<OrderResponseItem>(HttpStatusCode.Created, options);
        Assert.Equal(OrderStatus.Arrived, hold.Status);
        Assert.NotNull(hold.ArrivedAt);
        Assert.Equal(OrderStatus.Ordered, backOrder.Status);
        Assert.Equal(int.Parse(hold.OrderNo[^6..], CultureInfo.InvariantCulture) + 1, int.Parse(backOrder.OrderNo[^6..], CultureInfo.InvariantCulture));
        await noNameResponse.ReadProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_ERROR", options);
        await unknownProductResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "PRODUCT_NOT_FOUND", options);
        var byName = await client.GetJsonAsync<OrderResponse>($"{ApiRoutes.Orders}?keyword={name}", options);
        Assert.Equal(2, byName.Total);
        var arrived = await client.GetJsonAsync<OrderResponse>($"{ApiRoutes.Orders}?keyword={name}&status=Arrived", options);
        Assert.Equal(hold.Id, Assert.Single(arrived.Items).Id);
        var byNo = await client.GetJsonAsync<OrderResponse>($"{ApiRoutes.Orders}?keyword={backOrder.OrderNo}&open=true", options);
        Assert.Equal(backOrder.Id, Assert.Single(byNo.Items).Id);
        Assert.Equal(0, (await client.GetJsonAsync<OrderResponse>($"{ApiRoutes.Orders}?keyword={name}&type=Hold&status=Cancelled", options)).Total);
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private static OrderCreateRequest CreateOrderRequest(OrderType type, Guid? customerId, string? customerName, string? phone) => new()
    {
        Id = Guid.NewGuid(),
        StoreId = TestData.MainStoreId,
        TerminalId = TestData.MainTerminal1Id,
        StaffId = TestData.MainCashierStaffId,
        CustomerId = customerId,
        CustomerName = customerName,
        Phone = phone,
        Type = type,
        RequestedDate = BusinessDate.AddDays(5),
        OrderedAt = Now,
        Lines =
        [
            new OrderCreateRequestLine { Id = Guid.NewGuid(), LineNo = 1, ProductId = TestData.CameraProductId, ProductCode = "CAM-X100", ProductName = "デジタルカメラ X-100", Quantity = 1m, UnitPrice = 80000m }
        ]
    };

    private async Task<Guid> OpenShiftAsync(HttpClient client)
    {
        var request = new ShiftOpenRequest { Id = Guid.NewGuid(), StoreId = TestData.MainStoreId, TerminalId = TestData.MainTerminal1Id, BusinessDate = BusinessDate, OpenedAt = Now, OpenedByStaffId = TestData.MainCashierStaffId, OpeningCash = 0m };
        using var response = await client.PostJsonAsync(ApiRoutes.Shifts, request, options);
        return (await response.ReadAsAsync<ShiftResponseItem>(HttpStatusCode.Created, options)).Id;
    }

    // SD カード 2,000 円 (内税 10%、ポイントなし) を現金で。受注の明細と同じでなくてよい
    private async Task<HttpResponseMessage> PostSaleAsync(HttpClient client, Guid shiftId, string receiptNo, Guid orderId)
    {
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
                new TransactionCreateRequestLine { Id = Guid.NewGuid(), LineNo = 1, ProductId = TestData.SdCardProductId, ProductCode = "SD-64", ProductName = "SD カード 64GB", CategoryId = AccessoryCategoryId, Kind = ProductKind.Goods, ListPrice = 2000m, UnitPrice = 2000m, Quantity = 1m, TaxRateId = TestData.StandardTaxRateId, TaxRate = 0.10m, TaxIncluded = true, PointRate = 0m }
            ],
            Payments =
            [
                new TransactionCreateRequestPayment { Id = Guid.NewGuid(), SeqNo = 1, PaymentMethodId = TestData.CashPaymentMethodId, Kind = PaymentKind.Cash, Amount = 2000m, TenderedAmount = 2000m }
            ]
        };
        using var calculateResponse = await client.PostJsonAsync($"{ApiRoutes.Transactions}/calculate", TransactionRequests.ToCalculateRequest(sale), options);
        TransactionRequests.Apply(sale, await calculateResponse.ReadAsAsync<TransactionCalculateResponse>(HttpStatusCode.OK, options));
        return await client.PostJsonAsync(ApiRoutes.Transactions, sale, options);
    }
}
