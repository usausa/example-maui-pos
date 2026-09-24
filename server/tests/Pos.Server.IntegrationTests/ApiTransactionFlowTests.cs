namespace Pos.Server;

using System.Text;
using System.Text.Json;

using Pos.Contract.Customers;
using Pos.Contract.Inventory;
using Pos.Contract.Reports;
using Pos.Contract.Shifts;
using Pos.Contract.Terminals;
using Pos.Contract.Transactions;
using Pos.Server.Host.Endpoints;

// 端末の 1 日: 開設 → 販売 → 再送 → 返品 → 取消 → 入出金 → 精算 → 精算レポート・売上レポート
public sealed class ApiTransactionFlowTests : IClassFixture<TestApplicationFactory>
{
    private static readonly DateTime Now = new(2026, 9, 11, 3, 15, 0, DateTimeKind.Utc);

    private static readonly DateOnly BusinessDate = new(2026, 9, 11);

    private static readonly Guid CameraCategoryId = new("00000000-0000-0000-0004-000000000015");

    private static readonly Guid AccessoryCategoryId = new("00000000-0000-0000-0004-000000000017");

    private static readonly Guid ServiceCategoryId = new("00000000-0000-0000-0004-000000000004");

    private static readonly decimal[] ExampleNetAmounts = [75063m, 3951m, 1086m];

    private readonly TestApplicationFactory factory;

    private readonly JsonSerializerOptions options;

    public ApiTransactionFlowTests(TestApplicationFactory factory)
    {
        this.factory = factory;
        options = factory.JsonOptions();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ShiftSaleReturnVoidCloseAndReports()
    {
        var client = factory.CreateClient();
        var shiftId = Guid.NewGuid();

        // 開設 (再送は 200、別 id で再開設は 409)
        var open = new ShiftOpenRequest { Id = shiftId, StoreId = TestData.MainStoreId, TerminalId = TestData.MainTerminal1Id, BusinessDate = BusinessDate, OpenedAt = Now, OpenedByStaffId = TestData.MainCashierStaffId, OpeningCash = 30000m };
        using var openResponse = await client.PostJsonAsync(ApiRoutes.Shifts, open, options);
        var shift = await openResponse.ReadAsAsync<ShiftResponseItem>(HttpStatusCode.Created, options);
        Assert.Equal(ShiftStatus.Open, shift.Status);
        Assert.Equal(30000m, shift.ExpectedCash);
        using var reopenResponse = await client.PostJsonAsync(ApiRoutes.Shifts, open, options);
        Assert.Equal(shiftId, (await reopenResponse.ReadAsAsync<ShiftResponseItem>(HttpStatusCode.OK, options)).Id);
        using var secondOpenResponse = await client.PostJsonAsync(ApiRoutes.Shifts, new ShiftOpenRequest { Id = Guid.NewGuid(), StoreId = TestData.MainStoreId, TerminalId = TestData.MainTerminal1Id, BusinessDate = BusinessDate, OpenedAt = Now, OpenedByStaffId = TestData.MainCashierStaffId, OpeningCash = 0m }, options);
        await secondOpenResponse.ReadProblemAsync(HttpStatusCode.Conflict, "TERMINAL_HAS_OPEN_SHIFT", options);
        Assert.Equal(shiftId, (await client.GetJsonAsync<ShiftResponseItem>($"{ApiRoutes.Shifts}/current?terminalId={TestData.MainTerminal1Id}", options)).Id);

        var cameraBefore = await QuantityAsync(client, TestData.CameraProductId);
        var sdCardBefore = await QuantityAsync(client, TestData.SdCardProductId);

        // 販売の計算 (設計書の例)
        var sale = CreateSaleRequest(shiftId);
        using var calculateResponse = await client.PostJsonAsync($"{ApiRoutes.Transactions}/calculate", TransactionRequests.ToCalculateRequest(sale), options);
        var calculation = await calculateResponse.ReadAsAsync<TransactionCalculateResponse>(HttpStatusCode.OK, options);
        Assert.Equal(85100m, calculation.Subtotal);
        Assert.Equal(5000m, calculation.DiscountTotal);
        Assert.Equal(80100m, calculation.Total);
        Assert.Equal(7281m, calculation.TaxTotal);
        Assert.Equal(4900m, calculation.ChangeAmount);
        Assert.Equal(7074, calculation.PointsEarned);
        Assert.Equal(5000, calculation.PointsRedeemed);
        Assert.Equal(ExampleNetAmounts, calculation.Lines.Select(static x => x.NetAmount).ToList());
        TransactionRequests.Apply(sale, calculation);

        // 登録 → 再送 200 → 内容違いの再送 409 → 計算違い 422
        using var saleResponse = await client.PostJsonAsync(ApiRoutes.Transactions, sale, options);
        var saleResult = await saleResponse.ReadAsAsync<TransactionResponseItem>(HttpStatusCode.Created, options);
        Assert.Equal(TransactionStatus.Completed, saleResult.Status);
        Assert.Equal(8074, saleResult.PointsBalanceAfter);
        Assert.Empty(saleResult.Warnings);
        Assert.Equal("SN-0001234", Assert.Single(saleResult.Lines[0].SerialNumbers));
        Assert.Equal("山田 太郎", saleResult.Delivery?.RecipientName);
        Assert.Equal(2, saleResult.Discounts.Count);

        using var resendResponse = await client.PostJsonAsync(ApiRoutes.Transactions, sale, options);
        Assert.Equal(sale.Id, (await resendResponse.ReadAsAsync<TransactionResponseItem>(HttpStatusCode.OK, options)).Id);

        // シリアル番号 (完全一致) で取引を引ける
        var bySerial = await client.GetJsonAsync<TransactionResponse>($"{ApiRoutes.Transactions}?serialNumber=SN-0001234", options);
        Assert.Equal(sale.Id, Assert.Single(bySerial.Items).Id);
        Assert.Equal(0, (await client.GetJsonAsync<TransactionResponse>($"{ApiRoutes.Transactions}?serialNumber=SN-000123", options)).Total);

        var mismatch = CreateSaleRequest(shiftId);
        mismatch.Id = sale.Id;
        TransactionRequests.Apply(mismatch, calculation);
        mismatch.Total = 1m;
        using var mismatchResponse = await client.PostJsonAsync(ApiRoutes.Transactions, mismatch, options);
        await mismatchResponse.ReadProblemAsync(HttpStatusCode.Conflict, "DUPLICATE_ID_MISMATCH", options);

        var wrong = CreateSaleRequest(shiftId);
        wrong.ReceiptNo = "S001-01-000099";
        TransactionRequests.Apply(wrong, calculation);
        wrong.Total = 80000m;
        using var wrongResponse = await client.PostJsonAsync(ApiRoutes.Transactions, wrong, options);
        var wrongProblem = await wrongResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "CALCULATION_MISMATCH", options);
        Assert.Equal(80100m, wrongProblem.Expected?.Total);

        // 副作用: ポイント残高・履歴、在庫、端末の連番
        Assert.Equal(8074, (await client.GetJsonAsync<CustomerResponseItem>($"{ApiRoutes.Customers}/{TestData.Customer1Id}", options)).PointBalance);
        var history = await client.GetJsonAsync<CustomerPointHistoryResponse>($"{ApiRoutes.Customers}/{TestData.Customer1Id}/points/history", options);
        Assert.Equal(3, history.Total);
        Assert.Contains(history.Items, static x => (x.Type == PointHistoryType.Redeem) && (x.Points == -5000) && (x.BalanceAfter == 1000));
        Assert.Contains(history.Items, static x => (x.Type == PointHistoryType.Earn) && (x.Points == 7074) && (x.BalanceAfter == 8074));
        Assert.Equal(cameraBefore - 1m, await QuantityAsync(client, TestData.CameraProductId));
        Assert.Equal(sdCardBefore - 2m, await QuantityAsync(client, TestData.SdCardProductId));
        var changes = await client.GetJsonAsync<InventoryChangeResponse>($"{ApiRoutes.Inventory}/changes?storeId={TestData.MainStoreId}&type=Sale", options);
        Assert.Equal(2, changes.Total);
        Assert.All(changes.Items, x => Assert.Equal(sale.Id, x.ReferenceId));
        Assert.Equal(1, (await client.GetJsonAsync<TerminalResponseItem>($"{ApiRoutes.Terminals}/{TestData.MainTerminal1Id}", options)).LastReceiptSeq);

        // 返品 (SD カード 1 枚)
        var sdCardLine = saleResult.Lines[1];
        var returnRequest = CreateReturnRequest(shiftId, sale.Id, sdCardLine, 1m, "S001-01-000002");
        using var returnCalculateResponse = await client.PostJsonAsync($"{ApiRoutes.Transactions}/calculate", TransactionRequests.ToCalculateRequest(returnRequest), options);
        var returnCalculation = await returnCalculateResponse.ReadAsAsync<TransactionCalculateResponse>(HttpStatusCode.OK, options);
        Assert.Equal(1976m, returnCalculation.Total);
        Assert.Equal(179m, returnCalculation.TaxTotal);
        Assert.Equal(-18, returnCalculation.PointsEarned);
        Assert.Equal(-123, returnCalculation.PointsRedeemed);
        TransactionRequests.Apply(returnRequest, returnCalculation);
        using var returnResponse = await client.PostJsonAsync(ApiRoutes.Transactions, returnRequest, options);
        var returnResult = await returnResponse.ReadAsAsync<TransactionResponseItem>(HttpStatusCode.Created, options);
        Assert.Equal(TransactionType.Return, returnResult.Type);
        Assert.Equal(8074 + 123 - 18, returnResult.PointsBalanceAfter);
        Assert.Equal(1m, (await client.GetJsonAsync<TransactionResponseItem>($"{ApiRoutes.Transactions}/{sale.Id}", options)).Lines[1].ReturnedQuantity);
        Assert.Equal(sdCardBefore - 1m, await QuantityAsync(client, TestData.SdCardProductId));

        // 返品可能数量超過
        var excess = CreateReturnRequest(shiftId, sale.Id, sdCardLine, 2m, "S001-01-000003");
        using var excessResponse = await client.PostJsonAsync(ApiRoutes.Transactions, excess, options);
        await excessResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "RETURN_QUANTITY_EXCEEDED", options);

        // 取消: 返品済みの販売は不可、返品の取消は在庫・ポイント・返品数量を戻す
        var voidRequest = new TransactionVoidRequest { StaffId = TestData.ManagerStaffId, Reason = "誤操作", VoidedAt = Now.AddMinutes(30) };
        using var voidSaleResponse = await client.PostJsonAsync($"{ApiRoutes.Transactions}/{sale.Id}/void", voidRequest, options);
        await voidSaleResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "HAS_RETURNS", options);
        using var voidReturnResponse = await client.PostJsonAsync($"{ApiRoutes.Transactions}/{returnRequest.Id}/void", voidRequest, options);
        var voided = await voidReturnResponse.ReadAsAsync<TransactionResponseItem>(HttpStatusCode.OK, options);
        Assert.Equal(TransactionStatus.Voided, voided.Status);
        Assert.Equal("誤操作", voided.Void?.Reason);
        Assert.Equal(8074, (await client.GetJsonAsync<CustomerResponseItem>($"{ApiRoutes.Customers}/{TestData.Customer1Id}", options)).PointBalance);
        Assert.Equal(0m, (await client.GetJsonAsync<TransactionResponseItem>($"{ApiRoutes.Transactions}/{sale.Id}", options)).Lines[1].ReturnedQuantity);
        Assert.Equal(sdCardBefore - 2m, await QuantityAsync(client, TestData.SdCardProductId));
        using var voidAgainResponse = await client.PostJsonAsync($"{ApiRoutes.Transactions}/{returnRequest.Id}/void", voidRequest, options);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, voidAgainResponse.StatusCode);

        // 一覧・検索
        Assert.Equal(2, (await client.GetJsonAsync<TransactionResponse>($"{ApiRoutes.Transactions}?shiftId={shiftId}", options)).Total);
        Assert.Equal(1, (await client.GetJsonAsync<TransactionResponse>($"{ApiRoutes.Transactions}?shiftId={shiftId}&status=Completed", options)).Total);
        Assert.Equal(sale.Id, (await client.GetJsonAsync<TransactionResponseItem>($"{ApiRoutes.Transactions}/lookup?receiptNo={sale.ReceiptNo}", options)).Id);
        Assert.Equal(2, (await client.GetJsonAsync<TransactionResponse>($"{ApiRoutes.Customers}/{TestData.Customer1Id}/transactions", options)).Total);

        // 入出金 (再送は 200)
        var cashEvent = new ShiftCashEventRequest { Id = Guid.NewGuid(), Type = CashEventType.PaidOut, Amount = 10000m, Reason = "両替", StaffId = TestData.MainCashierStaffId, OccurredAt = Now.AddHours(1) };
        using var cashEventResponse = await client.PostJsonAsync($"{ApiRoutes.Shifts}/{shiftId}/cash-events", cashEvent, options);
        await cashEventResponse.ReadAsAsync<ShiftCashEventResponseItem>(HttpStatusCode.Created, options);
        using var cashEventAgainResponse = await client.PostJsonAsync($"{ApiRoutes.Shifts}/{shiftId}/cash-events", cashEvent, options);
        await cashEventAgainResponse.ReadAsAsync<ShiftCashEventResponseItem>(HttpStatusCode.OK, options);
        Assert.Equal(1, (await client.GetJsonAsync<ShiftCashEventResponse>($"{ApiRoutes.Shifts}/{shiftId}/cash-events", options)).Total);

        // 開設中の集計: 30000 + 25100 − 0 + 0 − 10000
        var opened = await client.GetJsonAsync<ShiftResponseItem>($"{ApiRoutes.Shifts}/{shiftId}", options);
        Assert.Equal(25100m, opened.Totals.CashSales);
        Assert.Equal(1, opened.Totals.SalesCount);
        Assert.Equal(0, opened.Totals.ReturnCount);
        Assert.Equal(1, opened.Totals.VoidCount);
        Assert.Equal(45100m, opened.ExpectedCash);

        // 精算 → 精算後は取引・取消・入出金を受け付けない
        using var closeResponse = await client.PostJsonAsync($"{ApiRoutes.Shifts}/{shiftId}/close", new ShiftCloseRequest { ClosedAt = Now.AddHours(8), ClosedByStaffId = TestData.MainCashierStaffId, ActualCash = 45000m, Denominations = [new ShiftCloseRequestDenomination { Denomination = 10000, Count = 4 }, new ShiftCloseRequestDenomination { Denomination = 5000, Count = 1 }] }, options);
        var closed = await closeResponse.ReadAsAsync<ShiftResponseItem>(HttpStatusCode.OK, options);
        Assert.Equal(ShiftStatus.Closed, closed.Status);
        Assert.Equal(45100m, closed.ExpectedCash);
        Assert.Equal(-100m, closed.Difference);
        Assert.Equal(2, closed.Denominations.Count);
        using var noCurrentResponse = await client.GetAsync(new Uri($"{ApiRoutes.Shifts}/current?terminalId={TestData.MainTerminal1Id}", UriKind.Relative), Token);
        await noCurrentResponse.ReadProblemAsync(HttpStatusCode.NotFound, "NOT_FOUND", options);
        var late = CreateSaleRequest(shiftId);
        late.ReceiptNo = "S001-01-000004";
        TransactionRequests.Apply(late, calculation);
        using var lateResponse = await client.PostJsonAsync(ApiRoutes.Transactions, late, options);
        await lateResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "SHIFT_CLOSED", options);
        using var lateVoidResponse = await client.PostJsonAsync($"{ApiRoutes.Transactions}/{sale.Id}/void", voidRequest, options);
        await lateVoidResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "SHIFT_CLOSED", options);
        using var lateCashResponse = await client.PostJsonAsync($"{ApiRoutes.Shifts}/{shiftId}/cash-events", new ShiftCashEventRequest { Id = Guid.NewGuid(), Type = CashEventType.PaidIn, Amount = 1m, StaffId = TestData.MainCashierStaffId, OccurredAt = Now.AddHours(9) }, options);
        await lateCashResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "SHIFT_CLOSED", options);

        // 精算レポート
        var summary = await client.GetJsonAsync<ShiftSummaryResponse>($"{ApiRoutes.Shifts}/{shiftId}/summary", options);
        Assert.Equal(45100m, summary.Cash.ExpectedCash);
        Assert.Equal(45000m, summary.Cash.ActualCash);
        Assert.Equal(10000m, summary.Cash.PaidOut);
        Assert.Equal(3, summary.ByPaymentMethod.Count);
        Assert.Equal(25100m, summary.ByPaymentMethod.Single(static x => x.Kind == PaymentKind.Cash).SalesAmount);
        Assert.Equal(7281m, Assert.Single(summary.ByTaxRate).TaxAmount);
        Assert.Equal(3, summary.ByCategory.Count);
        Assert.Equal(7074, summary.Points.Earned);
        Assert.Equal(5000, summary.Points.Redeemed);
        Assert.Equal(1, (await client.GetJsonAsync<ShiftResponse>($"{ApiRoutes.Shifts}?storeId={TestData.MainStoreId}&status=Closed&from=2026-09-11&to=2026-09-11", options)).Total);

        // 帳票 PDF (D-37)
        await AssertPdfAsync(client, $"{ApiRoutes.Shifts}/{shiftId}/summary/pdf", "shift-report");
        await AssertPdfAsync(client, $"{ApiRoutes.Reports}/sales/daily/pdf?storeId={TestData.MainStoreId}&date=2026-09-11", "daily-sales");
        await AssertPdfAsync(client, $"{ApiRoutes.Transactions}/{sale.Id}/receipt/pdf", "receipt");
        using var missingReceiptResponse = await client.GetAsync(new Uri($"{ApiRoutes.Transactions}/{Guid.NewGuid()}/receipt/pdf", UriKind.Relative), Token);
        await missingReceiptResponse.ReadProblemAsync(HttpStatusCode.NotFound, "NOT_FOUND", options);
        using var missingPdfResponse = await client.GetAsync(new Uri($"{ApiRoutes.Shifts}/{Guid.NewGuid()}/summary/pdf", UriKind.Relative), Token);
        await missingPdfResponse.ReadProblemAsync(HttpStatusCode.NotFound, "NOT_FOUND", options);
        using var missingStorePdfResponse = await client.GetAsync(new Uri($"{ApiRoutes.Reports}/sales/daily/pdf?storeId={Guid.NewGuid()}&date=2026-09-11", UriKind.Relative), Token);
        await missingStorePdfResponse.ReadProblemAsync(HttpStatusCode.NotFound, "NOT_FOUND", options);

        // 売上レポート (取消済みは除外)
        var byDay = await client.GetJsonAsync<ReportSalesSummaryResponse>($"{ApiRoutes.Reports}/sales/summary?storeId={TestData.MainStoreId}&from=2026-09-11&to=2026-09-11&groupBy=day", options);
        var day = Assert.Single(byDay.Rows);
        Assert.Equal("2026-09-11", day.Key);
        Assert.Equal(1, day.TransactionCount);
        Assert.Equal(0, day.ReturnCount);
        Assert.Equal(80100m, byDay.Total.NetSales);
        Assert.Equal(5000m, byDay.Total.DiscountTotal);
        Assert.Equal(7074, byDay.Total.PointsEarned);
        var byHour = await client.GetJsonAsync<ReportSalesSummaryResponse>($"{ApiRoutes.Reports}/sales/summary?storeId={TestData.MainStoreId}&from=2026-09-11&to=2026-09-11&groupBy=hour", options);
        Assert.Equal("12", Assert.Single(byHour.Rows).Key);
        var byPaymentMethod = await client.GetJsonAsync<ReportSalesSummaryResponse>($"{ApiRoutes.Reports}/sales/summary?from=2026-09-11&to=2026-09-11&groupBy=paymentMethod", options);
        Assert.Equal(3, byPaymentMethod.Rows.Count);
        Assert.Equal(80100m, byPaymentMethod.Total.SalesTotal);
        var byTaxRate = await client.GetJsonAsync<ReportSalesSummaryResponse>($"{ApiRoutes.Reports}/sales/summary?from=2026-09-11&to=2026-09-11&groupBy=taxRate", options);
        Assert.Equal(7281m, byTaxRate.Total.TaxAmount);
        var defaultPeriod = await client.GetJsonAsync<ReportSalesSummaryResponse>($"{ApiRoutes.Reports}/sales/summary", options);
        Assert.Equal(DateOnly.FromDateTime(DateTime.Now), defaultPeriod.To);
        Assert.Equal(defaultPeriod.To.AddDays(-30), defaultPeriod.From);
        using var badGroupResponse = await client.GetAsync(new Uri($"{ApiRoutes.Reports}/sales/summary?from=2026-09-11&to=2026-09-11&groupBy=week", UriKind.Relative), Token);
        await badGroupResponse.ReadProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_ERROR", options);
        using var badPeriodResponse = await client.GetAsync(new Uri($"{ApiRoutes.Reports}/sales/summary?from=2026-09-12&to=2026-09-11", UriKind.Relative), Token);
        await badPeriodResponse.ReadProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_ERROR", options);
        var products = await client.GetJsonAsync<ReportProductSalesResponse>($"{ApiRoutes.Reports}/sales/products?from=2026-09-11&to=2026-09-11", options);
        Assert.Equal(3, products.Rows.Count);
        Assert.Equal("CAM-X100", products.Rows[0].ProductCode);
        Assert.Equal(75063m, products.Rows[0].NetSales);
        Assert.Equal(75063m - 60000m, products.Rows[0].GrossProfit);
        var byQuantity = await client.GetJsonAsync<ReportProductSalesResponse>($"{ApiRoutes.Reports}/sales/products?from=2026-09-11&to=2026-09-11&sort=quantity&size=1", options);
        Assert.Equal("SD-64", Assert.Single(byQuantity.Rows).ProductCode);
    }

    // 棚卸 (絶対数量) と調整 (増減)、同じ id の再送は Duplicate
    [Fact]
    public async Task InventoryChangesAreIdempotent()
    {
        var client = factory.CreateClient();
        var before = (await client.GetJsonAsync<InventoryProductResponse>($"{ApiRoutes.Inventory}/{TestData.SdCardProductId}", options)).Levels.Single(static x => x.StoreId == TestData.BranchStoreId).Quantity;
        var request = new InventoryChangeRequest
        {
            Changes =
            [
                new InventoryChangeRequestChange { Id = Guid.NewGuid(), StoreId = TestData.BranchStoreId, ProductId = TestData.SdCardProductId, Type = InventoryChangeType.PhysicalCount, Quantity = before + 5m, StaffId = TestData.BranchCashierStaffId, OccurredAt = Now },
                new InventoryChangeRequestChange { Id = Guid.NewGuid(), StoreId = TestData.BranchStoreId, ProductId = TestData.SdCardProductId, Type = InventoryChangeType.Adjustment, Quantity = -1m, Reason = "破損", StaffId = TestData.BranchCashierStaffId, OccurredAt = Now }
            ]
        };

        using var response = await client.PostJsonAsync($"{ApiRoutes.Inventory}/changes", request, options);
        var result = await response.ReadAsAsync<InventoryChangeResultResponse>(HttpStatusCode.OK, options);
        Assert.All(result.Results, x => Assert.Equal(InventoryChangeResultStatus.Created, x.Status));
        Assert.Equal(5m, result.Results[0].QuantityDelta);
        Assert.Equal(before + 5m, result.Results[0].QuantityAfter);
        Assert.Equal(before + 4m, result.Results[1].QuantityAfter);

        using var againResponse = await client.PostJsonAsync($"{ApiRoutes.Inventory}/changes", request, options);
        var again = await againResponse.ReadAsAsync<InventoryChangeResultResponse>(HttpStatusCode.OK, options);
        Assert.All(again.Results, x => Assert.Equal(InventoryChangeResultStatus.Duplicate, x.Status));
        Assert.Equal(before + 4m, again.Results[1].QuantityAfter);

        var levels = await client.GetJsonAsync<InventoryLevelResponse>($"{ApiRoutes.Inventory}?storeId={TestData.BranchStoreId}&productId={TestData.SdCardProductId}", options);
        Assert.Equal(before + 4m, Assert.Single(levels.Items).Quantity);
        var changes = await client.GetJsonAsync<InventoryChangeResponse>($"{ApiRoutes.Inventory}/changes?storeId={TestData.BranchStoreId}&productId={TestData.SdCardProductId}", options);
        Assert.Equal(2, changes.Total);
        var negative = await client.GetJsonAsync<InventoryLevelResponse>($"{ApiRoutes.Inventory}?negativeOnly=true", options);
        Assert.Equal(0, negative.Total);
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    // application/pdf で %PDF から始まる本文。確認用に TestResults へ保存する
    private static async Task AssertPdfAsync(HttpClient client, string url, string name)
    {
        using var response = await client.GetAsync(new Uri(url, UriKind.Relative), Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith(name, response.Content.Headers.ContentDisposition?.FileName?.Trim('"'), StringComparison.Ordinal);
        var bytes = await response.Content.ReadAsByteArrayAsync(Token);
        Assert.True(bytes.Length > 1000);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));

        Directory.CreateDirectory("TestResults");
        await File.WriteAllBytesAsync(Path.Combine("TestResults", $"{name}.pdf"), bytes, Token);
    }

    private async Task<decimal> QuantityAsync(HttpClient client, Guid productId) =>
        (await client.GetJsonAsync<InventoryProductResponse>($"{ApiRoutes.Inventory}/{productId}", options)).Levels.Single(static x => x.StoreId == TestData.MainStoreId).Quantity;

    // 設計書の販売例: デジカメ (展示品 5%) + SD カード × 2 + 配送料、取引値引 1,000、ポイント 5,000 + カード 50,000 + 現金 25,100 (預り 30,000)
    private static TransactionCreateRequest CreateSaleRequest(Guid shiftId)
    {
        var cameraLineId = Guid.NewGuid();
        return new TransactionCreateRequest
        {
            Id = Guid.NewGuid(),
            Type = TransactionType.Sale,
            Status = TransactionStatus.Completed,
            StoreId = TestData.MainStoreId,
            TerminalId = TestData.MainTerminal1Id,
            StaffId = TestData.MainCashierStaffId,
            ShiftId = shiftId,
            CustomerId = TestData.Customer1Id,
            ReceiptNo = "S001-01-000001",
            BusinessDate = BusinessDate,
            TransactedAt = Now,
            Lines =
            [
                new TransactionCreateRequestLine { Id = cameraLineId, LineNo = 1, ProductId = TestData.CameraProductId, ProductCode = "CAM-X100", ProductName = "デジタルカメラ X-100", CategoryId = CameraCategoryId, Kind = ProductKind.Goods, ListPrice = 80000m, UnitPrice = 80000m, Quantity = 1m, TaxRateId = TestData.StandardTaxRateId, TaxRate = 0.10m, TaxIncluded = true, PointRate = 0.10m, SerialNumbers = ["SN-0001234"] },
                new TransactionCreateRequestLine { Id = Guid.NewGuid(), LineNo = 2, ProductId = TestData.SdCardProductId, ProductCode = "SD-64", ProductName = "SD カード 64GB", CategoryId = AccessoryCategoryId, Kind = ProductKind.Goods, ListPrice = 2000m, UnitPrice = 2000m, Quantity = 2m, TaxRateId = TestData.StandardTaxRateId, TaxRate = 0.10m, TaxIncluded = true, PointRate = 0.01m },
                new TransactionCreateRequestLine { Id = Guid.NewGuid(), LineNo = 3, ProductId = TestData.DeliveryProductId, ProductCode = "SVC-DELIVERY", ProductName = "配送料", CategoryId = ServiceCategoryId, Kind = ProductKind.Service, ListPrice = 1100m, UnitPrice = 1100m, Quantity = 1m, TaxRateId = TestData.StandardTaxRateId, TaxRate = 0.10m, TaxIncluded = true, PointRate = 0m }
            ],
            Discounts =
            [
                new TransactionCreateRequestDiscount { Id = Guid.NewGuid(), LineId = cameraLineId, DiscountId = TestData.DisplayDiscountId, Name = "展示品 5%", Type = DiscountType.Percent, Value = 0.05m, ApprovedByStaffId = TestData.ManagerStaffId },
                new TransactionCreateRequestDiscount { Id = Guid.NewGuid(), Name = "端数値引", Type = DiscountType.Amount, Value = 1000m, Reason = "セット割" }
            ],
            Payments =
            [
                new TransactionCreateRequestPayment { Id = Guid.NewGuid(), SeqNo = 1, PaymentMethodId = TestData.PointsPaymentMethodId, Kind = PaymentKind.Points, Amount = 5000m, TenderedAmount = 5000m },
                new TransactionCreateRequestPayment { Id = Guid.NewGuid(), SeqNo = 2, PaymentMethodId = TestData.CardPaymentMethodId, Kind = PaymentKind.Card, Amount = 50000m, TenderedAmount = 50000m, Reference = "CARD-0001" },
                new TransactionCreateRequestPayment { Id = Guid.NewGuid(), SeqNo = 3, PaymentMethodId = TestData.CashPaymentMethodId, Kind = PaymentKind.Cash, Amount = 25100m, TenderedAmount = 30000m }
            ],
            Delivery = new TransactionCreateRequestDelivery { RecipientName = "山田 太郎", Address = "東京都千代田区千代田 1-1-1", RequestedDate = new DateOnly(2026, 9, 14), TimeSlot = "14-16" }
        };
    }

    // 返品: ポイント返還分 (−pointsRedeemed) をポイント、残りを現金で返金する
    private static TransactionCreateRequest CreateReturnRequest(Guid shiftId, Guid originalId, TransactionResponseLine original, decimal quantity, string receiptNo)
    {
        var refundPoints = (int)Math.Floor(original.PointsRedeemed * quantity / original.Quantity);
        var netAmount = Math.Floor(original.UnitPrice * quantity) - Math.Floor(original.DiscountAmount * quantity / original.Quantity) - Math.Floor(original.AllocatedDiscountAmount * quantity / original.Quantity);
        return new TransactionCreateRequest
        {
            Id = Guid.NewGuid(),
            Type = TransactionType.Return,
            Status = TransactionStatus.Completed,
            StoreId = TestData.MainStoreId,
            TerminalId = TestData.MainTerminal1Id,
            StaffId = TestData.MainCashierStaffId,
            ShiftId = shiftId,
            CustomerId = TestData.Customer1Id,
            ReceiptNo = receiptNo,
            BusinessDate = BusinessDate,
            TransactedAt = Now.AddMinutes(10),
            OriginalTransactionId = originalId,
            Lines =
            [
                new TransactionCreateRequestLine { Id = Guid.NewGuid(), LineNo = 1, ProductId = original.ProductId, ProductCode = original.ProductCode, ProductName = original.ProductName, CategoryId = original.CategoryId, Kind = original.Kind, ListPrice = original.ListPrice, UnitPrice = original.UnitPrice, Quantity = quantity, TaxRateId = original.TaxRateId, TaxRate = original.TaxRate, TaxIncluded = original.TaxIncluded, PointRate = original.PointRate, OriginalLineId = original.Id }
            ],
            Payments =
            [
                new TransactionCreateRequestPayment { Id = Guid.NewGuid(), SeqNo = 1, PaymentMethodId = TestData.PointsPaymentMethodId, Kind = PaymentKind.Points, Amount = refundPoints, TenderedAmount = refundPoints },
                new TransactionCreateRequestPayment { Id = Guid.NewGuid(), SeqNo = 2, PaymentMethodId = TestData.CashPaymentMethodId, Kind = PaymentKind.Cash, Amount = netAmount - refundPoints, TenderedAmount = netAmount - refundPoints }
            ]
        };
    }
}
