namespace Pos.Server;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

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

    private static readonly decimal[] ExampleNetAmounts = [75063m, 3951m, 1086m];

    private readonly TestApplicationFactory factory;

    private readonly JsonSerializerOptions options;

    public ApiTransactionFlowTests(TestApplicationFactory factory)
    {
        this.factory = factory;
        options = factory.JsonOptions();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // 開設から販売・返品・取消・入出金・精算・レポートまでの一連の流れ
    [Fact]
    public async Task ShiftSaleReturnVoidCloseAndReports()
    {
        // Arrange
        var client = await factory.CreateAdminClientAsync();
        var shiftId = Guid.NewGuid();

        // Act / Assert: 開設 (再送は 200、別 id で再開設は 409)
        var open = new ShiftOpenRequest { Id = shiftId, StoreId = TestData.MainStoreId, TerminalId = TestData.MainTerminal1Id, BusinessDate = BusinessDate, OpenedAt = Now, OpenedByStaffId = TestData.MainCashierStaffId, OpeningCash = 30000m };
        using var openResponse = await client.PostJsonAsync(ApiRoutes.Shifts, open, options);
        var shift = await openResponse.ReadAsAsync<ShiftListResponseItem>(HttpStatusCode.Created, options);
        Assert.Equal(ShiftStatus.Open, shift.Status);
        Assert.Equal(30000m, shift.ExpectedCash);
        using var reopenResponse = await client.PostJsonAsync(ApiRoutes.Shifts, open, options);
        Assert.Equal(shiftId, (await reopenResponse.ReadAsAsync<ShiftListResponseItem>(HttpStatusCode.OK, options)).Id);
        using var secondOpenResponse = await client.PostJsonAsync(ApiRoutes.Shifts, new ShiftOpenRequest { Id = Guid.NewGuid(), StoreId = TestData.MainStoreId, TerminalId = TestData.MainTerminal1Id, BusinessDate = BusinessDate, OpenedAt = Now, OpenedByStaffId = TestData.MainCashierStaffId, OpeningCash = 0m }, options);
        await secondOpenResponse.ReadProblemAsync(HttpStatusCode.Conflict, "TERMINAL_HAS_OPEN_SHIFT", options);
        Assert.Equal(shiftId, (await client.GetJsonAsync<ShiftListResponseItem>($"{ApiRoutes.Shifts}/current?terminalId={TestData.MainTerminal1Id}", options)).Id);

        var cameraBefore = await QuantityAsync(client, TestData.CameraProductId);
        var sdCardBefore = await QuantityAsync(client, TestData.SdCardProductId);

        // Act / Assert: 販売の計算
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

        // Act / Assert: 登録 → 再送 200 → 内容違いの再送 409 → 計算違い 422
        using var saleResponse = await client.PostJsonAsync(ApiRoutes.Transactions, sale, options);
        var saleResult = await saleResponse.ReadAsAsync<TransactionListResponseItem>(HttpStatusCode.Created, options);
        Assert.Equal(TransactionStatus.Completed, saleResult.Status);
        Assert.Equal(8074, saleResult.PointsBalanceAfter);
        Assert.Empty(saleResult.Warnings);
        Assert.Equal("SN-0001234", Assert.Single(saleResult.Lines[0].SerialNumbers));
        Assert.Equal("山田 太郎", saleResult.Delivery?.RecipientName);
        Assert.Equal(2, saleResult.Discounts.Count);

        using var resendResponse = await client.PostJsonAsync(ApiRoutes.Transactions, sale, options);
        Assert.Equal(sale.Id, (await resendResponse.ReadAsAsync<TransactionListResponseItem>(HttpStatusCode.OK, options)).Id);

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

        // Act / Assert: シリアル番号 (完全一致) で取引を引ける
        var bySerial = await client.GetJsonAsync<TransactionListResponse>($"{ApiRoutes.Transactions}?serialNumber=SN-0001234", options);
        Assert.Equal(sale.Id, Assert.Single(bySerial.Items).Id);
        Assert.Equal(0, (await client.GetJsonAsync<TransactionListResponse>($"{ApiRoutes.Transactions}?serialNumber=SN-000123", options)).Total);

        // Act / Assert: 副作用 (ポイント残高・履歴、在庫、端末の連番)
        Assert.Equal(8074, (await client.GetJsonAsync<CustomerListResponseItem>($"{ApiRoutes.Customers}/{TestData.Customer1Id}", options)).PointBalance);
        var history = await client.GetJsonAsync<CustomerPointHistoryResponse>($"{ApiRoutes.Customers}/{TestData.Customer1Id}/points/history", options);
        Assert.Equal(3, history.Total);
        Assert.Contains(history.Items, static x => (x.Type == PointHistoryType.Redeem) && (x.Points == -5000) && (x.BalanceAfter == 1000));
        Assert.Contains(history.Items, static x => (x.Type == PointHistoryType.Earn) && (x.Points == 7074) && (x.BalanceAfter == 8074));
        Assert.Equal(cameraBefore - 1m, await QuantityAsync(client, TestData.CameraProductId));
        Assert.Equal(sdCardBefore - 2m, await QuantityAsync(client, TestData.SdCardProductId));
        var changes = await client.GetJsonAsync<InventoryChangeListResponse>($"{ApiRoutes.Inventory}/changes?storeId={TestData.MainStoreId}&type=Sale", options);
        Assert.Equal(2, changes.Total);
        Assert.All(changes.Items, x => Assert.Equal(sale.Id, x.ReferenceId));
        Assert.Equal(1, (await client.GetJsonAsync<TerminalListResponseItem>($"{ApiRoutes.Terminals}/{TestData.MainTerminal1Id}", options)).LastReceiptSeq);

        // Act / Assert: 返品 (SD カード 1 枚)
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
        var returnResult = await returnResponse.ReadAsAsync<TransactionListResponseItem>(HttpStatusCode.Created, options);
        Assert.Equal(TransactionType.Return, returnResult.Type);
        Assert.Equal(8074 + 123 - 18, returnResult.PointsBalanceAfter);
        Assert.Equal(1m, (await client.GetJsonAsync<TransactionListResponseItem>($"{ApiRoutes.Transactions}/{sale.Id}", options)).Lines[1].ReturnedQuantity);
        Assert.Equal(sdCardBefore - 1m, await QuantityAsync(client, TestData.SdCardProductId));

        // Act / Assert: 返品可能数量超過
        var excess = CreateReturnRequest(shiftId, sale.Id, sdCardLine, 2m, "S001-01-000003");
        using var excessResponse = await client.PostJsonAsync(ApiRoutes.Transactions, excess, options);
        await excessResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "RETURN_QUANTITY_EXCEEDED", options);

        // Act / Assert: 返品の商品が見つからない (外部キーの違反の 500 にしない)
        var noProduct = CreateReturnRequest(shiftId, sale.Id, sdCardLine, 1m, "S001-01-000005");
        TransactionRequests.Apply(noProduct, returnCalculation);
        noProduct.Lines[0].ProductId = Guid.NewGuid();
        using var noProductResponse = await client.PostJsonAsync(ApiRoutes.Transactions, noProduct, options);
        await noProductResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "PRODUCT_NOT_FOUND", options);

        // Act / Assert: 取消 (返品済みの販売は不可、返品の取消は在庫・ポイント・返品数量を戻す)
        var voidRequest = new TransactionVoidRequest { StaffId = TestData.ManagerStaffId, Reason = "誤操作", VoidedAt = Now.AddMinutes(30) };
        using var voidSaleResponse = await client.PostJsonAsync($"{ApiRoutes.Transactions}/{sale.Id}/void", voidRequest, options);
        await voidSaleResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "HAS_RETURNS", options);
        using var voidReturnResponse = await client.PostJsonAsync($"{ApiRoutes.Transactions}/{returnRequest.Id}/void", voidRequest, options);
        var voided = await voidReturnResponse.ReadAsAsync<TransactionListResponseItem>(HttpStatusCode.OK, options);
        Assert.Equal(TransactionStatus.Voided, voided.Status);
        Assert.Equal("誤操作", voided.Void?.Reason);
        Assert.Equal(8074, (await client.GetJsonAsync<CustomerListResponseItem>($"{ApiRoutes.Customers}/{TestData.Customer1Id}", options)).PointBalance);
        Assert.Equal(0m, (await client.GetJsonAsync<TransactionListResponseItem>($"{ApiRoutes.Transactions}/{sale.Id}", options)).Lines[1].ReturnedQuantity);
        Assert.Equal(sdCardBefore - 2m, await QuantityAsync(client, TestData.SdCardProductId));
        using var voidAgainResponse = await client.PostJsonAsync($"{ApiRoutes.Transactions}/{returnRequest.Id}/void", voidRequest, options);
        await voidAgainResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "VALIDATION_ERROR", options);

        // Act / Assert: 一覧・検索
        Assert.Equal(2, (await client.GetJsonAsync<TransactionListResponse>($"{ApiRoutes.Transactions}?shiftId={shiftId}", options)).Total);
        Assert.Equal(1, (await client.GetJsonAsync<TransactionListResponse>($"{ApiRoutes.Transactions}?shiftId={shiftId}&status=Completed", options)).Total);
        Assert.Equal(sale.Id, (await client.GetJsonAsync<TransactionListResponseItem>($"{ApiRoutes.Transactions}/lookup?receiptNo={sale.ReceiptNo}", options)).Id);
        using var noReceiptNoResponse = await client.GetAsync(new Uri($"{ApiRoutes.Transactions}/lookup?receiptNo=", UriKind.Relative), Token);
        await noReceiptNoResponse.ReadProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_ERROR", options);
        Assert.Equal(2, (await client.GetJsonAsync<TransactionListResponse>($"{ApiRoutes.Customers}/{TestData.Customer1Id}/transactions", options)).Total);
        using var unknownCustomerResponse = await client.GetAsync(new Uri($"{ApiRoutes.Customers}/{Guid.NewGuid()}/transactions", UriKind.Relative), Token);
        await unknownCustomerResponse.ReadProblemAsync(HttpStatusCode.NotFound, "NOT_FOUND", options);

        // Act / Assert: 入出金 (再送は 200)
        var cashEvent = new ShiftCashEventRequest { Id = Guid.NewGuid(), Type = CashEventType.PaidOut, Amount = 10000m, Reason = "両替", StaffId = TestData.MainCashierStaffId, OccurredAt = Now.AddHours(1) };
        using var cashEventResponse = await client.PostJsonAsync($"{ApiRoutes.Shifts}/{shiftId}/cash-events", cashEvent, options);
        await cashEventResponse.ReadAsAsync<ShiftCashEventListResponseItem>(HttpStatusCode.Created, options);
        using var cashEventAgainResponse = await client.PostJsonAsync($"{ApiRoutes.Shifts}/{shiftId}/cash-events", cashEvent, options);
        await cashEventAgainResponse.ReadAsAsync<ShiftCashEventListResponseItem>(HttpStatusCode.OK, options);
        Assert.Equal(1, (await client.GetJsonAsync<ShiftCashEventListResponse>($"{ApiRoutes.Shifts}/{shiftId}/cash-events", options)).Total);

        // Act / Assert: 開設中の集計 (30000 + 25100 − 0 + 0 − 10000)
        var opened = await client.GetJsonAsync<ShiftListResponseItem>($"{ApiRoutes.Shifts}/{shiftId}", options);
        Assert.Equal(25100m, opened.Totals.CashSales);
        Assert.Equal(1, opened.Totals.SalesCount);
        Assert.Equal(0, opened.Totals.ReturnCount);
        Assert.Equal(1, opened.Totals.VoidCount);
        Assert.Equal(45100m, opened.ExpectedCash);

        // Act / Assert: 精算 → 精算後は取引・取消・入出金を受け付けない
        using var closeResponse = await client.PostJsonAsync($"{ApiRoutes.Shifts}/{shiftId}/close", new ShiftCloseRequest { ClosedAt = Now.AddHours(8), ClosedByStaffId = TestData.MainCashierStaffId, ActualCash = 45000m, Denominations = [new ShiftCloseRequestDenomination { Denomination = 10000, Count = 4 }, new ShiftCloseRequestDenomination { Denomination = 5000, Count = 1 }] }, options);
        var closed = await closeResponse.ReadAsAsync<ShiftListResponseItem>(HttpStatusCode.OK, options);
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

        // Act / Assert: 精算レポート
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
        Assert.Equal(1, (await client.GetJsonAsync<ShiftListResponse>($"{ApiRoutes.Shifts}?storeId={TestData.MainStoreId}&status=Closed&from=2026-09-11&to=2026-09-11", options)).Total);

        // Act / Assert: 帳票 PDF (ない取引・シフトは 404)
        await AssertPdfAsync(client, $"{ApiRoutes.Shifts}/{shiftId}/summary/pdf", "shift-report");
        await AssertPdfAsync(client, $"{ApiRoutes.Reports}/sales/daily/pdf?storeId={TestData.MainStoreId}&date=2026-09-11", "daily-sales");
        await AssertPdfAsync(client, $"{ApiRoutes.Transactions}/{sale.Id}/receipt/pdf", "receipt");
        using var missingReceiptResponse = await client.GetAsync(new Uri($"{ApiRoutes.Transactions}/{Guid.NewGuid()}/receipt/pdf", UriKind.Relative), Token);
        await missingReceiptResponse.ReadProblemAsync(HttpStatusCode.NotFound, "NOT_FOUND", options);
        using var missingPdfResponse = await client.GetAsync(new Uri($"{ApiRoutes.Shifts}/{Guid.NewGuid()}/summary/pdf", UriKind.Relative), Token);
        await missingPdfResponse.ReadProblemAsync(HttpStatusCode.NotFound, "NOT_FOUND", options);
        using var missingStorePdfResponse = await client.GetAsync(new Uri($"{ApiRoutes.Reports}/sales/daily/pdf?storeId={Guid.NewGuid()}&date=2026-09-11", UriKind.Relative), Token);
        await missingStorePdfResponse.ReadProblemAsync(HttpStatusCode.NotFound, "NOT_FOUND", options);

        // Act / Assert: 売上レポート (取消済みは除外)
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

    // 店舗・端末・担当が見つからない開設・入出金・精算は 422、精算の金種の重複と null は 400 (外部キーや主キーの違反の 500 にすると、端末の未送信が送り直し続ける)
    [Fact]
    public async Task ShiftWritesRejectMissingReferencesAndDuplicates()
    {
        // Arrange
        var client = await factory.CreateAdminClientAsync();
        var missingId = Guid.NewGuid();
        var businessDate = new DateOnly(2026, 9, 20);

        // Act / Assert: 開設の店舗・端末・担当
        using var noStoreResponse = await client.PostJsonAsync(ApiRoutes.Shifts, new ShiftOpenRequest { Id = Guid.NewGuid(), StoreId = missingId, TerminalId = TestData.BranchTerminalId, BusinessDate = businessDate, OpenedAt = Now, OpenedByStaffId = TestData.BranchCashierStaffId, OpeningCash = 0m }, options);
        await noStoreResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "VALIDATION_ERROR", options);
        using var noTerminalResponse = await client.PostJsonAsync(ApiRoutes.Shifts, new ShiftOpenRequest { Id = Guid.NewGuid(), StoreId = TestData.BranchStoreId, TerminalId = missingId, BusinessDate = businessDate, OpenedAt = Now, OpenedByStaffId = TestData.BranchCashierStaffId, OpeningCash = 0m }, options);
        await noTerminalResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "VALIDATION_ERROR", options);
        using var noStaffResponse = await client.PostJsonAsync(ApiRoutes.Shifts, new ShiftOpenRequest { Id = Guid.NewGuid(), StoreId = TestData.BranchStoreId, TerminalId = TestData.BranchTerminalId, BusinessDate = businessDate, OpenedAt = Now, OpenedByStaffId = missingId, OpeningCash = 0m }, options);
        await noStaffResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "STAFF_INVALID", options);

        // Act / Assert: 入出金と精算の担当
        var shiftId = Guid.NewGuid();
        using var openResponse = await client.PostJsonAsync(ApiRoutes.Shifts, new ShiftOpenRequest { Id = shiftId, StoreId = TestData.BranchStoreId, TerminalId = TestData.BranchTerminalId, BusinessDate = businessDate, OpenedAt = Now, OpenedByStaffId = TestData.BranchCashierStaffId, OpeningCash = 0m }, options);
        Assert.Equal(HttpStatusCode.Created, openResponse.StatusCode);
        using var cashEventResponse = await client.PostJsonAsync($"{ApiRoutes.Shifts}/{shiftId}/cash-events", new ShiftCashEventRequest { Id = Guid.NewGuid(), Type = CashEventType.PaidIn, Amount = 100m, Reason = "両替", StaffId = missingId, OccurredAt = Now }, options);
        await cashEventResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "STAFF_INVALID", options);
        using var closeResponse = await client.PostJsonAsync($"{ApiRoutes.Shifts}/{shiftId}/close", new ShiftCloseRequest { ClosedAt = Now, ClosedByStaffId = missingId, ActualCash = 0m }, options);
        await closeResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "STAFF_INVALID", options);

        // Act / Assert: 精算の金種の重複と null
        var duplicated = new ShiftCloseRequest
        {
            ClosedAt = Now,
            ClosedByStaffId = TestData.BranchCashierStaffId,
            ActualCash = 3000m,
            Denominations = [new ShiftCloseRequestDenomination { Denomination = 1000, Count = 1 }, new ShiftCloseRequestDenomination { Denomination = 1000, Count = 2 }]
        };
        using var duplicatedResponse = await client.PostJsonAsync($"{ApiRoutes.Shifts}/{shiftId}/close", duplicated, options);
        await duplicatedResponse.ReadProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_ERROR", options);
        using var nullResponse = await PostWithNullAsync(client, $"{ApiRoutes.Shifts}/{shiftId}/close", new ShiftCloseRequest { ClosedAt = Now, ClosedByStaffId = TestData.BranchCashierStaffId, ActualCash = 0m }, static x => x["denominations"] = null);
        await nullResponse.ReadProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_ERROR", options);
        Assert.Equal(ShiftStatus.Open, (await client.GetJsonAsync<ShiftListResponseItem>($"{ApiRoutes.Shifts}/{shiftId}", options)).Status);
    }

    // 販売の参照先 (店舗、会員、支払方法、税率、値引、承認者) が見つからなければ 422、一意の制約になる ID と番号の重複、販売に付いた元取引、null の一覧 (登録と計算) と null のシリアル番号は 400 で、どれも書かない
    // (外部キーや一意の制約の違反の 500 にすると、端末の未送信が送り直し続ける)
    [Fact]
    public async Task SaleRejectsMissingReferencesAndDuplicates()
    {
        // Arrange
        var client = await factory.CreateAdminClientAsync();
        var shiftId = Guid.NewGuid();
        using var openResponse = await client.PostJsonAsync(ApiRoutes.Shifts, new ShiftOpenRequest { Id = shiftId, StoreId = TestData.MainStoreId, TerminalId = TestData.MainTerminal2Id, BusinessDate = BusinessDate, OpenedAt = Now, OpenedByStaffId = TestData.MainCashierStaffId, OpeningCash = 0m }, options);
        Assert.Equal(HttpStatusCode.Created, openResponse.StatusCode);
        using var calculateResponse = await client.PostJsonAsync($"{ApiRoutes.Transactions}/calculate", TransactionRequests.ToCalculateRequest(CreateSaleRequest(shiftId)), options);
        var calculation = await calculateResponse.ReadAsAsync<TransactionCalculateResponse>(HttpStatusCode.OK, options);

        // Act / Assert: 参照先が見つからない
        using var noStoreResponse = await client.PostJsonAsync(ApiRoutes.Transactions, CreateTerminal2SaleRequest(shiftId, calculation, static x => x.StoreId = Guid.NewGuid()), options);
        await noStoreResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "VALIDATION_ERROR", options);
        using var noCustomerResponse = await client.PostJsonAsync(ApiRoutes.Transactions, CreateTerminal2SaleRequest(shiftId, calculation, static x => x.CustomerId = Guid.NewGuid()), options);
        await noCustomerResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "VALIDATION_ERROR", options);
        using var noPaymentMethodResponse = await client.PostJsonAsync(ApiRoutes.Transactions, CreateTerminal2SaleRequest(shiftId, calculation, static x => x.Payments[0].PaymentMethodId = Guid.NewGuid()), options);
        await noPaymentMethodResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "VALIDATION_ERROR", options);
        using var noTaxRateResponse = await client.PostJsonAsync(ApiRoutes.Transactions, CreateTerminal2SaleRequest(shiftId, calculation, static x => x.TaxSummaries[0].TaxRateId = Guid.NewGuid()), options);
        await noTaxRateResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "VALIDATION_ERROR", options);
        using var noDiscountResponse = await client.PostJsonAsync(ApiRoutes.Transactions, CreateTerminal2SaleRequest(shiftId, calculation, static x => x.Discounts[0].DiscountId = Guid.NewGuid()), options);
        await noDiscountResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "VALIDATION_ERROR", options);
        using var noApproverResponse = await client.PostJsonAsync(ApiRoutes.Transactions, CreateTerminal2SaleRequest(shiftId, calculation, static x => x.Discounts[1].ApprovedByStaffId = Guid.NewGuid()), options);
        await noApproverResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "APPROVAL_REQUIRED", options);

        // Act / Assert: 明細・値引・支払の ID と番号、シリアル番号の重複、販売に付いた元取引、null の一覧とシリアル番号
        using var lineIdResponse = await client.PostJsonAsync(ApiRoutes.Transactions, CreateTerminal2SaleRequest(shiftId, calculation, static x => x.Lines[2].Id = x.Lines[1].Id), options);
        await lineIdResponse.ReadProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_ERROR", options);
        using var lineNoResponse = await client.PostJsonAsync(ApiRoutes.Transactions, CreateTerminal2SaleRequest(shiftId, calculation, static x => x.Lines[2].LineNo = 2), options);
        await lineNoResponse.ReadProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_ERROR", options);
        using var serialResponse = await client.PostJsonAsync(ApiRoutes.Transactions, CreateTerminal2SaleRequest(shiftId, calculation, static x => x.Lines[0].SerialNumbers = ["SN-0001234", "SN-0001234"]), options);
        await serialResponse.ReadProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_ERROR", options);
        using var discountIdResponse = await client.PostJsonAsync(ApiRoutes.Transactions, CreateTerminal2SaleRequest(shiftId, calculation, static x => x.Discounts[1].Id = x.Discounts[0].Id), options);
        await discountIdResponse.ReadProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_ERROR", options);
        using var paymentIdResponse = await client.PostJsonAsync(ApiRoutes.Transactions, CreateTerminal2SaleRequest(shiftId, calculation, static x => x.Payments[2].Id = x.Payments[1].Id), options);
        await paymentIdResponse.ReadProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_ERROR", options);
        using var seqNoResponse = await client.PostJsonAsync(ApiRoutes.Transactions, CreateTerminal2SaleRequest(shiftId, calculation, static x => x.Payments[2].SeqNo = 2), options);
        await seqNoResponse.ReadProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_ERROR", options);
        using var originalResponse = await client.PostJsonAsync(ApiRoutes.Transactions, CreateTerminal2SaleRequest(shiftId, calculation, static x => x.OriginalTransactionId = Guid.NewGuid()), options);
        await originalResponse.ReadProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_ERROR", options);
        using var originalLineResponse = await client.PostJsonAsync(ApiRoutes.Transactions, CreateTerminal2SaleRequest(shiftId, calculation, static x => x.Lines[0].OriginalLineId = Guid.NewGuid()), options);
        await originalLineResponse.ReadProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_ERROR", options);
        var valid = CreateTerminal2SaleRequest(shiftId, calculation, static _ => { });
        using var nullDiscountsResponse = await PostWithNullAsync(client, ApiRoutes.Transactions, valid, static x => x["discounts"] = null);
        await nullDiscountsResponse.ReadProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_ERROR", options);
        using var nullTaxSummariesResponse = await PostWithNullAsync(client, ApiRoutes.Transactions, valid, static x => x["taxSummaries"] = null);
        await nullTaxSummariesResponse.ReadProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_ERROR", options);
        using var nullSerialsResponse = await PostWithNullAsync(client, ApiRoutes.Transactions, valid, static x => x["lines"]![0]!["serialNumbers"] = null);
        await nullSerialsResponse.ReadProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_ERROR", options);
        using var nullSerialNumberResponse = await PostWithNullAsync(client, ApiRoutes.Transactions, valid, static x => x["lines"]![0]!["serialNumbers"]![0] = null);
        await nullSerialNumberResponse.ReadProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_ERROR", options);
        using var nullCalculateDiscountsResponse = await PostWithNullAsync(client, $"{ApiRoutes.Transactions}/calculate", TransactionRequests.ToCalculateRequest(valid), static x => x["discounts"] = null);
        await nullCalculateDiscountsResponse.ReadProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_ERROR", options);
        using var nullCalculatePaymentsResponse = await PostWithNullAsync(client, $"{ApiRoutes.Transactions}/calculate", TransactionRequests.ToCalculateRequest(valid), static x => x["payments"] = null);
        await nullCalculatePaymentsResponse.ReadProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_ERROR", options);

        // Act / Assert: どれも書いていない
        Assert.Equal(0, (await client.GetJsonAsync<TransactionListResponse>($"{ApiRoutes.Transactions}?shiftId={shiftId}", options)).Total);
    }

    // 参照先 (店舗、商品、調整理由、担当) が見つからない変動があれば 422 で、一緒に送った変動も書かない (外部キーの違反の 500 にしない)
    [Fact]
    public async Task InventoryChangesRejectMissingReferences()
    {
        // Arrange
        var client = await factory.CreateAdminClientAsync();
        var before = await QuantityAsync(client, TestData.SdCardProductId);
        var valid = new InventoryChangesRequestChange { Id = Guid.NewGuid(), StoreId = TestData.MainStoreId, ProductId = TestData.SdCardProductId, Type = InventoryChangeType.Adjustment, Quantity = 1m, StaffId = TestData.MainCashierStaffId, OccurredAt = Now };
        var noStore = new InventoryChangesRequestChange { Id = Guid.NewGuid(), StoreId = Guid.NewGuid(), ProductId = TestData.SdCardProductId, Type = InventoryChangeType.Adjustment, Quantity = 1m, StaffId = TestData.MainCashierStaffId, OccurredAt = Now };
        var noProduct = new InventoryChangesRequestChange { Id = Guid.NewGuid(), StoreId = TestData.MainStoreId, ProductId = Guid.NewGuid(), Type = InventoryChangeType.Adjustment, Quantity = 1m, StaffId = TestData.MainCashierStaffId, OccurredAt = Now };
        var noReason = new InventoryChangesRequestChange { Id = Guid.NewGuid(), StoreId = TestData.MainStoreId, ProductId = TestData.SdCardProductId, Type = InventoryChangeType.Adjustment, Quantity = 1m, ReasonId = Guid.NewGuid(), StaffId = TestData.MainCashierStaffId, OccurredAt = Now };
        var noStaff = new InventoryChangesRequestChange { Id = Guid.NewGuid(), StoreId = TestData.MainStoreId, ProductId = TestData.SdCardProductId, Type = InventoryChangeType.Adjustment, Quantity = 1m, StaffId = Guid.NewGuid(), OccurredAt = Now };

        // Act
        using var noStoreResponse = await client.PostJsonAsync($"{ApiRoutes.Inventory}/changes", new InventoryChangesRequest { Changes = [valid, noStore] }, options);
        using var noProductResponse = await client.PostJsonAsync($"{ApiRoutes.Inventory}/changes", new InventoryChangesRequest { Changes = [valid, noProduct] }, options);
        using var noReasonResponse = await client.PostJsonAsync($"{ApiRoutes.Inventory}/changes", new InventoryChangesRequest { Changes = [valid, noReason] }, options);
        using var noStaffResponse = await client.PostJsonAsync($"{ApiRoutes.Inventory}/changes", new InventoryChangesRequest { Changes = [valid, noStaff] }, options);

        // Assert
        await noStoreResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "VALIDATION_ERROR", options);
        await noProductResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "PRODUCT_NOT_FOUND", options);
        await noReasonResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "VALIDATION_ERROR", options);
        await noStaffResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "STAFF_INVALID", options);
        Assert.Equal(before, await QuantityAsync(client, TestData.SdCardProductId));
    }

    // 棚卸 (絶対数量) と調整 (増減)、同じ id の再送は Duplicate
    [Fact]
    public async Task InventoryChangesAreIdempotent()
    {
        // Arrange
        var client = await factory.CreateAdminClientAsync();
        var before = (await client.GetJsonAsync<InventoryProductLevelsResponse>($"{ApiRoutes.Inventory}/{TestData.SdCardProductId}", options)).Levels.Single(static x => x.StoreId == TestData.BranchStoreId).Quantity;
        var request = new InventoryChangesRequest
        {
            Changes =
            [
                new InventoryChangesRequestChange { Id = Guid.NewGuid(), StoreId = TestData.BranchStoreId, ProductId = TestData.SdCardProductId, Type = InventoryChangeType.PhysicalCount, Quantity = before + 5m, StaffId = TestData.BranchCashierStaffId, OccurredAt = Now },
                new InventoryChangesRequestChange { Id = Guid.NewGuid(), StoreId = TestData.BranchStoreId, ProductId = TestData.SdCardProductId, Type = InventoryChangeType.Adjustment, Quantity = -1m, Reason = "破損", StaffId = TestData.BranchCashierStaffId, OccurredAt = Now }
            ]
        };

        // Act / Assert: 棚卸 (絶対数量) と調整 (増減)
        using var response = await client.PostJsonAsync($"{ApiRoutes.Inventory}/changes", request, options);
        var result = await response.ReadAsAsync<InventoryChangesResponse>(HttpStatusCode.OK, options);
        Assert.All(result.Results, x => Assert.Equal(InventoryChangeResultStatus.Created, x.Status));
        Assert.Equal(5m, result.Results[0].QuantityDelta);
        Assert.Equal(before + 5m, result.Results[0].QuantityAfter);
        Assert.Equal(before + 4m, result.Results[1].QuantityAfter);

        // Act / Assert: 同じ id の再送は Duplicate
        using var againResponse = await client.PostJsonAsync($"{ApiRoutes.Inventory}/changes", request, options);
        var again = await againResponse.ReadAsAsync<InventoryChangesResponse>(HttpStatusCode.OK, options);
        Assert.All(again.Results, x => Assert.Equal(InventoryChangeResultStatus.Duplicate, x.Status));
        Assert.Equal(before + 4m, again.Results[1].QuantityAfter);

        // Act / Assert: 在庫と変動の一覧
        var levels = await client.GetJsonAsync<InventoryLevelListResponse>($"{ApiRoutes.Inventory}?storeId={TestData.BranchStoreId}&productId={TestData.SdCardProductId}", options);
        Assert.Equal(before + 4m, Assert.Single(levels.Items).Quantity);
        var changes = await client.GetJsonAsync<InventoryChangeListResponse>($"{ApiRoutes.Inventory}/changes?storeId={TestData.BranchStoreId}&productId={TestData.SdCardProductId}", options);
        Assert.Equal(2, changes.Total);
        var negative = await client.GetJsonAsync<InventoryLevelListResponse>($"{ApiRoutes.Inventory}?negativeOnly=true", options);
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

    // 項目を null にして送る (アプリの JSON は null の項目を書かないので、JSON の木を書き換える)
    private async Task<HttpResponseMessage> PostWithNullAsync<T>(HttpClient client, string url, T request, Action<JsonObject> change)
    {
        var json = JsonSerializer.SerializeToNode(request, options)!.AsObject();
        change(json);
        using var content = new StringContent(json.ToJsonString(), Encoding.UTF8, "application/json");
        return await client.PostAsync(new Uri(url, UriKind.Relative), content, Token);
    }

    private async Task<decimal> QuantityAsync(HttpClient client, Guid productId) =>
        (await client.GetJsonAsync<InventoryProductLevelsResponse>($"{ApiRoutes.Inventory}/{productId}", options)).Levels.Single(static x => x.StoreId == TestData.MainStoreId).Quantity;

    // 販売の例: デジカメ (展示品 5%) + SD カード × 2 + 配送料、取引値引 1,000、ポイント 5,000 + カード 50,000 + 現金 25,100 (預り 30,000)
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
                new TransactionCreateRequestLine { Id = cameraLineId, LineNo = 1, ProductId = TestData.CameraProductId, ProductCode = "CAM-X100", ProductName = "デジタルカメラ X-100", CategoryId = TestData.CameraCategoryId, Kind = ProductKind.Goods, ListPrice = 80000m, UnitPrice = 80000m, Quantity = 1m, TaxRateId = TestData.StandardTaxRateId, TaxRate = 0.10m, TaxIncluded = true, PointRate = 0.10m, SerialNumbers = ["SN-0001234"] },
                new TransactionCreateRequestLine { Id = Guid.NewGuid(), LineNo = 2, ProductId = TestData.SdCardProductId, ProductCode = "SD-64", ProductName = "SD カード 64GB", CategoryId = TestData.AccessoryCategoryId, Kind = ProductKind.Goods, ListPrice = 2000m, UnitPrice = 2000m, Quantity = 2m, TaxRateId = TestData.StandardTaxRateId, TaxRate = 0.10m, TaxIncluded = true, PointRate = 0.01m },
                new TransactionCreateRequestLine { Id = Guid.NewGuid(), LineNo = 3, ProductId = TestData.DeliveryProductId, ProductCode = "SVC-DELIVERY", ProductName = "配送料", CategoryId = TestData.ServiceCategoryId, Kind = ProductKind.Service, ListPrice = 1100m, UnitPrice = 1100m, Quantity = 1m, TaxRateId = TestData.StandardTaxRateId, TaxRate = 0.10m, TaxIncluded = true, PointRate = 0m }
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

    // 端末 2 の計算済みの販売に、1 か所だけ手を加える
    private static TransactionCreateRequest CreateTerminal2SaleRequest(Guid shiftId, TransactionCalculateResponse calculation, Action<TransactionCreateRequest> change)
    {
        var sale = CreateSaleRequest(shiftId);
        sale.TerminalId = TestData.MainTerminal2Id;
        sale.ReceiptNo = "S001-02-000001";
        TransactionRequests.Apply(sale, calculation);
        change(sale);
        return sale;
    }

    // 返品: ポイント返還分 (−pointsRedeemed) をポイント、残りを現金で返金する
    private static TransactionCreateRequest CreateReturnRequest(Guid shiftId, Guid originalId, TransactionListResponseLine original, decimal quantity, string receiptNo)
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
