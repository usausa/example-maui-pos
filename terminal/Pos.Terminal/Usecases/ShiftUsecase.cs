namespace Pos.Terminal.Usecases;

using System.Text.Json;

using Pos.Contract.Shifts;
using Pos.Contract.Transactions;
using Pos.Terminal.Models.Entity;

using Smart.Data;

// シフト: 開設 (サーバに残っていれば引き継ぎ)、精算、入出金、集計。書き込みは Outbox 経由でサーバへ送る
public sealed class ShiftUsecase
{
    private readonly IDbProvider provider;

    private readonly DataAccessor accessor;

    private readonly Session session;

    private readonly NetworkService network;

    private readonly SyncService sync;

    public ShiftUsecase(
        IDbProvider provider,
        DataAccessor accessor,
        Session session,
        NetworkService network,
        SyncService sync)
    {
        this.provider = provider;
        this.accessor = accessor;
        this.session = session;
        this.network = network;
        this.sync = sync;
    }

    public ValueTask<LocalShiftEntity?> QueryAsync(Guid id) => accessor.QueryShiftAsync(id);

    //--------------------------------------------------------------------------------
    // Open / Close
    //--------------------------------------------------------------------------------

    // サーバに開設中のシフトが残っていれば引き継ぐ (再インストール時など。Outbox には入れない)
    public async ValueTask<LocalShiftEntity?> AdoptServerShiftAsync()
    {
        if ((session.Terminal is null) || !network.IsConnected)
        {
            return null;
        }

        var result = await network.ExecuteAsync(h => h.GetCurrentShiftAsync(session.Terminal.Id), notify: false);
        if (result is not { IsSuccess: true, Content: { } shift } || (shift.Status != ShiftStatus.Open))
        {
            return null;
        }

        var entity = new LocalShiftEntity
        {
            Id = shift.Id,
            StoreId = shift.StoreId,
            TerminalId = shift.TerminalId,
            Status = shift.Status,
            BusinessDate = shift.BusinessDate,
            OpenedAt = shift.OpenedAt,
            OpenedByStaffId = shift.OpenedByStaffId,
            OpeningCash = shift.OpeningCash,
            Note = shift.Note
        };
        await accessor.InsertServerShiftAsync(entity);
        session.CurrentShift = entity;
        return entity;
    }

    // 店舗・端末・担当が決まっているときだけ呼ぶ
    public async ValueTask<LocalShiftEntity> OpenAsync(decimal openingCash)
    {
        var now = DateTime.UtcNow;
        var entity = new LocalShiftEntity
        {
            Id = Guid.CreateVersion7(),
            StoreId = session.Store!.Id,
            TerminalId = session.Terminal!.Id,
            Status = ShiftStatus.Open,
            BusinessDate = session.BusinessDate,
            OpenedAt = now,
            OpenedByStaffId = session.Staff!.Id,
            OpeningCash = openingCash
        };
        var request = new ShiftOpenRequest
        {
            Id = entity.Id,
            StoreId = entity.StoreId,
            TerminalId = entity.TerminalId,
            BusinessDate = entity.BusinessDate,
            OpenedAt = entity.OpenedAt,
            OpenedByStaffId = entity.OpenedByStaffId,
            OpeningCash = entity.OpeningCash
        };
        await provider.UsingTxAsync(async (_, tx) =>
        {
            await accessor.InsertShiftAsync(tx, entity);
            await accessor.InsertOutboxAsync(tx, SyncService.CreateEntry(OutboxKind.ShiftOpen, entity.Id, request, now));
            await tx.CommitAsync();
        });
        session.CurrentShift = entity;
        await sync.UpdateCountsAsync();
        sync.Trigger();
        return entity;
    }

    // 精算。Session.CanTransact のときだけ呼ぶ
    public async ValueTask CloseAsync(LocalShiftEntity shift, decimal actualCash, decimal expectedCash, IReadOnlyList<ShiftCloseRequestDenomination> denominations)
    {
        var now = DateTime.UtcNow;
        var staffId = session.Staff!.Id;
        var request = new ShiftCloseRequest
        {
            ClosedAt = now,
            ClosedByStaffId = staffId,
            ActualCash = actualCash,
            Denominations = denominations
        };
        await provider.UsingTxAsync(async (_, tx) =>
        {
            await accessor.UpdateShiftClosedAsync(tx, shift.Id, now, staffId, actualCash, expectedCash, actualCash - expectedCash, null);
            await accessor.InsertOutboxAsync(tx, SyncService.CreateEntry(OutboxKind.ShiftClose, shift.Id, request, now));
            await tx.CommitAsync();
        });
        session.CurrentShift = await accessor.QueryShiftAsync(shift.Id);
        await sync.UpdateCountsAsync();
        sync.Trigger();
    }

    //--------------------------------------------------------------------------------
    // CashEvent
    //--------------------------------------------------------------------------------

    // 入出金。Session.CanTransact のときだけ呼ぶ
    public async ValueTask AddCashEventAsync(CashEventType type, decimal amount, string? reason)
    {
        var now = DateTime.UtcNow;
        var shiftId = session.CurrentShift!.Id;
        var entity = new LocalCashEventEntity
        {
            Id = Guid.CreateVersion7(),
            ShiftId = shiftId,
            Type = type,
            Amount = amount,
            Reason = reason,
            StaffId = session.Staff!.Id,
            OccurredAt = now
        };
        var request = new ShiftCashEventRequest
        {
            Id = entity.Id,
            Type = entity.Type,
            Amount = entity.Amount,
            Reason = entity.Reason,
            StaffId = entity.StaffId,
            OccurredAt = entity.OccurredAt
        };
        await provider.UsingTxAsync(async (_, tx) =>
        {
            await accessor.InsertCashEventAsync(tx, entity);
            await accessor.InsertOutboxAsync(tx, SyncService.CreateEntry(OutboxKind.CashEvent, shiftId, request, now));
            await tx.CommitAsync();
        });
        await sync.UpdateCountsAsync();
        sync.Trigger();
    }

    //--------------------------------------------------------------------------------
    // Summary
    //--------------------------------------------------------------------------------

    // ローカルの取引 (Payload = TransactionResponseItem)・入出金・前受金から集計する
    public async ValueTask<ShiftSummaryResponse> BuildSummaryAsync(LocalShiftEntity shift)
    {
        var transactions = (await accessor.QueryTransactionListAsync(shift.Id, null, null, 10000))
            .Select(static x => JsonSerializer.Deserialize<TransactionResponseItem>(x.Payload, HttpService.JsonOptions)!)
            .ToList();
        var cashEvents = await accessor.QueryCashEventListAsync(shift.Id);
        var deposits = await accessor.QueryOrderDepositListAsync(shift.Id);
        var paymentMethods = await accessor.QueryPaymentMethodListAsync();
        var categories = await accessor.QueryCategoryListAsync();
        return ShiftSummaryCalculator.Calculate(shift, transactions, cashEvents, deposits, paymentMethods, categories);
    }

    // 未送信がなくオンラインならサーバの集計、それ以外は端末の集計
    public async ValueTask<(ShiftSummaryResponse Summary, bool FromServer)> QuerySummaryAsync(LocalShiftEntity shift)
    {
        if ((session.UnsentCount == 0) && network.IsConnected)
        {
            var result = await network.ExecuteAsync(h => h.GetShiftSummaryAsync(shift.Id), notify: false);
            if (result is { IsSuccess: true, Content: not null })
            {
                return (result.Content, true);
            }
        }

        return (await BuildSummaryAsync(shift), false);
    }
}
