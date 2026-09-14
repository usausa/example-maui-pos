namespace Pos.Server;

using System.Data.Common;

using Microsoft.Extensions.DependencyInjection;

using Pos.Server.Accessors;
using Pos.Server.Models.Entity;
using Pos.Server.Models.Parameters;
using Pos.Server.Models.Views;
using Pos.Server.Services;

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

    private static ValueTask<int> InTxAsync(IDbProvider provider, Func<DbTransaction, ValueTask<int>> func) =>
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
        var accessor = Resolve<MasterAccessor>();
        var now = DateTime.UtcNow;
        var id = Guid.NewGuid();
        var code = $"L{id:N}"[..10];
        await accessor.InsertStoreAsync(new StoreEntity { Id = id, Code = code, Name = "楽観ロック", TimeZone = "Asia/Tokyo", IsActive = true, CreatedAt = now, UpdatedAt = now, Version = 1 }, Token);

        var updated = await accessor.UpdateStoreAsync(id, code, "更新 1", null, null, null, null, null, null, "Asia/Tokyo", true, now.AddSeconds(1), 1, Token);
        var conflicted = await accessor.UpdateStoreAsync(id, code, "更新 2", null, null, null, null, null, null, "Asia/Tokyo", true, now.AddSeconds(2), 1, Token);
        var stored = await accessor.QueryStoreAsync(id, Token);

        Assert.Equal(1, updated);
        Assert.Equal(0, conflicted);
        Assert.NotNull(stored);
        Assert.Equal("更新 1", stored.Name);
        Assert.Equal(2, stored.Version);

        // 差分同期: updatedSince 以降のものだけ
        var since = await accessor.QueryStoreListAsync(now.AddMilliseconds(500), true, "UpdatedAt, Id", 100, 0, Token);
        Assert.Contains(since, x => x.Id == id);
        Assert.DoesNotContain(since, x => x.Id == InitialData.MainStoreId);

        Assert.Equal(1, await accessor.DeleteStoreAsync(id, now.AddSeconds(3), Token));
        Assert.Equal(0, await accessor.DeleteStoreAsync(id, now.AddSeconds(4), Token));
        Assert.DoesNotContain(await accessor.QueryStoreListAsync(null, false, "Code", 100, 0, Token), x => x.Id == id);
        Assert.Contains(await accessor.QueryStoreListAsync(null, true, "Code", 100, 0, Token), x => (x.Id == id) && x.IsDeleted);
    }

    [Fact]
    public async Task ProductListFilters()
    {
        var accessor = Resolve<ProductAccessor>();

        var byKeyword = await accessor.QueryListAsync(null, "%カメラ%", null, null, false, "Code", 100, 0, Token);
        Assert.NotEmpty(byKeyword);
        Assert.All(byKeyword, x => Assert.Contains("カメラ", x.Name + x.Kana + x.Code, StringComparison.Ordinal));

        var camera = await accessor.QueryByCodeAsync("CAM-X100", Token);
        Assert.NotNull(camera);
        var byCategory = await accessor.QueryListAsync(camera.CategoryId, null, null, null, false, "Code", 100, 0, Token);
        Assert.Contains(byCategory, x => x.Id == camera.Id);
        Assert.Equal(await accessor.CountAsync(camera.CategoryId, null, null, null, false, Token), byCategory.Count);

        var byIds = await accessor.QueryByIdsAsync([InitialData.CameraProductId, InitialData.SdCardProductId], Token);
        Assert.Equal(2, byIds.Count);

        var inactive = await accessor.QueryListAsync(null, null, false, null, false, "Code", 100, 0, Token);
        Assert.Empty(inactive);
    }

    // 開設 → 販売 (取引一式 + 在庫 + ポイント) → 集計 → 返品数量 → 取消 → 精算 → レポート
    [Fact]
    public async Task TransactionFlow()
    {
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

        // 開設 (端末に Open が 2 つは作れない)
        await shifts.InsertAsync(new ShiftEntity { Id = shiftId, StoreId = InitialData.MainStoreId, TerminalId = InitialData.MainTerminal2Id, Status = ShiftStatus.Open, BusinessDate = businessDate, OpenedAt = now, OpenedByStaffId = InitialData.MainCashierStaffId, OpeningCash = 30000m, CreatedAt = now, UpdatedAt = now }, Token);
        await Assert.ThrowsAnyAsync<Exception>(async () => await shifts.InsertAsync(new ShiftEntity { Id = Guid.NewGuid(), StoreId = InitialData.MainStoreId, TerminalId = InitialData.MainTerminal2Id, Status = ShiftStatus.Open, BusinessDate = businessDate, OpenedAt = now, OpenedByStaffId = InitialData.MainCashierStaffId, OpeningCash = 0m, CreatedAt = now, UpdatedAt = now }, Token));
        var current = await shifts.QueryCurrentAsync(InitialData.MainTerminal2Id, Token);
        Assert.NotNull(current);
        Assert.Equal(shiftId, current.Id);

        // 販売
        var sdBefore = (await inventory.QueryLevelsByProductAsync(InitialData.SdCardProductId, Token)).Single(x => x.StoreId == InitialData.MainStoreId).Quantity;
        await provider.UsingTxAsync(async (_, tx) =>
        {
            await transactions.InsertAsync(tx, new TransactionEntity
            {
                Id = transactionId, Type = TransactionType.Sale, Status = TransactionStatus.Completed,
                StoreId = InitialData.MainStoreId, TerminalId = InitialData.MainTerminal2Id, StaffId = InitialData.MainCashierStaffId, ShiftId = shiftId,
                CustomerId = InitialData.Customer1Id, ReceiptNo = "S001-02-000001", BusinessDate = businessDate, TransactedAt = now,
                Subtotal = 84000m, DiscountTotal = 4000m, NetSubtotal = 80000m, TaxTotal = 7272m, Total = 80000m, TenderedTotal = 80000m, ChangeAmount = 0m,
                PointsEarned = 7640, PointsRedeemed = 0, PointsBalanceAfter = null, CreatedAt = now, UpdatedAt = now
            }, Token);
            await transactions.InsertLineAsync(tx, new TransactionLineEntity { Id = cameraLineId, TransactionId = transactionId, LineNo = 1, ProductId = InitialData.CameraProductId, ProductCode = "CAM-X100", ProductName = "デジタルカメラ X-100", CategoryId = Guid.Parse("00000000-0000-0000-0004-000000000015"), Kind = ProductKind.Goods, ListPrice = 80000m, UnitPrice = 80000m, Quantity = 1m, TaxRateId = InitialData.StandardTaxRateId, TaxRate = 0.10m, TaxIncluded = true, PointRate = 0.10m, Amount = 80000m, DiscountAmount = 4000m, AllocatedDiscountAmount = 0m, NetAmount = 76000m, PointsRedeemed = 0, PointsEarned = 7600 }, Token);
            await transactions.InsertLineAsync(tx, new TransactionLineEntity { Id = sdCardLineId, TransactionId = transactionId, LineNo = 2, ProductId = InitialData.SdCardProductId, ProductCode = "SD-64", ProductName = "SD カード 64GB", CategoryId = Guid.Parse("00000000-0000-0000-0004-000000000017"), Kind = ProductKind.Goods, ListPrice = 2000m, UnitPrice = 2000m, Quantity = 2m, TaxRateId = InitialData.StandardTaxRateId, TaxRate = 0.10m, TaxIncluded = true, PointRate = 0.01m, Amount = 4000m, DiscountAmount = 0m, AllocatedDiscountAmount = 0m, NetAmount = 4000m, PointsRedeemed = 0, PointsEarned = 40 }, Token);
            await transactions.InsertLineSerialAsync(tx, new TransactionLineSerialEntity { TransactionLineId = cameraLineId, SerialNumber = "SN-0001234" }, Token);
            await transactions.InsertDiscountAsync(tx, new TransactionDiscountEntity { Id = Guid.NewGuid(), TransactionId = transactionId, LineId = cameraLineId, DiscountId = InitialData.DisplayDiscountId, SortNo = 1, Name = "展示品 5%", Type = DiscountType.Percent, Value = 0.05m, Amount = 4000m }, Token);
            await transactions.InsertTaxSummaryAsync(tx, new TransactionTaxSummaryEntity { TransactionId = transactionId, TaxRateId = InitialData.StandardTaxRateId, TaxIncluded = true, Rate = 0.10m, TaxableAmount = 80000m, TaxAmount = 7272m }, Token);
            await transactions.InsertPaymentAsync(tx, new TransactionPaymentEntity { Id = Guid.NewGuid(), TransactionId = transactionId, SeqNo = 1, PaymentMethodId = InitialData.CashPaymentMethodId, Kind = PaymentKind.Cash, Amount = 80000m, TenderedAmount = 80000m }, Token);
            await transactions.InsertDeliveryAsync(tx, new TransactionDeliveryEntity { TransactionId = transactionId, RecipientName = "山田 太郎", Address = "東京都", RequestedDate = new DateOnly(2026, 9, 14), TimeSlot = "14-16" }, Token);

            var after = await inventory.AddQuantityAsync(tx, InitialData.MainStoreId, InitialData.SdCardProductId, -2m, now, Token);
            await inventory.InsertChangeAsync(tx, new InventoryChangeEntity { Id = Guid.NewGuid(), StoreId = InitialData.MainStoreId, ProductId = InitialData.SdCardProductId, Type = InventoryChangeType.Sale, QuantityDelta = -2m, QuantityAfter = after, ReferenceType = "Transaction", ReferenceId = transactionId, ReferenceLineId = sdCardLineId, StaffId = InitialData.MainCashierStaffId, OccurredAt = now, CreatedAt = now }, Token);

            var balance = await customers.AddPointsAsync(tx, InitialData.Customer1Id, 7640, now, Token);
            await customers.InsertPointHistoryAsync(tx, new PointHistoryEntity { Id = Guid.NewGuid(), CustomerId = InitialData.Customer1Id, Type = PointHistoryType.Earn, Points = 7640, BalanceAfter = balance, TransactionId = transactionId, OccurredAt = now, CreatedAt = now }, Token);
            Assert.Equal(6000 + 7640, balance);

            await masters.UpdateTerminalLastReceiptSeqAsync(tx, InitialData.MainTerminal2Id, 1, now, Token);
            await tx.CommitAsync(Token);
        }, Token);

        // 読み戻し
        var stored = await transactions.QueryAsync(transactionId, Token);
        Assert.NotNull(stored);
        Assert.Equal(TransactionType.Sale, stored.Type);
        Assert.Equal(businessDate, stored.BusinessDate);
        Assert.Equal(now, stored.TransactedAt);
        Assert.Equal(2, (await transactions.QueryLinesAsync(transactionId, Token)).Count);
        Assert.Single(await transactions.QueryLineSerialsAsync(transactionId, Token));
        Assert.Single(await transactions.QueryDiscountsAsync(transactionId, Token));
        Assert.Single(await transactions.QueryTaxSummariesAsync(transactionId, Token));
        Assert.Equal(PaymentKind.Cash, (await transactions.QueryPaymentsAsync(transactionId, Token)).Single().Kind);
        Assert.Equal(new DateOnly(2026, 9, 14), (await transactions.QueryDeliveryAsync(transactionId, Token))!.RequestedDate);
        Assert.Equal(sdBefore - 2m, (await inventory.QueryLevelsByProductAsync(InitialData.SdCardProductId, Token)).Single(x => x.StoreId == InitialData.MainStoreId).Quantity);
        Assert.Equal(1, (await masters.QueryTerminalAsync(InitialData.MainTerminal2Id, Token))!.LastReceiptSeq);
        Assert.Equal(1, await transactions.CountAsync(null, null, null, shiftId, null, null, null, TransactionType.Sale, TransactionStatus.Completed, Token));
        Assert.Single(await transactions.QueryListAsync(InitialData.MainStoreId, null, null, null, InitialData.Customer1Id, businessDate, businessDate, null, null, "TransactedAt DESC", 10, 0, Token));

        // 集計
        var totals = await shifts.QueryTotalsAsync(shiftId, Token);
        Assert.NotNull(totals);
        Assert.Equal(new ShiftTotals(80000m, 0m, 0m, 0m, 1, 0, 0, 80000m, 0m), totals);
        var byPayment = Assert.Single(await shifts.QueryPaymentMethodTotalsAsync(shiftId, Token));
        Assert.Equal(new PaymentMethodTotal(InitialData.CashPaymentMethodId, "現金", PaymentKind.Cash, 80000m, 1, 0m, 0), byPayment);
        var byTax = Assert.Single(await shifts.QueryTaxRateTotalsAsync(shiftId, Token));
        Assert.Equal(7272m, byTax.TaxAmount);
        Assert.Equal(2, (await shifts.QueryCategoryTotalsAsync(shiftId, Token)).Count);
        Assert.Equal(new PointTotals(7640, 0), await shifts.QueryPointTotalsAsync(shiftId, Token));

        // 返品数量 (超過は 0 件)
        Assert.Equal(1, await InTxAsync(provider, tx => transactions.AddReturnedQuantityAsync(tx, sdCardLineId, 1m, Token)));
        Assert.Equal(0, await InTxAsync(provider, tx => transactions.AddReturnedQuantityAsync(tx, sdCardLineId, 2m, Token)));
        Assert.Equal(1m, (await transactions.QueryLinesAsync(transactionId, Token))[1].ReturnedQuantity);
        Assert.Equal(0, await transactions.CountReturnsAsync(transactionId, Token));

        // レポート
        var byDay = await reports.QuerySalesSummaryAsync(InitialData.MainStoreId, businessDate, businessDate, SalesSummaryGroupBy.Day, Token);
        var day = Assert.Single(byDay);
        Assert.Equal("2026-09-11", day.GroupKey);
        Assert.Equal(1, day.TransactionCount);
        Assert.Equal(80000m, day.NetSales);
        Assert.Equal(4000m, day.DiscountTotal);
        Assert.Equal("12", Assert.Single(await reports.QuerySalesSummaryByHourAsync(InitialData.MainStoreId, businessDate, businessDate, "+9 hours", Token)).GroupKey);
        Assert.Equal("現金", Assert.Single(await reports.QuerySalesSummaryByPaymentMethodAsync(InitialData.MainStoreId, businessDate, businessDate, Token)).GroupLabel);
        Assert.Equal(7272m, Assert.Single(await reports.QuerySalesSummaryByTaxRateAsync(InitialData.MainStoreId, businessDate, businessDate, Token)).TaxAmount);
        Assert.Equal(2, (await reports.QuerySalesSummaryByCategoryAsync(InitialData.MainStoreId, businessDate, businessDate, Token)).Count);
        Assert.Equal("本店 レジ 2", Assert.Single(await reports.QuerySalesSummaryAsync(null, businessDate, businessDate, SalesSummaryGroupBy.Terminal, Token)).GroupLabel);
        var products = await reports.QueryProductSalesAsync(null, businessDate, businessDate, null, ProductSalesSort.NetSales, 10, Token);
        Assert.Equal(2, products.Count);
        Assert.Equal("CAM-X100", products[0].ProductCode);
        Assert.Equal(76000m - 60000m, products[0].GrossProfit);

        // 取消 → 集計から外れる
        Assert.Equal(1, await InTxAsync(provider, tx => transactions.VoidAsync(tx, transactionId, now.AddMinutes(10), InitialData.ManagerStaffId, "誤操作", now.AddMinutes(10), Token)));
        Assert.Equal(TransactionStatus.Voided, (await transactions.QueryAsync(transactionId, Token))!.Status);
        var totalsAfterVoid = await shifts.QueryTotalsAsync(shiftId, Token);
        Assert.Equal(new ShiftTotals(0m, 0m, 0m, 0m, 0, 0, 1, 0m, 0m), totalsAfterVoid);

        // 入出金と精算
        await shifts.InsertCashEventAsync(new CashEventEntity { Id = Guid.NewGuid(), ShiftId = shiftId, Type = CashEventType.PaidOut, Amount = 10000m, Reason = "両替", StaffId = InitialData.MainCashierStaffId, OccurredAt = now.AddMinutes(20), CreatedAt = now.AddMinutes(20) }, Token);
        Assert.Equal(1, await shifts.CountCashEventsAsync(shiftId, Token));
        var closeTotals = (await shifts.QueryTotalsAsync(shiftId, Token))!;
        Assert.Equal(10000m, closeTotals.PaidOut);
        await provider.UsingTxAsync(async (_, tx) =>
        {
            Assert.Equal(1, await shifts.CloseAsync(tx, shiftId, now.AddHours(8), InitialData.MainCashierStaffId, 19900m, 20000m, -100m, closeTotals, "精算", now.AddHours(8), Token));
            await shifts.InsertDenominationAsync(tx, new ShiftDenominationEntity { ShiftId = shiftId, Denomination = 10000, Count = 1 }, Token);
            await tx.CommitAsync(Token);
        }, Token);
        var closed = await shifts.QueryAsync(shiftId, Token);
        Assert.NotNull(closed);
        Assert.Equal(ShiftStatus.Closed, closed.Status);
        Assert.Equal(-100m, closed.Difference);
        Assert.Equal(1, closed.VoidCount);
        Assert.Single(await shifts.QueryDenominationsAsync(shiftId, Token));
        Assert.Null(await shifts.QueryCurrentAsync(InitialData.MainTerminal2Id, Token));
        Assert.Equal(1, await shifts.CountAsync(InitialData.MainStoreId, null, ShiftStatus.Closed, businessDate, businessDate, Token));
    }
}
