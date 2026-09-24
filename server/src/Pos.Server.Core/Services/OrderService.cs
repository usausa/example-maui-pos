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

// 受注 (取り寄せ・取り置き)。会計前の約束で、在庫は会計時に減らす。会計との紐付けは TransactionService が行う
public sealed class OrderService
{
    private readonly TimeProvider timeProvider;
    private readonly IDbProvider provider;
    private readonly IDialect dialect;
    private readonly MasterAccessor masterAccessor;
    private readonly ProductAccessor productAccessor;
    private readonly CustomerAccessor customerAccessor;
    private readonly OrderAccessor orderAccessor;
    private readonly ChangeNotificationService changeNotification;

    public OrderService(
        TimeProvider timeProvider,
        IDbProvider provider,
        IDialect dialect,
        MasterAccessor masterAccessor,
        ProductAccessor productAccessor,
        CustomerAccessor customerAccessor,
        OrderAccessor orderAccessor,
        ChangeNotificationService changeNotification)
    {
        this.timeProvider = timeProvider;
        this.provider = provider;
        this.dialect = dialect;
        this.masterAccessor = masterAccessor;
        this.productAccessor = productAccessor;
        this.customerAccessor = customerAccessor;
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
            return new OrderResult(OrderResultStatus.Success, new OrderDetailView { Order = updated, Lines = lines });
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

    // キャンセル (未完了のときだけ)
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
        return current is null
            ? new OrderResult(OrderResultStatus.NotFound)
            : new OrderResult(OrderResultStatus.Violation, Violation: OrderLogic.ValidateCancel(current.Status) ?? new RuleError(ErrorCode.OrderStatusInvalid, RuleReason.OrderNotCancellable));
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private async ValueTask<OrderDetailView> LoadDetailAsync(OrderEntity order, CancellationToken cancellationToken) =>
        new()
        {
            Order = order,
            Lines = await orderAccessor.QueryLineListAsync(order.Id, cancellationToken)
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
