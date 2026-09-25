namespace Pos.Server.Services;

using Pos.Server.Accessors;
using Pos.Server.Models;
using Pos.Server.Models.Entity;
using Pos.Server.Models.Parameters;
using Pos.Server.Models.Views;

public enum ShiftResultStatus
{
    Success,
    // 同じ id の再送 (登録済みを返す)
    Existing,
    NotFound,
    // 同じ id で内容が異なる
    DuplicateMismatch,
    // 端末に開設中のシフトがある
    TerminalHasOpenShift,
    // 精算済み
    Closed
}

// 開設・精算の結果
public sealed record ShiftResult(ShiftResultStatus Status, ShiftDetailView? Detail = null);

public enum CashEventResultStatus
{
    Success,
    Existing,
    DuplicateMismatch,
    ShiftNotFound,
    ShiftClosed
}

// 入出金の結果
public sealed record CashEventResult(CashEventResultStatus Status, CashEventEntity? Entity = null);

// レジ開閉・現金管理。Open 中の集計は取引から都度求め、精算時に Shifts へ確定する
public sealed class ShiftService
{
    private readonly TimeProvider timeProvider;
    private readonly IDbProvider provider;
    private readonly IDialect dialect;
    private readonly MasterAccessor masterAccessor;
    private readonly ShiftAccessor shiftAccessor;
    private readonly ChangeNotificationService changeNotification;

    public ShiftService(
        TimeProvider timeProvider,
        IDbProvider provider,
        IDialect dialect,
        MasterAccessor masterAccessor,
        ShiftAccessor shiftAccessor,
        ChangeNotificationService changeNotification)
    {
        this.timeProvider = timeProvider;
        this.provider = provider;
        this.dialect = dialect;
        this.masterAccessor = masterAccessor;
        this.shiftAccessor = shiftAccessor;
        this.changeNotification = changeNotification;
    }

    // openingCash + cashSales − cashReturns + paidIn − paidOut
    public static decimal ExpectedCash(decimal openingCash, ShiftTotalsView totals)
    {
        return openingCash + totals.CashSales - totals.CashReturns + totals.PaidIn - totals.PaidOut;
    }

    //--------------------------------------------------------------------------------
    // Query
    //--------------------------------------------------------------------------------

    public ValueTask<ShiftEntity?> QueryAsync(Guid id, CancellationToken cancellationToken) =>
        shiftAccessor.QueryAsync(id, cancellationToken);

    public async ValueTask<ShiftDetailView?> QueryDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await shiftAccessor.QueryAsync(id, cancellationToken);
        return entity is null ? null : await LoadDetailAsync(entity, cancellationToken);
    }

    // 端末の開設中シフト (なければ null)
    public async ValueTask<ShiftDetailView?> QueryCurrentAsync(Guid terminalId, CancellationToken cancellationToken)
    {
        var entity = await shiftAccessor.QueryCurrentAsync(terminalId, cancellationToken);
        return entity is null ? null : await LoadDetailAsync(entity, cancellationToken);
    }

    public async ValueTask<PagedResult<ShiftEntity>> QueryPageAsync(ShiftQueryParameter parameter, CancellationToken cancellationToken)
    {
        var total = await shiftAccessor.CountAsync(parameter.StoreId, parameter.TerminalId, parameter.Status, parameter.From, parameter.To, cancellationToken);
        var items = await shiftAccessor.QueryListAsync(parameter.StoreId, parameter.TerminalId, parameter.Status, parameter.From, parameter.To, parameter.Sort, parameter.Desc, parameter.Size, parameter.Page * parameter.Size, cancellationToken);
        return new PagedResult<ShiftEntity>((int)total, parameter.Page, parameter.Size, items);
    }

    // 集計付き
    public async ValueTask<PagedResult<ShiftDetailView>> QueryDetailPageAsync(ShiftQueryParameter parameter, CancellationToken cancellationToken)
    {
        var page = await QueryPageAsync(parameter, cancellationToken);
        var items = new List<ShiftDetailView>(page.Items.Count);
        foreach (var entity in page.Items)
        {
            items.Add(await LoadDetailAsync(entity, cancellationToken));
        }

        return new PagedResult<ShiftDetailView>(page.Total, page.Page, page.Size, items);
    }

    // 精算レポートの内容
    public async ValueTask<ShiftSummaryView?> QuerySummaryAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await shiftAccessor.QueryAsync(id, cancellationToken);
        return entity is null ? null : await LoadSummaryAsync(entity, cancellationToken);
    }

    // 精算レポート PDF の入力 (表示名と店舗のタイムゾーン付き)
    public async ValueTask<ShiftReportView?> QueryReportAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await shiftAccessor.QueryAsync(id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var store = await masterAccessor.QueryStoreAsync(entity.StoreId, cancellationToken);
        var terminal = await masterAccessor.QueryTerminalAsync(entity.TerminalId, cancellationToken);
        var openedBy = await masterAccessor.QueryStaffAsync(entity.OpenedByStaffId, cancellationToken);
        var closedBy = entity.ClosedByStaffId is null ? null : await masterAccessor.QueryStaffAsync(entity.ClosedByStaffId.Value, cancellationToken);
        return new ShiftReportView
        {
            Summary = await LoadSummaryAsync(entity, cancellationToken),
            StoreName = store?.Name ?? String.Empty,
            TerminalName = terminal?.Name ?? String.Empty,
            TerminalNo = terminal?.TerminalNo ?? 0,
            OpenedBy = openedBy?.Name ?? String.Empty,
            ClosedBy = closedBy?.Name,
            TimeZone = StoreService.ResolveTimeZone(store?.TimeZone)
        };
    }

    //--------------------------------------------------------------------------------
    // Open / Close
    //--------------------------------------------------------------------------------

    // 開設。同じ id は Existing、端末に Open のシフトがあれば TerminalHasOpenShift
    public async ValueTask<ShiftResult> OpenAsync(ShiftEntity entity, CancellationToken cancellationToken)
    {
        var existing = await shiftAccessor.QueryAsync(entity.Id, cancellationToken);
        if (existing is not null)
        {
            return (existing.TerminalId == entity.TerminalId) && (existing.BusinessDate == entity.BusinessDate)
                ? new ShiftResult(ShiftResultStatus.Existing, await LoadDetailAsync(existing, cancellationToken))
                : new ShiftResult(ShiftResultStatus.DuplicateMismatch);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        entity.Status = ShiftStatus.Open;
        entity.CreatedAt = now;
        entity.UpdatedAt = now;
        try
        {
            await shiftAccessor.InsertAsync(entity, cancellationToken);
        }
        catch (DbException ex) when (dialect.IsDuplicate(ex))
        {
            return new ShiftResult(ShiftResultStatus.TerminalHasOpenShift);
        }

        changeNotification.Notify(DataChangeKind.Shift);
        return new ShiftResult(ShiftResultStatus.Success, await LoadDetailAsync(entity, cancellationToken));
    }

    // 精算: 集計を確定して Closed にする。精算済みは、実査金額が同じ再送なら Existing、違えば Closed
    public async ValueTask<ShiftResult> CloseAsync(Guid id, ShiftCloseParameter parameter, CancellationToken cancellationToken)
    {
        var shift = await shiftAccessor.QueryAsync(id, cancellationToken);
        if (shift is null)
        {
            return new ShiftResult(ShiftResultStatus.NotFound);
        }

        if (shift.Status == ShiftStatus.Closed)
        {
            return await ClosedResultAsync(shift, parameter, cancellationToken);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var totals = await shiftAccessor.QuerySummaryAsync(id, cancellationToken) ?? ShiftTotalsView.Empty;
        var expectedCash = ExpectedCash(shift.OpeningCash, totals);
        var updated = await provider.UsingTxAsync(async (_, tx) =>
        {
            // 読んでから更新するまでに同じシフトの精算が先に通っていたら、金種を足さずに終える
            if (await shiftAccessor.UpdateClosedAsync(tx, id, parameter.ClosedAt, parameter.ClosedByStaffId, parameter.ActualCash, expectedCash, parameter.ActualCash - expectedCash, totals, parameter.Note, now, cancellationToken) == 0)
            {
                return false;
            }

            foreach (var denomination in parameter.Denominations)
            {
                await shiftAccessor.InsertDenominationAsync(tx, new ShiftDenominationEntity { ShiftId = id, Denomination = denomination.Denomination, Count = denomination.Count }, cancellationToken);
            }

            await tx.CommitAsync(cancellationToken);
            return true;
        }, cancellationToken);

        var closed = await shiftAccessor.QueryAsync(id, cancellationToken);
        if (!updated)
        {
            return await ClosedResultAsync(closed!, parameter, cancellationToken);
        }

        changeNotification.Notify(DataChangeKind.Shift);
        return new ShiftResult(ShiftResultStatus.Success, await LoadDetailAsync(closed!, cancellationToken));
    }

    // 精算済み: 実査金額が同じなら再送とみなして Existing、違えば Closed
    private async ValueTask<ShiftResult> ClosedResultAsync(ShiftEntity shift, ShiftCloseParameter parameter, CancellationToken cancellationToken) =>
        shift.ActualCash == parameter.ActualCash
            ? new ShiftResult(ShiftResultStatus.Existing, await LoadDetailAsync(shift, cancellationToken))
            : new ShiftResult(ShiftResultStatus.Closed);

    //--------------------------------------------------------------------------------
    // CashEvent
    //--------------------------------------------------------------------------------

    // 入出金 (Open のみ)。同じ id は Existing
    public async ValueTask<CashEventResult> AddCashEventAsync(CashEventEntity entity, CancellationToken cancellationToken)
    {
        var existing = await shiftAccessor.QueryCashEventAsync(entity.Id, cancellationToken);
        if (existing is not null)
        {
            return (existing.ShiftId == entity.ShiftId) && (existing.Amount == entity.Amount) && (existing.Type == entity.Type)
                ? new CashEventResult(CashEventResultStatus.Existing, existing)
                : new CashEventResult(CashEventResultStatus.DuplicateMismatch);
        }

        var shift = await shiftAccessor.QueryAsync(entity.ShiftId, cancellationToken);
        if (shift is null)
        {
            return new CashEventResult(CashEventResultStatus.ShiftNotFound);
        }

        if (shift.Status != ShiftStatus.Open)
        {
            return new CashEventResult(CashEventResultStatus.ShiftClosed);
        }

        entity.CreatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await shiftAccessor.InsertCashEventAsync(entity, cancellationToken);
        changeNotification.Notify(DataChangeKind.Shift);
        return new CashEventResult(CashEventResultStatus.Success, entity);
    }

    // シフトがなければ null
    public async ValueTask<PagedResult<CashEventEntity>?> QueryCashEventPageAsync(Guid shiftId, int page, int size, CancellationToken cancellationToken)
    {
        if (await shiftAccessor.QueryAsync(shiftId, cancellationToken) is null)
        {
            return null;
        }

        var total = await shiftAccessor.CountCashEventsAsync(shiftId, cancellationToken);
        var items = await shiftAccessor.QueryCashEventListAsync(shiftId, size, page * size, cancellationToken);
        return new PagedResult<CashEventEntity>((int)total, page, size, items);
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    // Open 中は取引から都度集計し、Closed は確定値 (Shifts の列) を使う
    private async ValueTask<ShiftDetailView> LoadDetailAsync(ShiftEntity entity, CancellationToken cancellationToken)
    {
        var totals = entity.Status == ShiftStatus.Closed
            ? new ShiftTotalsView(entity.CashSales, entity.CashReturns, entity.PaidIn, entity.PaidOut, entity.SalesCount, entity.ReturnCount, entity.VoidCount, entity.SalesTotal, entity.ReturnsTotal)
            : await shiftAccessor.QuerySummaryAsync(entity.Id, cancellationToken) ?? ShiftTotalsView.Empty;
        return new ShiftDetailView
        {
            Shift = entity,
            Totals = totals,
            Denominations = await shiftAccessor.QueryDenominationListAsync(entity.Id, cancellationToken),
            ExpectedCash = entity.Status == ShiftStatus.Closed ? entity.ExpectedCash : ExpectedCash(entity.OpeningCash, totals)
        };
    }

    private async ValueTask<ShiftSummaryView> LoadSummaryAsync(ShiftEntity entity, CancellationToken cancellationToken) =>
        new()
        {
            Shift = await LoadDetailAsync(entity, cancellationToken),
            ByPaymentMethod = await shiftAccessor.QueryPaymentMethodSummaryAsync(entity.Id, cancellationToken),
            ByTaxRate = await shiftAccessor.QueryTaxRateSummaryAsync(entity.Id, cancellationToken),
            ByCategory = await shiftAccessor.QueryCategorySummaryAsync(entity.Id, cancellationToken),
            Points = await shiftAccessor.QueryPointSummaryAsync(entity.Id, cancellationToken) ?? PointTotalsView.Empty
        };
}
