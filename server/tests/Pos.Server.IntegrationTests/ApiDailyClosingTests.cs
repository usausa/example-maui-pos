namespace Pos.Server;

using System.Text.Json;

using Pos.Contract.DailyClosings;
using Pos.Contract.Reports;
using Pos.Contract.Shifts;
using Pos.Contract.Transactions;
using Pos.Server.Host.Endpoints;

// 日次締め: 未精算で 422 → 精算後に締め → 締め後の取消 422 → 締め後に届いた取引は警告付きで受理 → 解除 → 締め直しで取り込む
public sealed class ApiDailyClosingTests : IClassFixture<TestApplicationFactory>
{
    private static readonly DateTime Now = new(2026, 9, 12, 1, 0, 0, DateTimeKind.Utc);

    private static readonly Guid ServiceCategoryId = new("00000000-0000-0000-0004-000000000004");

    private readonly TestApplicationFactory factory;

    private readonly JsonSerializerOptions options;

    public ApiDailyClosingTests(TestApplicationFactory factory)
    {
        this.factory = factory;
        options = factory.JsonOptions();
    }

    // 締めの前後で取消・遅れて届いた取引の扱いが変わり、解除して締め直すと日計に取り込まれる
    [Fact]
    public async Task CloseBlocksVoidAndRecloseIncludesLateTransactions()
    {
        // Arrange
        var client = await factory.CreateAdminClientAsync();
        var date = new DateOnly(2026, 9, 12);
        var request = new DailyClosingCreateRequest { StoreId = TestData.MainStoreId, BusinessDate = date };
        var shiftId = await OpenShiftAsync(client, TestData.MainStoreId, TestData.MainTerminal1Id, date);
        await PostSaleAsync(client, TestData.MainStoreId, TestData.MainTerminal1Id, shiftId, date, "S001-01-000101");

        // Act / Assert: 未精算のシフトがあれば締められない (確認用の集計は取れる)
        using var openResponse = await client.PostJsonAsync(ApiRoutes.DailyClosings, request, options);
        await openResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "SHIFT_STILL_OPEN", options);
        var preview = await client.GetJsonAsync<DailyClosingSummaryResponse>($"{ApiRoutes.DailyClosings}/preview?storeId={TestData.MainStoreId}&businessDate=2026-09-12", options);
        Assert.Equal(DailyClosingStatus.Open, preview.DailyClosing.Status);
        Assert.Null(preview.DailyClosing.Id);
        Assert.Equal(1, preview.DailyClosing.OpenShiftCount);
        Assert.Equal(1, preview.DailyClosing.SalesCount);
        Assert.Equal(1100m, preview.DailyClosing.NetSales);

        // Act / Assert: シフトのない日は締めるものがない
        using var emptyResponse = await client.PostJsonAsync(ApiRoutes.DailyClosings, new DailyClosingCreateRequest { StoreId = TestData.MainStoreId, BusinessDate = date.AddDays(1) }, options);
        await emptyResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "SHIFT_NOT_FOUND", options);

        // Act / Assert: 精算後に締める。日計は売上集計と一致し、再度の締めは 409
        await CloseShiftAsync(client, shiftId, 1100m);
        using var closeResponse = await client.PostJsonAsync(ApiRoutes.DailyClosings, request, options);
        var closed = await closeResponse.ReadAsAsync<DailyClosingSummaryResponse>(HttpStatusCode.Created, options);
        var closingId = closed.DailyClosing.Id!.Value;
        Assert.Equal($"{ApiRoutes.DailyClosings}/{closingId}", closeResponse.Headers.Location?.OriginalString);
        Assert.Equal(DailyClosingStatus.Closed, closed.DailyClosing.Status);
        Assert.Equal(1, closed.DailyClosing.ShiftCount);
        Assert.Equal(0, closed.DailyClosing.OpenShiftCount);
        Assert.False(closed.DailyClosing.HasLateTransactions);
        Assert.NotNull(closed.DailyClosing.ClosedAt);
        Assert.Equal(1100m, Assert.Single(closed.ByPaymentMethod).SalesAmount);
        Assert.Equal(100m, Assert.Single(closed.ByTaxRate).TaxAmount);
        Assert.Equal(shiftId, Assert.Single(closed.Shifts).Id);
        var report = await client.GetJsonAsync<ReportSalesSummaryResponse>($"{ApiRoutes.Reports}/sales/summary?storeId={TestData.MainStoreId}&from=2026-09-12&to=2026-09-12&groupBy=day", options);
        var reportDay = Assert.Single(report.Rows);
        Assert.Equal(reportDay.TransactionCount, closed.DailyClosing.SalesCount);
        Assert.Equal(reportDay.NetSales, closed.DailyClosing.NetSales);
        Assert.Equal(reportDay.TaxTotal, closed.DailyClosing.TaxTotal);
        Assert.Equal(reportDay.CustomerCount, closed.DailyClosing.CustomerCount);
        using var againResponse = await client.PostJsonAsync(ApiRoutes.DailyClosings, request, options);
        await againResponse.ReadProblemAsync(HttpStatusCode.Conflict, "ALREADY_CLOSED", options);
        var list = await client.GetJsonAsync<DailyClosingResponse>($"{ApiRoutes.DailyClosings}?storeId={TestData.MainStoreId}&from=2026-09-12&to=2026-09-12", options);
        Assert.Equal(closingId, Assert.Single(list.Items).Id);
        Assert.Equal(0, (await client.GetJsonAsync<DailyClosingResponse>($"{ApiRoutes.DailyClosings}?storeId={TestData.MainStoreId}&from=2026-09-12&to=2026-09-12&status=Open", options)).Total);

        // Act / Assert: 締め後に開設したシフトの取引は警告付きで受理し、締めた日計はそのまま。取消は 422
        var lateShiftId = await OpenShiftAsync(client, TestData.MainStoreId, TestData.MainTerminal2Id, date);
        var late = await PostSaleAsync(client, TestData.MainStoreId, TestData.MainTerminal2Id, lateShiftId, date, "S001-02-000101");
        Assert.Equal("DAY_ALREADY_CLOSED", Assert.Single(late.Warnings).Code);
        var afterLate = await client.GetJsonAsync<DailyClosingSummaryResponse>($"{ApiRoutes.DailyClosings}/{closingId}", options);
        Assert.True(afterLate.DailyClosing.HasLateTransactions);
        Assert.Equal(1, afterLate.DailyClosing.SalesCount);
        Assert.Equal(1, afterLate.DailyClosing.OpenShiftCount);
        var voidRequest = new TransactionVoidRequest { StaffId = TestData.ManagerStaffId, Reason = "誤操作", VoidedAt = Now.AddMinutes(30) };
        using var voidResponse = await client.PostJsonAsync($"{ApiRoutes.Transactions}/{late.Id}/void", voidRequest, options);
        await voidResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "DAY_CLOSED", options);

        // Act / Assert: 解除すると未締めに戻り、締め直すと遅れて届いた取引も日計に入る
        using var reopenResponse = await client.DeleteUrlAsync($"{ApiRoutes.DailyClosings}/{closingId}");
        Assert.Equal(HttpStatusCode.NoContent, reopenResponse.StatusCode);
        using var missingResponse = await client.GetAsync(new Uri($"{ApiRoutes.DailyClosings}/{closingId}", UriKind.Relative), TestContext.Current.CancellationToken);
        await missingResponse.ReadProblemAsync(HttpStatusCode.NotFound, "NOT_FOUND", options);
        using var reopenAgainResponse = await client.DeleteUrlAsync($"{ApiRoutes.DailyClosings}/{closingId}");
        await reopenAgainResponse.ReadProblemAsync(HttpStatusCode.NotFound, "NOT_FOUND", options);
        await CloseShiftAsync(client, lateShiftId, 1100m);
        using var recloseResponse = await client.PostJsonAsync(ApiRoutes.DailyClosings, request, options);
        var reclosed = await recloseResponse.ReadAsAsync<DailyClosingSummaryResponse>(HttpStatusCode.Created, options);
        Assert.NotEqual(closingId, reclosed.DailyClosing.Id);
        Assert.Equal(2, reclosed.DailyClosing.ShiftCount);
        Assert.Equal(2, reclosed.DailyClosing.SalesCount);
        Assert.Equal(2200m, reclosed.DailyClosing.NetSales);
        Assert.False(reclosed.DailyClosing.HasLateTransactions);
        Assert.Equal(2, reclosed.Shifts.Count);
    }

    // 日をまたいだシフト (前日の営業日で開設し、翌営業日の取引を含む) は、取引の営業日の締めでも精算を待つ
    [Fact]
    public async Task CloseWaitsForShiftCarryingTransactionsOfTheDay()
    {
        // Arrange
        var client = await factory.CreateAdminClientAsync();
        var openedDate = new DateOnly(2026, 9, 20);
        var saleDate = openedDate.AddDays(1);
        var shiftId = await OpenShiftAsync(client, TestData.BranchStoreId, TestData.BranchTerminalId, openedDate);
        await PostSaleAsync(client, TestData.BranchStoreId, TestData.BranchTerminalId, shiftId, saleDate, "S002-01-000101");

        // Act
        using var openResponse = await client.PostJsonAsync(ApiRoutes.DailyClosings, new DailyClosingCreateRequest { StoreId = TestData.BranchStoreId, BusinessDate = saleDate }, options);
        await CloseShiftAsync(client, shiftId, 1100m);
        using var saleDayResponse = await client.PostJsonAsync(ApiRoutes.DailyClosings, new DailyClosingCreateRequest { StoreId = TestData.BranchStoreId, BusinessDate = saleDate }, options);
        using var openedDayResponse = await client.PostJsonAsync(ApiRoutes.DailyClosings, new DailyClosingCreateRequest { StoreId = TestData.BranchStoreId, BusinessDate = openedDate }, options);

        // Assert
        await openResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "SHIFT_STILL_OPEN", options);
        var saleDay = await saleDayResponse.ReadAsAsync<DailyClosingSummaryResponse>(HttpStatusCode.Created, options);
        Assert.Equal(1, saleDay.DailyClosing.ShiftCount);
        Assert.Equal(1, saleDay.DailyClosing.SalesCount);
        Assert.Equal(openedDate, Assert.Single(saleDay.Shifts).BusinessDate);
        var openedDay = await openedDayResponse.ReadAsAsync<DailyClosingSummaryResponse>(HttpStatusCode.Created, options);
        Assert.Equal(1, openedDay.DailyClosing.ShiftCount);
        Assert.Equal(0, openedDay.DailyClosing.SalesCount);
        var days = await client.GetJsonAsync<DailyClosingResponse>($"{ApiRoutes.DailyClosings}?storeId={TestData.BranchStoreId}&from=2026-09-20&to=2026-09-21", options);
        DateOnly[] expected = [saleDate, openedDate];
        Assert.Equal(expected, days.Items.Select(static x => x.BusinessDate).ToList());
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private async Task<Guid> OpenShiftAsync(HttpClient client, Guid storeId, Guid terminalId, DateOnly businessDate)
    {
        var request = new ShiftOpenRequest { Id = Guid.NewGuid(), StoreId = storeId, TerminalId = terminalId, BusinessDate = businessDate, OpenedAt = Now, OpenedByStaffId = TestData.ManagerStaffId, OpeningCash = 0m };
        using var response = await client.PostJsonAsync(ApiRoutes.Shifts, request, options);
        return (await response.ReadAsAsync<ShiftResponseItem>(HttpStatusCode.Created, options)).Id;
    }

    private async Task CloseShiftAsync(HttpClient client, Guid shiftId, decimal actualCash)
    {
        var request = new ShiftCloseRequest { ClosedAt = Now.AddHours(8), ClosedByStaffId = TestData.ManagerStaffId, ActualCash = actualCash };
        using var response = await client.PostJsonAsync($"{ApiRoutes.Shifts}/{shiftId}/close", request, options);
        await response.ReadAsAsync<ShiftResponseItem>(HttpStatusCode.OK, options);
    }

    // 配送料 1,100 円 (内税 10%、ポイントなし) を現金で
    private async Task<TransactionResponseItem> PostSaleAsync(HttpClient client, Guid storeId, Guid terminalId, Guid shiftId, DateOnly businessDate, string receiptNo)
    {
        var sale = new TransactionCreateRequest
        {
            Id = Guid.NewGuid(),
            Type = TransactionType.Sale,
            Status = TransactionStatus.Completed,
            StoreId = storeId,
            TerminalId = terminalId,
            StaffId = TestData.AdminStaffId,
            ShiftId = shiftId,
            ReceiptNo = receiptNo,
            BusinessDate = businessDate,
            TransactedAt = Now.AddMinutes(10),
            Lines =
            [
                new TransactionCreateRequestLine { Id = Guid.NewGuid(), LineNo = 1, ProductId = TestData.DeliveryProductId, ProductCode = "SVC-DELIVERY", ProductName = "配送料", CategoryId = ServiceCategoryId, Kind = ProductKind.Service, ListPrice = 1100m, UnitPrice = 1100m, Quantity = 1m, TaxRateId = TestData.StandardTaxRateId, TaxRate = 0.10m, TaxIncluded = true, PointRate = 0m }
            ],
            Payments =
            [
                new TransactionCreateRequestPayment { Id = Guid.NewGuid(), SeqNo = 1, PaymentMethodId = TestData.CashPaymentMethodId, Kind = PaymentKind.Cash, Amount = 1100m, TenderedAmount = 1100m }
            ]
        };
        using var calculateResponse = await client.PostJsonAsync($"{ApiRoutes.Transactions}/calculate", TransactionRequests.ToCalculateRequest(sale), options);
        TransactionRequests.Apply(sale, await calculateResponse.ReadAsAsync<TransactionCalculateResponse>(HttpStatusCode.OK, options));
        using var response = await client.PostJsonAsync(ApiRoutes.Transactions, sale, options);
        return await response.ReadAsAsync<TransactionResponseItem>(HttpStatusCode.Created, options);
    }
}
