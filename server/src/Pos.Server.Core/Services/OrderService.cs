namespace Pos.Server.Services;

using Pos.Domain.Logic;
using Pos.Server.Accessors;
using Pos.Server.Models;
using Pos.Server.Models.Entity;
using Pos.Server.Models.Parameters;
using Pos.Server.Models.Views;

public enum OrderResultStatus
{
    Success,
    // 同じ id の再送 (登録済みを返す)
    Existing,
    NotFound,
    // 同じ id で内容が異なる
    DuplicateMismatch,
    VersionMismatch,
    // 業務ルール違反 (Violation に理由)
    Violation
}

// 受注の登録・変更・状態遷移の結果
public sealed record OrderResult(OrderResultStatus Status, OrderDetailView? Detail = null, RuleError? Violation = null);

// 受注 (取り寄せ・取り置き)。会計前の約束で、在庫は会計時に減らす。会計との紐付けは TransactionService が行う。
// 前受金は端末のシフトで受け取り、会計で全額を充てる (キャンセルは返してから)
public sealed class OrderService
{
    private readonly TimeProvider timeProvider;
    private readonly IDbProvider provider;
    private readonly IDialect dialect;
    private readonly MasterAccessor masterAccessor;
    private readonly ProductAccessor productAccessor;
    private readonly CustomerAccessor customerAccessor;
    private readonly ShiftAccessor shiftAccessor;
    private readonly OrderAccessor orderAccessor;
    private readonly ChangeNotificationService changeNotification;

    public OrderService(
        TimeProvider timeProvider,
        IDbProvider provider,
        IDialect dialect,
        MasterAccessor masterAccessor,
        ProductAccessor productAccessor,
        CustomerAccessor customerAccessor,
        ShiftAccessor shiftAccessor,
        OrderAccessor orderAccessor,
        ChangeNotificationService changeNotification)
    {
        this.timeProvider = timeProvider;
        this.provider = provider;
        this.dialect = dialect;
        this.masterAccessor = masterAccessor;
        this.productAccessor = productAccessor;
        this.customerAccessor = customerAccessor;
        this.shiftAccessor = shiftAccessor;
        this.orderAccessor = orderAccessor;
        this.changeNotification = changeNotification;
    }

    //--------------------------------------------------------------------------------
    // Query
    //--------------------------------------------------------------------------------

    public async ValueTask<PagedResult<OrderDetailView>> QueryPageAsync(OrderQueryParameter parameter, CancellationToken cancellationToken)
    {
        var keyword = ServiceHelper.ToLikePattern(dialect, parameter.Keyword);
        var (from, to) = ToUtcRange(parameter.From, parameter.To);
        var total = await orderAccessor.CountAsync(parameter.StoreId, parameter.Status, parameter.OpenOnly, parameter.Type, parameter.CustomerId, keyword, from, to, cancellationToken);
        var orders = await orderAccessor.QueryListAsync(parameter.StoreId, parameter.Status, parameter.OpenOnly, parameter.Type, parameter.CustomerId, keyword, from, to, parameter.Sort, parameter.Desc, parameter.Size, parameter.Page * parameter.Size, cancellationToken);
        var items = new List<OrderDetailView>(orders.Count);
        foreach (var order in orders)
        {
            items.Add(await LoadDetailAsync(order, cancellationToken));
        }

        return new PagedResult<OrderDetailView>((int)total, parameter.Page, parameter.Size, items);
    }

    public async ValueTask<OrderDetailView?> QueryDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        var order = await orderAccessor.QueryAsync(id, cancellationToken);
        return order is null ? null : await LoadDetailAsync(order, cancellationToken);
    }

    // 未完了の受注の件数 (入荷待ち・引き渡し待ち)
    public async ValueTask<(int Ordered, int Arrived)> CountOpenAsync(Guid? storeId, CancellationToken cancellationToken)
    {
        var counts = await orderAccessor.QueryStatusSummaryAsync(storeId, cancellationToken);
        return (counts.FirstOrDefault(static x => x.Status == OrderStatus.Ordered)?.Count ?? 0, counts.FirstOrDefault(static x => x.Status == OrderStatus.Arrived)?.Count ?? 0);
    }

    //--------------------------------------------------------------------------------
    // Create / Update
    //--------------------------------------------------------------------------------

    // 登録。同じ id は Existing (内容が違えば DuplicateMismatch)。受注番号は店舗ごとの連番、取り置きは引き渡し待ちから始める。
    // 受注日時を省略したら登録時刻にする (管理画面からの登録)
    public async ValueTask<OrderResult> CreateAsync(OrderDetailView detail, CancellationToken cancellationToken)
    {
        var order = detail.Order;
        var existing = await orderAccessor.QueryAsync(order.Id, cancellationToken);
        if (existing is not null)
        {
            return (existing.StoreId == order.StoreId) && (existing.Type == order.Type) && (existing.CustomerId == order.CustomerId)
                ? new OrderResult(OrderResultStatus.Existing, await LoadDetailAsync(existing, cancellationToken))
                : new OrderResult(OrderResultStatus.DuplicateMismatch);
        }

        var store = await masterAccessor.QueryStoreAsync(order.StoreId, cancellationToken);
        if (store is null)
        {
            return Violated(ErrorCode.ValidationError, RuleReason.StoreNotFound);
        }

        var customer = order.CustomerId is null ? null : await customerAccessor.QueryAsync(order.CustomerId.Value, cancellationToken);
        if ((order.CustomerId is not null) && (customer is null))
        {
            return Violated(ErrorCode.ValidationError, RuleReason.CustomerNotFound);
        }

        if (await ValidateProductsAsync(detail.Lines, cancellationToken) is { } violation)
        {
            return new OrderResult(OrderResultStatus.Violation, Violation: violation);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var orderedAt = order.OrderedAt == default ? now : order.OrderedAt;
        var status = OrderLogic.InitialStatus(order.Type);
        var lines = ToLines(order.Id, detail.Lines);
        var inserted = await provider.UsingTxAsync(async (_, tx) =>
        {
            var entity = await orderAccessor.InsertAsync(
                tx,
                order.Id,
                order.StoreId,
                store.Code,
                order.TerminalId,
                order.StaffId,
                order.CustomerId,
                ResolveName(order.CustomerName, customer),
                ResolvePhone(order.Phone, customer),
                order.Type,
                status,
                order.RequestedDate,
                order.Note,
                lines.Sum(static x => x.Amount),
                orderedAt,
                status == OrderStatus.Arrived ? orderedAt : null,
                now,
                cancellationToken);
            foreach (var line in lines)
            {
                await orderAccessor.InsertLineAsync(tx, line, cancellationToken);
            }

            await tx.CommitAsync(cancellationToken);
            return entity!;
        }, cancellationToken);

        changeNotification.Notify(DataChangeKind.Order);
        return new OrderResult(OrderResultStatus.Success, new OrderDetailView { Order = inserted, Lines = lines });
    }

    // 連絡先・明細・希望日・備考の変更 (未完了のときだけ)。明細は全体を置き換える
    public async ValueTask<OrderResult> UpdateAsync(Guid id, OrderUpdateParameter parameter, CancellationToken cancellationToken)
    {
        var customer = parameter.CustomerId is null ? null : await customerAccessor.QueryAsync(parameter.CustomerId.Value, cancellationToken);
        if ((parameter.CustomerId is not null) && (customer is null))
        {
            return Violated(ErrorCode.ValidationError, RuleReason.CustomerNotFound);
        }

        if (await ValidateProductsAsync(parameter.Lines, cancellationToken) is { } violation)
        {
            return new OrderResult(OrderResultStatus.Violation, Violation: violation);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var lines = ToLines(id, parameter.Lines);
        var updated = await provider.UsingTxAsync(async (_, tx) =>
        {
            var entity = await orderAccessor.UpdateAsync(tx, id, parameter.CustomerId, ResolveName(parameter.CustomerName, customer), ResolvePhone(parameter.Phone, customer), parameter.RequestedDate, parameter.Note, lines.Sum(static x => x.Amount), now, parameter.Version, cancellationToken);
            if (entity is null)
            {
                return null;
            }

            await orderAccessor.DeleteLinesAsync(tx, id, cancellationToken);
            foreach (var line in lines)
            {
                await orderAccessor.InsertLineAsync(tx, line, cancellationToken);
            }

            await tx.CommitAsync(cancellationToken);
            return entity;
        }, cancellationToken);

        if (updated is not null)
        {
            changeNotification.Notify(DataChangeKind.Order);
            return new OrderResult(OrderResultStatus.Success, new OrderDetailView { Order = updated, Lines = lines, Deposits = await orderAccessor.QueryDepositListAsync(id, cancellationToken) });
        }

        // 行が返らない理由: ない、完了・キャンセル済み、版の不一致
        var current = await orderAccessor.QueryAsync(id, cancellationToken);
        if (current is null)
        {
            return new OrderResult(OrderResultStatus.NotFound);
        }

        return OrderLogic.ValidateUpdate(current.Status) is { } error
            ? new OrderResult(OrderResultStatus.Violation, Violation: error)
            : new OrderResult(OrderResultStatus.VersionMismatch);
    }

    //--------------------------------------------------------------------------------
    // Arrive / Cancel
    //--------------------------------------------------------------------------------

    // 入荷 (取り寄せの商品が届いた。入荷待ちのときだけ)
    public async ValueTask<OrderResult> ArriveAsync(Guid id, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var updated = await orderAccessor.UpdateArrivedAsync(id, now, now, cancellationToken);
        if (updated is not null)
        {
            changeNotification.Notify(DataChangeKind.Order);
            return new OrderResult(OrderResultStatus.Success, await LoadDetailAsync(updated, cancellationToken));
        }

        var current = await orderAccessor.QueryAsync(id, cancellationToken);
        return current is null
            ? new OrderResult(OrderResultStatus.NotFound)
            : new OrderResult(OrderResultStatus.Violation, Violation: OrderLogic.ValidateArrive(current.Status) ?? new RuleError(ErrorCode.OrderStatusInvalid, RuleReason.OrderNotOrdered));
    }

    // キャンセル (未完了で、前受金がないときだけ)
    public async ValueTask<OrderResult> CancelAsync(Guid id, string? reason, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var updated = await orderAccessor.UpdateCancelledAsync(id, now, reason, now, cancellationToken);
        if (updated is not null)
        {
            changeNotification.Notify(DataChangeKind.Order);
            return new OrderResult(OrderResultStatus.Success, await LoadDetailAsync(updated, cancellationToken));
        }

        var current = await orderAccessor.QueryAsync(id, cancellationToken);
        if (current is null)
        {
            return new OrderResult(OrderResultStatus.NotFound);
        }

        var balance = OrderDetailView.DepositBalanceOf(current.Status, await orderAccessor.QueryDepositListAsync(id, cancellationToken));
        return new OrderResult(OrderResultStatus.Violation, Violation: OrderLogic.ValidateCancel(current.Status, balance) ?? new RuleError(ErrorCode.OrderStatusInvalid, RuleReason.OrderNotCancellable));
    }

    //--------------------------------------------------------------------------------
    // Deposit
    //--------------------------------------------------------------------------------

    // 前受金の受取 (端末)。同じ id は Existing (内容が違えば DuplicateMismatch)。受取日時を省略したら受付時刻にする
    public async ValueTask<OrderResult> DepositAsync(Guid id, OrderDepositEntity deposit, CancellationToken cancellationToken)
    {
        if (await ResolveResentDepositAsync(id, deposit, OrderDepositType.Receive, cancellationToken) is { } resent)
        {
            return resent;
        }

        var order = await orderAccessor.QueryAsync(id, cancellationToken);
        if (order is null)
        {
            return new OrderResult(OrderResultStatus.NotFound);
        }

        var method = await masterAccessor.QueryPaymentMethodAsync(deposit.PaymentMethodId, cancellationToken);
        if ((method is null) || !method.IsActive || method.IsDeleted)
        {
            return Violated(ErrorCode.OrderDepositInvalid, RuleReason.DepositMethodInvalid);
        }

        var balance = OrderDetailView.DepositBalanceOf(order.Status, await orderAccessor.QueryDepositListAsync(id, cancellationToken));
        if ((await ValidateDepositPlaceAsync(order, deposit, cancellationToken) ?? OrderLogic.ValidateDeposit(order.Status, balance, order.Total, deposit.Amount, method.Kind)) is { } violation)
        {
            return new OrderResult(OrderResultStatus.Violation, Violation: violation);
        }

        deposit.Type = OrderDepositType.Receive;
        deposit.Kind = method.Kind;
        return await InsertDepositAsync(id, deposit, balance, (current, currentBalance) => OrderLogic.ValidateDeposit(current.Status, currentBalance, current.Total, deposit.Amount, method.Kind), cancellationToken);
    }

    // 前受金の返金 (端末)。前受金の全額を、受け取った方法で返す。同じ id は Existing
    public async ValueTask<OrderResult> RefundDepositAsync(Guid id, OrderDepositEntity refund, CancellationToken cancellationToken)
    {
        if (await ResolveResentDepositAsync(id, refund, OrderDepositType.Refund, cancellationToken) is { } resent)
        {
            return resent;
        }

        var order = await orderAccessor.QueryAsync(id, cancellationToken);
        if (order is null)
        {
            return new OrderResult(OrderResultStatus.NotFound);
        }

        var deposits = await orderAccessor.QueryDepositListAsync(id, cancellationToken);
        var balance = OrderDetailView.DepositBalanceOf(order.Status, deposits);
        if ((await ValidateDepositPlaceAsync(order, refund, cancellationToken) ?? OrderLogic.ValidateDepositRefund(order.Status, balance)) is { } violation)
        {
            return new OrderResult(OrderResultStatus.Violation, Violation: violation);
        }

        // 有効な前受金は 1 つなので、最後の受取が返す相手
        var received = deposits.Last(static x => x.Type == OrderDepositType.Receive);
        refund.Type = OrderDepositType.Refund;
        refund.PaymentMethodId = received.PaymentMethodId;
        refund.Kind = received.Kind;
        refund.Amount = balance;
        return await InsertDepositAsync(id, refund, balance, static (current, currentBalance) => OrderLogic.ValidateDepositRefund(current.Status, currentBalance), cancellationToken);
    }

    // 同じ id の再送: 同じ受注・種類 (受取は方法と金額も) なら登録済みとして受注を返す
    private async ValueTask<OrderResult?> ResolveResentDepositAsync(Guid id, OrderDepositEntity deposit, OrderDepositType type, CancellationToken cancellationToken)
    {
        var existing = await orderAccessor.QueryDepositAsync(deposit.Id, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        var same = (existing.OrderId == id) &&
            (existing.Type == type) &&
            ((type == OrderDepositType.Refund) || ((existing.PaymentMethodId == deposit.PaymentMethodId) && (existing.Amount == deposit.Amount)));
        return same
            ? new OrderResult(OrderResultStatus.Existing, await QueryDetailAsync(id, cancellationToken))
            : new OrderResult(OrderResultStatus.DuplicateMismatch);
    }

    // 前受金の記録。読んだときの残り (balance) のままで、シフトが開設中なら登録し、重なった操作で変わっていたら改めて検証する
    private async ValueTask<OrderResult> InsertDepositAsync(Guid id, OrderDepositEntity deposit, decimal balance, Func<OrderEntity, decimal, RuleError?> validate, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        OrderDepositEntity? inserted;
        try
        {
            inserted = await orderAccessor.InsertDepositAsync(
                deposit.Id,
                id,
                deposit.TerminalId,
                deposit.ShiftId,
                deposit.StaffId,
                deposit.Type,
                deposit.PaymentMethodId,
                deposit.Kind,
                deposit.Amount,
                deposit.Reference,
                deposit.OccurredAt == default ? now : deposit.OccurredAt,
                now,
                balance,
                cancellationToken);
        }
        catch (DbException ex) when (dialect.IsDuplicate(ex))
        {
            // 同じ id の再送が先に通った
            return await ResolveResentDepositAsync(id, deposit, deposit.Type, cancellationToken) ?? new OrderResult(OrderResultStatus.DuplicateMismatch);
        }

        if (inserted is not null)
        {
            changeNotification.Notify(DataChangeKind.Order);
            return new OrderResult(OrderResultStatus.Success, await QueryDetailAsync(id, cancellationToken));
        }

        var current = await orderAccessor.QueryAsync(id, cancellationToken);
        if (current is null)
        {
            return new OrderResult(OrderResultStatus.NotFound);
        }

        var currentBalance = OrderDetailView.DepositBalanceOf(current.Status, await orderAccessor.QueryDepositListAsync(id, cancellationToken));
        return (await ValidateDepositPlaceAsync(current, deposit, cancellationToken) ?? validate(current, currentBalance)) is { } error
            ? new OrderResult(OrderResultStatus.Violation, Violation: error)
            : new OrderResult(OrderResultStatus.VersionMismatch);
    }

    private async ValueTask<RuleError?> ValidateDepositPlaceAsync(OrderEntity order, OrderDepositEntity deposit, CancellationToken cancellationToken)
    {
        var shift = await shiftAccessor.QueryAsync(deposit.ShiftId, cancellationToken);
        var staff = await masterAccessor.QueryStaffAsync(deposit.StaffId, cancellationToken);
        return OrderLogic.ValidateDepositPlace(
            shift is null ? null : new ShiftFact { Id = shift.Id, Status = shift.Status, TerminalId = shift.TerminalId, StoreId = shift.StoreId },
            deposit.TerminalId,
            order.StoreId,
            staff is null ? null : new StaffFact { Id = staff.Id, Role = staff.Role, StoreId = staff.StoreId, IsActive = staff.IsActive && !staff.IsDeleted });
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private async ValueTask<OrderDetailView> LoadDetailAsync(OrderEntity order, CancellationToken cancellationToken) =>
        new()
        {
            Order = order,
            Lines = await orderAccessor.QueryLineListAsync(order.Id, cancellationToken),
            Deposits = await orderAccessor.QueryDepositListAsync(order.Id, cancellationToken)
        };

    private async ValueTask<RuleError?> ValidateProductsAsync(IReadOnlyList<OrderLineEntity> lines, CancellationToken cancellationToken)
    {
        var ids = lines.Select(static x => x.ProductId).Distinct().ToList();
        var products = await productAccessor.QueryListByIdsAsync(ids, cancellationToken);
        var missing = lines.FirstOrDefault(x => products.All(product => product.Id != x.ProductId));
        return missing is null ? null : new RuleError(ErrorCode.ProductNotFound, RuleReason.ProductNotFound, missing.Id);
    }

    // 明細の金額を付け、並び順を振り直す
    private static List<OrderLineEntity> ToLines(Guid orderId, IEnumerable<OrderLineEntity> lines) =>
        lines.Select((x, i) => new OrderLineEntity
        {
            Id = x.Id,
            OrderId = orderId,
            LineNo = i + 1,
            ProductId = x.ProductId,
            ProductCode = x.ProductCode,
            ProductName = x.ProductName,
            Quantity = x.Quantity,
            UnitPrice = x.UnitPrice,
            Amount = OrderLogic.LineAmount(x.UnitPrice, x.Quantity),
            Note = x.Note
        }).ToList();

    // 宛名・電話を省略したら会員のものを使う
    private static string ResolveName(string? name, CustomerEntity? customer) =>
        String.IsNullOrWhiteSpace(name) ? customer?.Name ?? string.Empty : name.Trim();

    private static string? ResolvePhone(string? phone, CustomerEntity? customer) =>
        String.IsNullOrWhiteSpace(phone) ? customer?.Phone : phone.Trim();

    private static OrderResult Violated(ErrorCode code, RuleReason reason) =>
        new(OrderResultStatus.Violation, Violation: new RuleError(code, reason));

    // 受注日 (サーバのローカル日付) の範囲を受注日時 (UTC) の [from, to) にする
    private (DateTime? From, DateTime? To) ToUtcRange(DateOnly? from, DateOnly? to)
    {
        var timeZone = timeProvider.LocalTimeZone;
        return (
            from is null ? null : TimeZoneInfo.ConvertTimeToUtc(from.Value.ToDateTime(TimeOnly.MinValue), timeZone),
            to is null ? null : TimeZoneInfo.ConvertTimeToUtc(to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), timeZone));
    }
}
