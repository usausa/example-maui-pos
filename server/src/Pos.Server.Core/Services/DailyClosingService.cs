namespace Pos.Server.Services;

using Pos.Server.Accessors;
using Pos.Server.Models;
using Pos.Server.Models.Entity;
using Pos.Server.Models.Parameters;
using Pos.Server.Models.Views;

public enum DailyClosingResultStatus
{
    Success,
    // その営業日にシフトがない (締めるものがない)
    NoShift,
    // 未精算のシフトがある
    ShiftStillOpen,
    // 締め済み
    AlreadyClosed
}

// 締めの結果。Success 以外も、判定に使ったその日の内容を返す (一意制約で重複したときは null)
public sealed record DailyClosingResult(DailyClosingResultStatus Status, DailyClosingSummaryView? Summary = null);

// 日次締め。店舗 × 営業日の日計を締めた時点で確定し、以後の取消を止める。
// 締めた後に届いた取引 (オフラインの端末の送信) は取引の登録で受け付けて印を付け、締め直しで取り込む
public sealed class DailyClosingService
{
    private readonly TimeProvider timeProvider;
    private readonly IDbProvider provider;
    private readonly IDialect dialect;
    private readonly DailyClosingAccessor dailyClosingAccessor;
    private readonly ReportService reportService;
    private readonly ChangeNotificationService changeNotification;

    public DailyClosingService(
        TimeProvider timeProvider,
        IDbProvider provider,
        IDialect dialect,
        DailyClosingAccessor dailyClosingAccessor,
        ReportService reportService,
        ChangeNotificationService changeNotification)
    {
        this.timeProvider = timeProvider;
        this.provider = provider;
        this.dialect = dialect;
        this.dailyClosingAccessor = dailyClosingAccessor;
        this.reportService = reportService;
        this.changeNotification = changeNotification;
    }

    //--------------------------------------------------------------------------------
    // Query
    //--------------------------------------------------------------------------------

    // 店舗 × 営業日の一覧 (シフト・取引・締めのある日)
    public async ValueTask<PagedResult<DailyClosingDayView>> QueryDayPageAsync(DailyClosingQueryParameter parameter, CancellationToken cancellationToken)
    {
        var total = await dailyClosingAccessor.CountDaysAsync(parameter.StoreId, parameter.Status, parameter.From, parameter.To, cancellationToken);
        var items = await dailyClosingAccessor.QueryDayListAsync(parameter.StoreId, parameter.Status, parameter.From, parameter.To, parameter.Sort, parameter.Desc, parameter.Size, parameter.Page * parameter.Size, cancellationToken);
        return new PagedResult<DailyClosingDayView>((int)total, parameter.Page, parameter.Size, items);
    }

    // 前日までの未締め (ダッシュボードの要確認。古い日も漏らさないよう期間で絞らない)
    public ValueTask<PagedResult<DailyClosingDayView>> QueryUnclosedPageAsync(int size, CancellationToken cancellationToken) =>
        QueryDayPageAsync(new DailyClosingQueryParameter { Status = DailyClosingStatus.Open, To = reportService.Today.AddDays(-1), Desc = true, Size = size }, cancellationToken);

    // 締め済みの内容 (解除済みなら null)
    public async ValueTask<DailyClosingSummaryView?> QuerySummaryAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dailyClosingAccessor.QueryAsync(id, cancellationToken);
        return entity is null ? null : await QuerySummaryAsync(entity.StoreId, entity.BusinessDate, cancellationToken);
    }

    // 店舗 × 営業日の内容。締め済みは締めた時点の日計と内訳、未締めは取引からの集計 (締める前の確認に使う)
    public async ValueTask<DailyClosingSummaryView> QuerySummaryAsync(Guid storeId, DateOnly businessDate, CancellationToken cancellationToken)
    {
        var day = (await dailyClosingAccessor.QueryDayListAsync(storeId, null, businessDate, businessDate, DailyClosingSort.BusinessDate, false, 1, 0, cancellationToken)).FirstOrDefault()
            ?? DailyClosingDayView.Empty(storeId, businessDate);
        return new DailyClosingSummaryView
        {
            Day = day,
            ByPaymentMethod = day.Id is { } paymentId
                ? await dailyClosingAccessor.QueryPaymentListAsync(paymentId, cancellationToken)
                : await dailyClosingAccessor.QueryPaymentMethodSummaryAsync(storeId, businessDate, cancellationToken),
            ByTaxRate = day.Id is { } taxId
                ? await dailyClosingAccessor.QueryTaxListAsync(taxId, cancellationToken)
                : await dailyClosingAccessor.QueryTaxRateSummaryAsync(storeId, businessDate, cancellationToken),
            Shifts = await dailyClosingAccessor.QueryShiftListAsync(storeId, businessDate, cancellationToken)
        };
    }

    //--------------------------------------------------------------------------------
    // Close / Reopen
    //--------------------------------------------------------------------------------

    // 締め: その日のシフト (日をまたいで取引を含むシフトも) がすべて精算済みなら、日計と内訳を写して確定する
    public async ValueTask<DailyClosingResult> CloseAsync(Guid storeId, DateOnly businessDate, string? closedBy, CancellationToken cancellationToken)
    {
        var summary = await QuerySummaryAsync(storeId, businessDate, cancellationToken);
        var day = summary.Day;
        if (day.Status == DailyClosingStatus.Closed)
        {
            return new DailyClosingResult(DailyClosingResultStatus.AlreadyClosed, summary);
        }

        if (day.ShiftCount == 0)
        {
            return new DailyClosingResult(DailyClosingResultStatus.NoShift, summary);
        }

        if (day.OpenShiftCount > 0)
        {
            return new DailyClosingResult(DailyClosingResultStatus.ShiftStillOpen, summary);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var entity = new DailyClosingEntity
        {
            Id = Guid.CreateVersion7(),
            StoreId = storeId,
            BusinessDate = businessDate,
            ClosedAt = now,
            ClosedBy = closedBy,
            ShiftCount = day.ShiftCount,
            SalesCount = day.SalesCount,
            ReturnCount = day.ReturnCount,
            VoidCount = day.VoidCount,
            CustomerCount = day.CustomerCount,
            SalesTotal = day.SalesTotal,
            ReturnsTotal = day.ReturnsTotal,
            NetSales = day.NetSales,
            DiscountTotal = day.DiscountTotal,
            TaxTotal = day.TaxTotal,
            PointsEarned = day.PointsEarned,
            PointsRedeemed = day.PointsRedeemed,
            CreatedAt = now,
            UpdatedAt = now
        };
        try
        {
            await provider.UsingTxAsync(async (_, tx) =>
            {
                await dailyClosingAccessor.InsertAsync(tx, entity, cancellationToken);
                var lineNo = 0;
                foreach (var payment in summary.ByPaymentMethod)
                {
                    await dailyClosingAccessor.InsertPaymentAsync(tx, new DailyClosingPaymentEntity
                    {
                        DailyClosingId = entity.Id,
                        LineNo = ++lineNo,
                        PaymentMethodId = payment.PaymentMethodId,
                        Name = payment.Name,
                        Kind = payment.Kind,
                        SalesAmount = payment.SalesAmount,
                        SalesCount = payment.SalesCount,
                        ReturnAmount = payment.ReturnAmount,
                        ReturnCount = payment.ReturnCount
                    }, cancellationToken);
                }

                lineNo = 0;
                foreach (var tax in summary.ByTaxRate)
                {
                    await dailyClosingAccessor.InsertTaxAsync(tx, new DailyClosingTaxEntity
                    {
                        DailyClosingId = entity.Id,
                        LineNo = ++lineNo,
                        TaxRateId = tax.TaxRateId,
                        Rate = tax.Rate,
                        TaxIncluded = tax.TaxIncluded,
                        TaxableAmount = tax.TaxableAmount,
                        TaxAmount = tax.TaxAmount
                    }, cancellationToken);
                }

                await tx.CommitAsync(cancellationToken);
            }, cancellationToken);
        }
        catch (DbException ex) when (dialect.IsDuplicate(ex))
        {
            return new DailyClosingResult(DailyClosingResultStatus.AlreadyClosed);
        }

        changeNotification.Notify(DataChangeKind.DailyClosing);
        return new DailyClosingResult(DailyClosingResultStatus.Success, await QuerySummaryAsync(storeId, businessDate, cancellationToken));
    }

    // 締め解除: 日計と内訳を消し、その日を未締めに戻す (取消ができるようになり、締め直しで改めて集計する)。見つからなければ false
    public async ValueTask<bool> ReopenAsync(Guid id, CancellationToken cancellationToken)
    {
        var reopened = await provider.UsingTxAsync(async (_, tx) =>
        {
            await dailyClosingAccessor.DeletePaymentsAsync(tx, id, cancellationToken);
            await dailyClosingAccessor.DeleteTaxesAsync(tx, id, cancellationToken);
            var deleted = await dailyClosingAccessor.DeleteAsync(tx, id, cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return deleted > 0;
        }, cancellationToken);
        if (reopened)
        {
            changeNotification.Notify(DataChangeKind.DailyClosing);
        }

        return reopened;
    }
}
