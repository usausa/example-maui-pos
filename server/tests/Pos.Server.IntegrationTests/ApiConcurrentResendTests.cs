namespace Pos.Server;

using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;

using Pos.Contract.Inventory;
using Pos.Contract.Orders;
using Pos.Contract.Shifts;
using Pos.Contract.Transactions;
using Pos.Server.Host.Endpoints;

using Smart.Data;

// 同時に届いた登録: 書き込みを止めた間に送り、書く前の読み取り (同じ Id、レシート番号など) を終えてから書くように重ねる。
// 先に書いた 1 件のほかは、一意の制約の違反か、条件に合わずに書けなかったこと (前受金、精算) を受けて読み直した結果になる
public sealed class ApiConcurrentResendTests : IClassFixture<TestApplicationFactory>
{
    private const int Copies = 3;

    private static readonly TimeSpan HoldTime = TimeSpan.FromMilliseconds(500);

    private static readonly DateTime Now = new(2026, 9, 26, 1, 0, 0, DateTimeKind.Utc);

    private static readonly DateOnly BusinessDate = new(2026, 9, 26);

    private readonly TestApplicationFactory factory;

    private readonly JsonSerializerOptions options;

    public ApiConcurrentResendTests(TestApplicationFactory factory)
    {
        this.factory = factory;
        options = factory.JsonOptions();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // シフトの開設・入出金・取引・精算の同じ再送が同時に届いても 1 回だけ記録し、取引の在庫も 1 回だけ動かす。
    // 別の取引が同じレシート番号で同時に届けば、1 件のほかはレシート番号の重複にする
    [Fact]
    public async Task ShiftWritesAndTransactionsSentAtOnceAreRegisteredOnce()
    {
        // Arrange
        var client = await factory.CreateAdminClientAsync();
        var open = OpenRequest(TestData.MainStoreId, TestData.MainTerminal1Id);
        var cashEvent = new ShiftCashEventRequest { Id = Guid.NewGuid(), Type = CashEventType.PaidIn, Amount = 5000m, Reason = "両替", StaffId = TestData.MainCashierStaffId, OccurredAt = Now.AddMinutes(5) };
        var cashEventUrl = $"{ApiRoutes.Shifts}/{open.Id}/cash-events";
        var before = await QuantityAsync(client, TestData.MainStoreId);

        // Act / Assert: 開設
        await AssertRegisteredOnceAsync<ShiftListResponseItem>(
            await SendAtOnceAsync(Enumerable.Repeat(open, Copies), x => client.PostJsonAsync(ApiRoutes.Shifts, x, options)),
            static x => x.Id,
            open.Id);

        // Act / Assert: 入出金
        await AssertRegisteredOnceAsync<ShiftCashEventListResponseItem>(
            await SendAtOnceAsync(Enumerable.Repeat(cashEvent, Copies), x => client.PostJsonAsync(cashEventUrl, x, options)),
            static x => x.Id,
            cashEvent.Id);

        // Act / Assert: 販売
        var sale = await CreateSaleRequestAsync(client, open.Id, "S001-01-000001");
        await AssertRegisteredOnceAsync<TransactionListResponseItem>(
            await SendAtOnceAsync(Enumerable.Repeat(sale, Copies), x => client.PostJsonAsync(ApiRoutes.Transactions, x, options)),
            static x => x.Id,
            sale.Id);

        // Act / Assert: 同じレシート番号の別の取引
        var sameReceiptNo = new List<TransactionCreateRequest>();
        for (var i = 0; i < Copies; i++)
        {
            sameReceiptNo.Add(await CreateSaleRequestAsync(client, open.Id, "S001-01-000002"));
        }

        await AssertCreatedOnceAsync<TransactionListResponseItem>(
            await SendAtOnceAsync(sameReceiptNo, x => client.PostJsonAsync(ApiRoutes.Transactions, x, options)),
            HttpStatusCode.UnprocessableEntity,
            "DUPLICATE_RECEIPT_NO");

        // Act / Assert: 精算 (同じ実査金額の再送はどれも 200)
        var close = new ShiftCloseRequest { ClosedAt = Now.AddHours(8), ClosedByStaffId = TestData.MainCashierStaffId, ActualCash = 9000m, Denominations = [new ShiftCloseRequestDenomination { Denomination = 5000, Count = 1 }, new ShiftCloseRequestDenomination { Denomination = 1000, Count = 4 }] };
        foreach (var response in await SendAtOnceAsync(Enumerable.Repeat(close, Copies), x => client.PostJsonAsync($"{ApiRoutes.Shifts}/{open.Id}/close", x, options)))
        {
            using (response)
            {
                Assert.Equal(ShiftStatus.Closed, (await response.ReadAsAsync<ShiftListResponseItem>(HttpStatusCode.OK, options)).Status);
            }
        }

        // Act / Assert: 入金 1 件と販売 2 件だけが記録されて在庫は 2 個減り、精算の金種も 1 回分 (予想現金は 0 + 5,000 + 2,000 × 2)
        var shift = await client.GetJsonAsync<ShiftListResponseItem>($"{ApiRoutes.Shifts}/{open.Id}", options);
        Assert.Equal((5000m, 2, 9000m, 2), (shift.Totals.PaidIn, shift.Totals.SalesCount, shift.ExpectedCash, shift.Denominations.Count));
        Assert.Equal(1, (await client.GetJsonAsync<ShiftCashEventListResponse>(cashEventUrl, options)).Total);
        Assert.Equal(before - 2m, await QuantityAsync(client, TestData.MainStoreId));
    }

    // 受注と前受金の受取・返金の同じ Id の再送が同時に届いても 1 件だけ登録し、受注番号も前受金も 1 回分にする。
    // 同じ Id の受取が別の受注へ同時に届けば、1 件のほかは内容違いにする
    [Fact]
    public async Task OrderAndDepositsSentAtOnceAreRegisteredOnce()
    {
        // Arrange
        var client = await factory.CreateAdminClientAsync();
        var open = OpenRequest(TestData.BranchStoreId, TestData.BranchTerminalId);
        using var openResponse = await client.PostJsonAsync(ApiRoutes.Shifts, open, options);
        await openResponse.ReadAsAsync<ShiftListResponseItem>(HttpStatusCode.Created, options);
        using var otherResponse = await client.PostJsonAsync(ApiRoutes.Orders, OrderRequest(), options);
        var other = await otherResponse.ReadAsAsync<OrderListResponseItem>(HttpStatusCode.Created, options);
        var order = OrderRequest();
        var deposit = DepositRequest(open.Id);
        var refund = new OrderDepositRefundRequest { Id = Guid.NewGuid(), ShiftId = open.Id, TerminalId = TestData.BranchTerminalId, StaffId = TestData.BranchCashierStaffId, OccurredAt = Now.AddMinutes(10) };

        // Act / Assert: 受注
        var orders = await AssertRegisteredOnceAsync<OrderListResponseItem>(
            await SendAtOnceAsync(Enumerable.Repeat(order, Copies), x => client.PostJsonAsync(ApiRoutes.Orders, x, options)),
            static x => x.Id,
            order.Id);
        Assert.Single(orders.Select(static x => x.OrderNo).Distinct());

        // Act / Assert: 前受金の受取
        var deposited = await AssertRegisteredOnceAsync<OrderListResponseItem>(
            await SendAtOnceAsync(Enumerable.Repeat(deposit, Copies), x => client.PostJsonAsync($"{ApiRoutes.Orders}/{order.Id}/deposit", x, options)),
            static x => x.Id,
            order.Id);
        Assert.All(deposited, static x => Assert.Equal(1000m, x.DepositAmount));

        // Act / Assert: 前受金の返金
        var refunded = await AssertRegisteredOnceAsync<OrderListResponseItem>(
            await SendAtOnceAsync(Enumerable.Repeat(refund, Copies), x => client.PostJsonAsync($"{ApiRoutes.Orders}/{order.Id}/deposit/refund", x, options)),
            static x => x.Id,
            order.Id);
        Assert.All(refunded, static x => Assert.Equal(0m, x.DepositAmount));

        // Act / Assert: 受取と返金が 1 件ずつ記録され、シフトの現金にも 1 回ずつ入る
        var current = await client.GetJsonAsync<OrderListResponseItem>($"{ApiRoutes.Orders}/{order.Id}", options);
        Assert.Equal([(OrderDepositType.Receive, deposit.Id), (OrderDepositType.Refund, refund.Id)], current.Deposits.Select(static x => (x.Type, x.Id)).OrderBy(static x => x.Type));
        var shift = await client.GetJsonAsync<ShiftListResponseItem>($"{ApiRoutes.Shifts}/{open.Id}", options);
        Assert.Equal((1000m, 1000m), (shift.Totals.DepositCashIn, shift.Totals.DepositCashOut));

        // Act / Assert: 別の受注へ同じ Id の受取 (先の受取は自分の受注の残りしか変えないので、後の 1 件は条件に合ったまま一意の制約の違反になる)
        var again = DepositRequest(open.Id);
        await AssertCreatedOnceAsync<OrderListResponseItem>(
            await SendAtOnceAsync([order.Id, other.Id], x => client.PostJsonAsync($"{ApiRoutes.Orders}/{x}/deposit", again, options)),
            HttpStatusCode.Conflict,
            "DUPLICATE_ID_MISMATCH");
    }

    // 在庫の変動の同じ Id の再送が同時に届いても在庫は 1 回だけ動き、先の 1 件のほかは Duplicate を返す
    [Fact]
    public async Task InventoryChangesSentAtOnceAreAppliedOnce()
    {
        // Arrange
        var client = await factory.CreateAdminClientAsync();
        var before = await QuantityAsync(client, TestData.BranchStoreId);
        var change = new InventoryChangesRequestChange { Id = Guid.NewGuid(), StoreId = TestData.BranchStoreId, ProductId = TestData.SdCardProductId, Type = InventoryChangeType.Adjustment, Quantity = 3m, Reason = "計上漏れ", StaffId = TestData.BranchCashierStaffId, OccurredAt = Now };
        var request = new InventoryChangesRequest { Changes = [change] };

        // Act
        var responses = await SendAtOnceAsync(Enumerable.Repeat(request, Copies), x => client.PostJsonAsync($"{ApiRoutes.Inventory}/changes", x, options));

        // Assert
        var results = new List<InventoryChangesResponseResult>();
        foreach (var response in responses)
        {
            using (response)
            {
                results.Add(Assert.Single((await response.ReadAsAsync<InventoryChangesResponse>(HttpStatusCode.OK, options)).Results));
            }
        }

        Assert.Single(results, static x => x.Status == InventoryChangeResultStatus.Created);
        Assert.All(results, x => Assert.Equal((change.Id, 3m, before + 3m), (x.Id, x.QuantityDelta, x.QuantityAfter)));
        Assert.Equal(before + 3m, await QuantityAsync(client, TestData.BranchStoreId));
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    // DB の書き込みを止めた間に要求をまとめて送り、読み取りを終えて書き込みで待つころに止めを外す
    // (それまでに読み取りを終えなかった要求は、順に届いたときの分岐を通るだけで、結果は変わらない)
    private async Task<HttpResponseMessage[]> SendAtOnceAsync<T>(IEnumerable<T> requests, Func<T, Task<HttpResponseMessage>> send)
    {
        await using var con = factory.Services.GetRequiredService<IDbProvider>().CreateConnection();
        await con.OpenAsync(Token);
        Task<HttpResponseMessage>[] tasks;
        await using (await con.BeginTransactionAsync(Token))
        {
            tasks = requests.Select(send).ToArray();
            await Task.Delay(HoldTime, Token);

            // 書き込めない間は、どの要求も終わらない
            Assert.DoesNotContain(tasks, static x => x.IsCompleted);
        }

        return await Task.WhenAll(tasks);
    }

    // 先に書いた 1 件が 201、ほかは再送として 200 で、どれも同じ登録を返す
    private async Task<List<T>> AssertRegisteredOnceAsync<T>(HttpResponseMessage[] responses, Func<T, Guid> idOf, Guid id)
    {
        var created = Assert.Single(responses, static x => x.StatusCode == HttpStatusCode.Created);
        var items = new List<T>();
        foreach (var response in responses)
        {
            using (response)
            {
                var item = await response.ReadAsAsync<T>(response == created ? HttpStatusCode.Created : HttpStatusCode.OK, options);
                Assert.Equal(id, idOf(item));
                items.Add(item);
            }
        }

        return items;
    }

    // 先に書いた 1 件が 201、ほかは同じエラーを返す
    private async Task<T> AssertCreatedOnceAsync<T>(HttpResponseMessage[] responses, HttpStatusCode status, string errorCode)
    {
        var created = Assert.Single(responses, static x => x.StatusCode == HttpStatusCode.Created);
        foreach (var response in responses.Where(x => x != created))
        {
            using (response)
            {
                await response.ReadProblemAsync(status, errorCode, options);
            }
        }

        using (created)
        {
            return await created.ReadAsAsync<T>(HttpStatusCode.Created, options);
        }
    }

    private static ShiftOpenRequest OpenRequest(Guid storeId, Guid terminalId) =>
        new() { Id = Guid.NewGuid(), StoreId = storeId, TerminalId = terminalId, BusinessDate = BusinessDate, OpenedAt = Now, OpenedByStaffId = TestData.AdminStaffId, OpeningCash = 0m };

    // 支店の取り置き (SD カード 2 枚 = 4,000 円)
    private static OrderCreateRequest OrderRequest() => new()
    {
        Id = Guid.NewGuid(),
        StoreId = TestData.BranchStoreId,
        TerminalId = TestData.BranchTerminalId,
        StaffId = TestData.BranchCashierStaffId,
        CustomerName = "同時 太郎",
        Type = OrderType.Hold,
        OrderedAt = Now,
        Lines =
        [
            new OrderCreateRequestLine { Id = Guid.NewGuid(), LineNo = 1, ProductId = TestData.SdCardProductId, ProductCode = "SD-64", ProductName = "SD カード 64GB", Quantity = 2m, UnitPrice = 2000m }
        ]
    };

    // 支店のシフトで現金 1,000 円を受け取る
    private static OrderDepositRequest DepositRequest(Guid shiftId) =>
        new() { Id = Guid.NewGuid(), ShiftId = shiftId, TerminalId = TestData.BranchTerminalId, StaffId = TestData.BranchCashierStaffId, PaymentMethodId = TestData.CashPaymentMethodId, Amount = 1000m, OccurredAt = Now.AddMinutes(5) };

    private async Task<decimal> QuantityAsync(HttpClient client, Guid storeId) =>
        (await client.GetJsonAsync<InventoryProductLevelsResponse>($"{ApiRoutes.Inventory}/{TestData.SdCardProductId}", options)).Levels.Single(x => x.StoreId == storeId).Quantity;

    // SD カード 1 枚 2,000 円を現金で
    private async Task<TransactionCreateRequest> CreateSaleRequestAsync(HttpClient client, Guid shiftId, string receiptNo)
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
            Lines =
            [
                new TransactionCreateRequestLine { Id = Guid.NewGuid(), LineNo = 1, ProductId = TestData.SdCardProductId, ProductCode = "SD-64", ProductName = "SD カード 64GB", CategoryId = TestData.AccessoryCategoryId, Kind = ProductKind.Goods, ListPrice = 2000m, UnitPrice = 2000m, Quantity = 1m, TaxRateId = TestData.StandardTaxRateId, TaxRate = 0.10m, TaxIncluded = true, PointRate = 0m }
            ],
            Payments =
            [
                new TransactionCreateRequestPayment { Id = Guid.NewGuid(), SeqNo = 1, PaymentMethodId = TestData.CashPaymentMethodId, Kind = PaymentKind.Cash, Amount = 2000m, TenderedAmount = 2000m }
            ]
        };
        using var calculateResponse = await client.PostJsonAsync($"{ApiRoutes.Transactions}/calculate", TransactionRequests.ToCalculateRequest(sale), options);
        TransactionRequests.Apply(sale, await calculateResponse.ReadAsAsync<TransactionCalculateResponse>(HttpStatusCode.OK, options));
        return sale;
    }
}
