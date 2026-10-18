namespace Pos.Server;

using System.Data.Common;

using Microsoft.Extensions.DependencyInjection;

using Pos.Server.Accessors;
using Pos.Server.Models.Entity;
using Pos.Server.Models.Views;

using Smart.Data;

public sealed class AccessorTests : IClassFixture<TestApplicationFactory>
{
    private readonly TestApplicationFactory factory;

    public AccessorTests(TestApplicationFactory factory)
    {
        this.factory = factory;
    }

    private T Resolve<T>()
        where T : notnull
        => factory.Services.GetRequiredService<T>();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ValueTask<T> InTxAsync<T>(IDbProvider provider, Func<DbTransaction, ValueTask<T>> func) =>
        provider.UsingTxAsync(async (_, tx) =>
        {
            var result = await func(tx);
            await tx.CommitAsync(Token);
            return result;
        }, Token);

    // 楽観ロック: Version が一致する更新だけ通り、論理削除は一覧から消える
    [Fact]
    public async Task MasterUpdateUsesOptimisticLock()
    {
        // Arrange
        var accessor = Resolve<MasterAccessor>();
        var now = DateTime.UtcNow;
        var id = Guid.NewGuid();
        var code = $"L{id:N}"[..10];
        await accessor.InsertStoreAsync(new StoreEntity { Id = id, Code = code, Name = "楽観ロック", TimeZone = "Asia/Tokyo", IsActive = true, CreatedAt = now, UpdatedAt = now, Version = 1 }, Token);

        // Act / Assert: 版が合う更新だけが通る
        var updated = await accessor.UpdateStoreAsync(id, code, "更新 1", null, null, null, null, null, null, "Asia/Tokyo", true, now.AddSeconds(1), 1, Token);
        var conflicted = await accessor.UpdateStoreAsync(id, code, "更新 2", null, null, null, null, null, null, "Asia/Tokyo", true, now.AddSeconds(2), 1, Token);
        var stored = await accessor.QueryStoreAsync(id, Token);

        Assert.NotNull(updated);
        Assert.Equal("更新 1", updated.Name);
        Assert.Equal(2, updated.Version);
        Assert.Null(conflicted);
        Assert.NotNull(stored);
        Assert.Equal("更新 1", stored.Name);
        Assert.Equal(2, stored.Version);

        // Act / Assert: 差分同期は updatedSince 以降のものだけ
        var since = await accessor.QueryStoreListAsync(now.AddMilliseconds(500), true, StoreSort.Code, false, 100, 0, Token);
        Assert.Contains(since, x => x.Id == id);
        Assert.DoesNotContain(since, x => x.Id == TestData.MainStoreId);

        // Act / Assert: 削除は論理削除で、2 回目は 0 件
        Assert.Equal(1, await accessor.DeleteStoreAsync(id, now.AddSeconds(3), Token));
        Assert.Equal(0, await accessor.DeleteStoreAsync(id, now.AddSeconds(4), Token));
        Assert.DoesNotContain(await accessor.QueryStoreListAsync(null, false, StoreSort.Code, false, 100, 0, Token), x => x.Id == id);
        Assert.Contains(await accessor.QueryStoreListAsync(null, true, StoreSort.Code, false, 100, 0, Token), x => (x.Id == id) && x.IsDeleted);
    }

    // 在庫の差分同期は updatedSince より後の行だけを UpdatedAt の順に返す
    [Fact]
    public async Task InventoryLevelDeltaSync()
    {
        // Arrange
        var accessor = Resolve<InventoryAccessor>();

        // Act
        var all = await accessor.QueryLevelListAsync(null, null, null, false, DateTime.UnixEpoch, 1000, 0, Token);
        var none = await accessor.QueryLevelListAsync(null, null, null, false, DateTime.UtcNow.AddDays(1), 1000, 0, Token);

        // Assert
        Assert.NotEmpty(all);
        Assert.True(all.Zip(all.Skip(1)).All(static x => x.First.UpdatedAt <= x.Second.UpdatedAt));
        Assert.Empty(none);
    }

    // 商品の一覧はキーワード・部門・有効で絞り込め (部門の件数は一覧と合う)、ID の一覧でも引ける
    [Fact]
    public async Task ProductListFilters()
    {
        // Arrange
        var accessor = Resolve<ProductAccessor>();

        // Act / Assert: キーワード
        var byKeyword = await accessor.QueryListAsync(null, "%カメラ%", null, null, false, ProductSort.Code, false, 100, 0, Token);
        Assert.NotEmpty(byKeyword);
        Assert.All(byKeyword, x => Assert.Contains("カメラ", x.Name + x.Kana + x.Code, StringComparison.Ordinal));

        // Act / Assert: 部門 (件数は一覧と合う)
        var camera = await accessor.QueryByCodeAsync("CAM-X100", Token);
        Assert.NotNull(camera);
        var byCategory = await accessor.QueryListAsync(camera.CategoryId, null, null, null, false, ProductSort.Code, false, 100, 0, Token);
        Assert.Contains(byCategory, x => x.Id == camera.Id);
        Assert.Equal(await accessor.CountAsync(camera.CategoryId, null, null, null, false, Token), byCategory.Count);

        // Act / Assert: ID の一覧
        var byIds = await accessor.QueryListByIdsAsync([TestData.CameraProductId, TestData.SdCardProductId], Token);
        Assert.Equal(2, byIds.Count);

        // Act / Assert: 無効の商品はない (初期データ)
        var inactive = await accessor.QueryListAsync(null, null, false, null, false, ProductSort.Code, false, 100, 0, Token);
        Assert.Empty(inactive);
    }

    // 取消の更新は、完了した返品のある販売、精算したシフトの取引、締めた営業日の取引では行を返さない (検証のあとに先に通った返品・精算・締めを破らない。返品の取引は返品が付いていても取り消せる)
    [Fact]
    public async Task VoidUpdateRequiresNoReturnsOpenShiftAndOpenDay()
    {
        // Arrange
        var provider = Resolve<IDbProvider>();
        var shifts = Resolve<ShiftAccessor>();
        var transactions = Resolve<TransactionAccessor>();
        var closings = Resolve<DailyClosingAccessor>();
        var now = new DateTime(2026, 8, 3, 3, 0, 0, DateTimeKind.Utc);
        var openDate = new DateOnly(2026, 8, 3);
        var closedDate = new DateOnly(2026, 8, 4);
        var openShiftId = Guid.NewGuid();
        var closedShiftId = Guid.NewGuid();
        await shifts.InsertAsync(new ShiftEntity { Id = openShiftId, StoreId = TestData.BranchStoreId, TerminalId = TestData.BranchTerminalId, Status = ShiftStatus.Open, BusinessDate = openDate, OpenedAt = now, OpenedByStaffId = TestData.BranchCashierStaffId, OpeningCash = 0m, CreatedAt = now, UpdatedAt = now }, Token);
        await shifts.InsertAsync(new ShiftEntity { Id = closedShiftId, StoreId = TestData.BranchStoreId, TerminalId = TestData.BranchTerminalId, Status = ShiftStatus.Closed, BusinessDate = openDate, OpenedAt = now, OpenedByStaffId = TestData.BranchCashierStaffId, OpeningCash = 0m, ClosedAt = now, ClosedByStaffId = TestData.BranchCashierStaffId, CreatedAt = now, UpdatedAt = now }, Token);
        var returnedSaleId = await InsertBranchTransactionAsync(provider, transactions, TransactionType.Sale, openShiftId, openDate, null, "S002-01-000001", now);
        var returnId = await InsertBranchTransactionAsync(provider, transactions, TransactionType.Return, openShiftId, openDate, returnedSaleId, "S002-01-000002", now);
        await InsertBranchTransactionAsync(provider, transactions, TransactionType.Return, openShiftId, openDate, returnId, "S002-01-000005", now);
        var closedShiftSaleId = await InsertBranchTransactionAsync(provider, transactions, TransactionType.Sale, closedShiftId, openDate, null, "S002-01-000003", now);
        var closedDaySaleId = await InsertBranchTransactionAsync(provider, transactions, TransactionType.Sale, openShiftId, closedDate, null, "S002-01-000004", now);
        await InTxAsync(provider, tx => closings.InsertAsync(tx, new DailyClosingEntity { Id = Guid.NewGuid(), StoreId = TestData.BranchStoreId, BusinessDate = closedDate, ClosedAt = now, CreatedAt = now, UpdatedAt = now }, Token));

        // Act / Assert: 完了した返品のある販売は取り消せず、返品の取引は返品が付いていても取り消せ、返品を取り消すと販売も取り消せる
        Assert.Null(await VoidAsync(provider, transactions, returnedSaleId, now));
        Assert.Equal(TransactionStatus.Voided, (await VoidAsync(provider, transactions, returnId, now))?.Status);
        Assert.Equal(TransactionStatus.Voided, (await VoidAsync(provider, transactions, returnedSaleId, now))?.Status);

        // Act / Assert: 精算したシフトの取引と、締めた営業日の取引は取り消せない
        Assert.Null(await VoidAsync(provider, transactions, closedShiftSaleId, now));
        Assert.Null(await VoidAsync(provider, transactions, closedDaySaleId, now));
        Assert.Equal(TransactionStatus.Completed, (await transactions.QueryAsync(closedShiftSaleId, Token))?.Status);
        Assert.Equal(TransactionStatus.Completed, (await transactions.QueryAsync(closedDaySaleId, Token))?.Status);
    }

    // 開設 → 販売 (取引一式 + 在庫 + ポイント) → 集計 → 返品数量 → 取消 → 精算 → レポート
    [Fact]
    public async Task TransactionFlow()
    {
        // Arrange
        var provider = Resolve<IDbProvider>();
        var shifts = Resolve<ShiftAccessor>();
        var transactions = Resolve<TransactionAccessor>();
        var inventory = Resolve<InventoryAccessor>();
        var customers = Resolve<CustomerAccessor>();
        var masters = Resolve<MasterAccessor>();
        var reports = Resolve<ReportAccessor>();

        var now = new DateTime(2026, 9, 11, 3, 15, 0, DateTimeKind.Utc);
        var businessDate = new DateOnly(2026, 9, 11);
        var shiftId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var cameraLineId = Guid.NewGuid();
        var sdCardLineId = Guid.NewGuid();

        // Act / Assert: 開設 (端末に Open が 2 つは作れない)
        await shifts.InsertAsync(new ShiftEntity { Id = shiftId, StoreId = TestData.MainStoreId, TerminalId = TestData.MainTerminal2Id, Status = ShiftStatus.Open, BusinessDate = businessDate, OpenedAt = now, OpenedByStaffId = TestData.MainCashierStaffId, OpeningCash = 30000m, CreatedAt = now, UpdatedAt = now }, Token);
        await Assert.ThrowsAnyAsync<Exception>(async () => await shifts.InsertAsync(new ShiftEntity { Id = Guid.NewGuid(), StoreId = TestData.MainStoreId, TerminalId = TestData.MainTerminal2Id, Status = ShiftStatus.Open, BusinessDate = businessDate, OpenedAt = now, OpenedByStaffId = TestData.MainCashierStaffId, OpeningCash = 0m, CreatedAt = now, UpdatedAt = now }, Token));
        var current = await shifts.QueryCurrentAsync(TestData.MainTerminal2Id, Token);
        Assert.NotNull(current);
        Assert.Equal(shiftId, current.Id);

        // Act / Assert: 販売
        var sdBefore = (await inventory.QueryLevelListByProductAsync(TestData.SdCardProductId, Token)).Single(x => x.StoreId == TestData.MainStoreId).Quantity;
        await provider.UsingTxAsync(async (_, tx) =>
        {
            await transactions.InsertAsync(tx, new TransactionEntity
            {
                Id = transactionId, Type = TransactionType.Sale, Status = TransactionStatus.Completed,
                StoreId = TestData.MainStoreId, TerminalId = TestData.MainTerminal2Id, StaffId = TestData.MainCashierStaffId, ShiftId = shiftId,
                CustomerId = TestData.Customer1Id, ReceiptNo = "S001-02-000001", BusinessDate = businessDate, TransactedAt = now,
                Subtotal = 84000m, DiscountTotal = 4000m, NetSubtotal = 80000m, TaxTotal = 7272m, Total = 80000m, TenderedTotal = 80000m, ChangeAmount = 0m,
                PointsEarned = 7640, PointsRedeemed = 0, PointsBalanceAfter = null, CreatedAt = now, UpdatedAt = now
            }, Token);
            await transactions.InsertLineAsync(tx, new TransactionLineEntity { Id = cameraLineId, TransactionId = transactionId, LineNo = 1, ProductId = TestData.CameraProductId, ProductCode = "CAM-X100", ProductName = "デジタルカメラ X-100", CategoryId = TestData.CameraCategoryId, Kind = ProductKind.Goods, ListPrice = 80000m, UnitPrice = 80000m, Quantity = 1m, TaxRateId = TestData.StandardTaxRateId, TaxRate = 0.10m, TaxIncluded = true, PointRate = 0.10m, Amount = 80000m, DiscountAmount = 4000m, AllocatedDiscountAmount = 0m, NetAmount = 76000m, PointsRedeemed = 0, PointsEarned = 7600 }, Token);
            await transactions.InsertLineAsync(tx, new TransactionLineEntity { Id = sdCardLineId, TransactionId = transactionId, LineNo = 2, ProductId = TestData.SdCardProductId, ProductCode = "SD-64", ProductName = "SD カード 64GB", CategoryId = TestData.AccessoryCategoryId, Kind = ProductKind.Goods, ListPrice = 2000m, UnitPrice = 2000m, Quantity = 2m, TaxRateId = TestData.StandardTaxRateId, TaxRate = 0.10m, TaxIncluded = true, PointRate = 0.01m, Amount = 4000m, DiscountAmount = 0m, AllocatedDiscountAmount = 0m, NetAmount = 4000m, PointsRedeemed = 0, PointsEarned = 40 }, Token);
            await transactions.InsertLineSerialAsync(tx, new TransactionLineSerialEntity { TransactionLineId = cameraLineId, SerialNumber = "SN-0001234" }, Token);
            await transactions.InsertDiscountAsync(tx, new TransactionDiscountEntity { Id = Guid.NewGuid(), TransactionId = transactionId, LineId = cameraLineId, DiscountId = TestData.DisplayDiscountId, SortNo = 1, Name = "展示品 5%", Type = DiscountType.Percent, Value = 0.05m, Amount = 4000m }, Token);
            await transactions.InsertTaxSummaryAsync(tx, new TransactionTaxSummaryEntity { TransactionId = transactionId, TaxRateId = TestData.StandardTaxRateId, TaxIncluded = true, Rate = 0.10m, TaxableAmount = 80000m, TaxAmount = 7272m }, Token);
            await transactions.InsertPaymentAsync(tx, new TransactionPaymentEntity { Id = Guid.NewGuid(), TransactionId = transactionId, SeqNo = 1, PaymentMethodId = TestData.CashPaymentMethodId, Kind = PaymentKind.Cash, Amount = 80000m, TenderedAmount = 80000m }, Token);
            await transactions.InsertDeliveryAsync(tx, new TransactionDeliveryEntity { TransactionId = transactionId, RecipientName = "山田 太郎", Address = "東京都", RequestedDate = new DateOnly(2026, 9, 14), TimeSlot = "14-16" }, Token);

            var after = await inventory.AddQuantityAsync(tx, TestData.MainStoreId, TestData.SdCardProductId, -2m, now, Token);
            await inventory.InsertChangeAsync(tx, new InventoryChangeEntity { Id = Guid.NewGuid(), StoreId = TestData.MainStoreId, ProductId = TestData.SdCardProductId, Type = InventoryChangeType.Sale, QuantityDelta = -2m, QuantityAfter = after, ReferenceType = "Transaction", ReferenceId = transactionId, ReferenceLineId = sdCardLineId, StaffId = TestData.MainCashierStaffId, OccurredAt = now, CreatedAt = now }, Token);

            var balance = await customers.AddPointsAsync(tx, TestData.Customer1Id, 7640, now, Token);
            await customers.InsertPointHistoryAsync(tx, new PointHistoryEntity { Id = Guid.NewGuid(), CustomerId = TestData.Customer1Id, Type = PointHistoryType.Earn, Points = 7640, BalanceAfter = balance, TransactionId = transactionId, OccurredAt = now, CreatedAt = now }, Token);
            Assert.Equal(6000 + 7640, balance);

            await masters.UpdateTerminalLastReceiptSeqAsync(tx, TestData.MainTerminal2Id, 1, now, Token);
            await tx.CommitAsync(Token);
        }, Token);

        // Act / Assert: 読み戻し
        var stored = await transactions.QueryAsync(transactionId, Token);
        Assert.NotNull(stored);
        Assert.Equal(TransactionType.Sale, stored.Type);
        Assert.Equal(businessDate, stored.BusinessDate);
        Assert.Equal(now, stored.TransactedAt);
        Assert.Equal(2, (await transactions.QueryLineListAsync(transactionId, Token)).Count);
        Assert.Single(await transactions.QueryLineSerialListAsync(transactionId, Token));
        Assert.Single(await transactions.QueryDiscountListAsync(transactionId, Token));
        Assert.Single(await transactions.QueryTaxSummaryListAsync(transactionId, Token));
        Assert.Equal(PaymentKind.Cash, (await transactions.QueryPaymentListAsync(transactionId, Token)).Single().Kind);
        Assert.Equal(new DateOnly(2026, 9, 14), (await transactions.QueryDeliveryAsync(transactionId, Token))!.RequestedDate);
        Assert.Equal(sdBefore - 2m, (await inventory.QueryLevelListByProductAsync(TestData.SdCardProductId, Token)).Single(x => x.StoreId == TestData.MainStoreId).Quantity);
        Assert.Equal(1, (await masters.QueryTerminalAsync(TestData.MainTerminal2Id, Token))!.LastReceiptSeq);
        Assert.Equal(1, await transactions.CountAsync(null, null, null, shiftId, null, null, null, TransactionType.Sale, TransactionStatus.Completed, null, Token));
        Assert.Single(await transactions.QueryListAsync(TestData.MainStoreId, null, null, null, TestData.Customer1Id, businessDate, businessDate, null, null, null, TransactionSort.TransactedAt, true, 10, 0, Token));

        // Act / Assert: 集計
        var totals = await shifts.QuerySummaryAsync(shiftId, Token);
        Assert.NotNull(totals);
        Assert.Equal(new ShiftTotalsView(80000m, 0m, 0m, 0m, 0m, 0m, 1, 0, 0, 80000m, 0m), totals);
        var byPayment = Assert.Single(await shifts.QueryPaymentMethodSummaryAsync(shiftId, Token));
        Assert.Equal(new PaymentMethodTotalView(TestData.CashPaymentMethodId, "現金", PaymentKind.Cash, 80000m, 1, 0m, 0), byPayment);
        var byTax = Assert.Single(await shifts.QueryTaxRateSummaryAsync(shiftId, Token));
        Assert.Equal(7272m, byTax.TaxAmount);
        Assert.Equal(2, (await shifts.QueryCategorySummaryAsync(shiftId, Token)).Count);
        Assert.Equal(new PointTotalsView(7640, 0), await shifts.QueryPointSummaryAsync(shiftId, Token));

        // Act / Assert: 返品数量 (超過は 0 件)
        Assert.Equal(1, await InTxAsync(provider, tx => transactions.AddReturnedQuantityAsync(tx, sdCardLineId, 1m, Token)));
        Assert.Equal(0, await InTxAsync(provider, tx => transactions.AddReturnedQuantityAsync(tx, sdCardLineId, 2m, Token)));
        Assert.Equal(1m, (await transactions.QueryLineListAsync(transactionId, Token))[1].ReturnedQuantity);
        Assert.Equal(0, await transactions.CountReturnsAsync(transactionId, Token));

        // Act / Assert: レポート
        var byDay = await reports.QuerySalesSummaryAsync(TestData.MainStoreId, businessDate, businessDate, SalesSummaryGroupBy.Day, Token);
        var day = Assert.Single(byDay);
        Assert.Equal("2026-09-11", day.GroupKey);
        Assert.Equal(1, day.TransactionCount);
        Assert.Equal(80000m, day.NetSales);
        Assert.Equal(4000m, day.DiscountTotal);
        Assert.Equal("12", Assert.Single(await reports.QuerySalesSummaryByHourAsync(TestData.MainStoreId, businessDate, businessDate, "+9 hours", Token)).GroupKey);
        Assert.Equal("現金", Assert.Single(await reports.QuerySalesSummaryByPaymentMethodAsync(TestData.MainStoreId, businessDate, businessDate, Token)).GroupLabel);
        Assert.Equal(7272m, Assert.Single(await reports.QuerySalesSummaryByTaxRateAsync(TestData.MainStoreId, businessDate, businessDate, Token)).TaxAmount);
        Assert.Equal(2, (await reports.QuerySalesSummaryByCategoryAsync(TestData.MainStoreId, businessDate, businessDate, Token)).Count);
        Assert.Equal("本店 レジ 2", Assert.Single(await reports.QuerySalesSummaryAsync(null, businessDate, businessDate, SalesSummaryGroupBy.Terminal, Token)).GroupLabel);
        var products = await reports.QueryProductSalesSummaryAsync(null, businessDate, businessDate, null, ProductSalesSort.NetSales, 10, Token);
        Assert.Equal(2, products.Count);
        Assert.Equal("CAM-X100", products[0].ProductCode);
        Assert.Equal(76000m - 60000m, products[0].GrossProfit);

        // Act / Assert: 取消 → 集計から外れる
        var voided = await InTxAsync(provider, tx => transactions.UpdateVoidedAsync(tx, transactionId, now.AddMinutes(10), TestData.ManagerStaffId, "誤操作", now.AddMinutes(10), Token));
        Assert.Equal(TransactionStatus.Voided, voided?.Status);
        Assert.Equal("誤操作", voided?.VoidReason);
        Assert.Null(await InTxAsync(provider, tx => transactions.UpdateVoidedAsync(tx, transactionId, now.AddMinutes(11), TestData.ManagerStaffId, "再送", now.AddMinutes(11), Token)));
        Assert.Equal(TransactionStatus.Voided, (await transactions.QueryAsync(transactionId, Token))!.Status);
        var totalsAfterVoid = await shifts.QuerySummaryAsync(shiftId, Token);
        Assert.Equal(new ShiftTotalsView(0m, 0m, 0m, 0m, 0m, 0m, 0, 0, 1, 0m, 0m), totalsAfterVoid);

        // Act / Assert: 入出金と精算
        await shifts.InsertCashEventAsync(new CashEventEntity { Id = Guid.NewGuid(), ShiftId = shiftId, Type = CashEventType.PaidOut, Amount = 10000m, Reason = "両替", StaffId = TestData.MainCashierStaffId, OccurredAt = now.AddMinutes(20), CreatedAt = now.AddMinutes(20) }, Token);
        Assert.Equal(1, await shifts.CountCashEventsAsync(shiftId, Token));
        var closeTotals = (await shifts.QuerySummaryAsync(shiftId, Token))!;
        Assert.Equal(10000m, closeTotals.PaidOut);
        await provider.UsingTxAsync(async (_, tx) =>
        {
            var updated = await shifts.UpdateClosedAsync(
                tx,
                shiftId,
                now.AddHours(8),
                TestData.MainCashierStaffId,
                19900m,
                20000m,
                -100m,
                closeTotals.CashSales,
                closeTotals.CashReturns,
                closeTotals.PaidIn,
                closeTotals.PaidOut,
                closeTotals.DepositCashIn,
                closeTotals.DepositCashOut,
                closeTotals.SalesCount,
                closeTotals.ReturnCount,
                closeTotals.VoidCount,
                closeTotals.SalesTotal,
                closeTotals.ReturnsTotal,
                "精算",
                now.AddHours(8),
                Token);
            Assert.Equal(ShiftStatus.Closed, updated?.Status);
            Assert.Equal(-100m, updated?.Difference);
            await shifts.InsertDenominationAsync(tx, new ShiftDenominationEntity { ShiftId = shiftId, Denomination = 10000, Count = 1 }, Token);
            await tx.CommitAsync(Token);
        }, Token);
        var closed = await shifts.QueryAsync(shiftId, Token);
        Assert.NotNull(closed);
        Assert.Equal(ShiftStatus.Closed, closed.Status);
        Assert.Equal(-100m, closed.Difference);
        Assert.Equal(1, closed.VoidCount);
        Assert.Single(await shifts.QueryDenominationListAsync(shiftId, Token));
        Assert.Null(await shifts.QueryCurrentAsync(TestData.MainTerminal2Id, Token));
        Assert.Equal(1, await shifts.CountAsync(TestData.MainStoreId, null, ShiftStatus.Closed, businessDate, businessDate, Token));
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    // 取消の条件だけを確かめる支店の取引 (明細と支払は持たない)
    private static async ValueTask<Guid> InsertBranchTransactionAsync(IDbProvider provider, TransactionAccessor transactions, TransactionType type, Guid shiftId, DateOnly businessDate, Guid? originalTransactionId, string receiptNo, DateTime now)
    {
        var id = Guid.NewGuid();
        await InTxAsync(provider, tx => transactions.InsertAsync(tx, new TransactionEntity
        {
            Id = id, Type = type, Status = TransactionStatus.Completed,
            StoreId = TestData.BranchStoreId, TerminalId = TestData.BranchTerminalId, StaffId = TestData.BranchCashierStaffId, ShiftId = shiftId,
            ReceiptNo = receiptNo, BusinessDate = businessDate, TransactedAt = now, OriginalTransactionId = originalTransactionId, CreatedAt = now, UpdatedAt = now
        }, Token));
        return id;
    }

    private static ValueTask<TransactionEntity?> VoidAsync(IDbProvider provider, TransactionAccessor transactions, Guid id, DateTime now) =>
        InTxAsync(provider, tx => transactions.UpdateVoidedAsync(tx, id, now, TestData.BranchCashierStaffId, "取消", now, Token));
}
