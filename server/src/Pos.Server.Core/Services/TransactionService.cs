namespace Pos.Server.Services;

using Pos.Domain.Logic;
using Pos.Server.Accessors;
using Pos.Server.Models;
using Pos.Server.Models.Entity;
using Pos.Server.Models.Parameters;
using Pos.Server.Models.Views;

public enum TransactionResultStatus
{
    // 登録・取消した
    Success,
    // 同じ id の再送 (登録済みを返す)
    Existing,
    NotFound,
    // 同じ id で内容が異なる
    DuplicateMismatch,
    // 業務ルール違反 (Validation にエラーと期待値)
    Invalid,
    // 書き込み中に見つかった違反 (Violation)
    Violation
}

// 取引の登録・取消の結果
public sealed class TransactionResult
{
    public TransactionResultStatus Status { get; }

    public TransactionDetailView? Detail { get; }

    public IReadOnlyList<RuleWarning> Warnings { get; }

    public TransactionValidation? Validation { get; }

    public RuleError? Violation { get; }

    private TransactionResult(TransactionResultStatus status, TransactionDetailView? detail = null, IReadOnlyList<RuleWarning>? warnings = null, TransactionValidation? validation = null, RuleError? violation = null)
    {
        Status = status;
        Detail = detail;
        Warnings = warnings ?? [];
        Validation = validation;
        Violation = violation;
    }

    public static TransactionResult Success(TransactionDetailView detail, IReadOnlyList<RuleWarning>? warnings = null) => new(TransactionResultStatus.Success, detail, warnings);

    public static TransactionResult Existing(TransactionDetailView detail) => new(TransactionResultStatus.Existing, detail);

    public static TransactionResult NotFound() => new(TransactionResultStatus.NotFound);

    public static TransactionResult DuplicateMismatch() => new(TransactionResultStatus.DuplicateMismatch);

    public static TransactionResult Invalid(TransactionValidation validation) => new(TransactionResultStatus.Invalid, validation: validation);

    public static TransactionResult Violated(RuleError violation) => new(TransactionResultStatus.Violation, violation: violation);
}

// 計算の結果 (登録しない)。Error は先頭の 1 件
public sealed record TransactionCalculation(SalesResult? Result, RuleError? Error);

// 取引の登録・取消・照会。登録は取引一式 + 在庫 + ポイント + 端末の連番を 1 トランザクションで書く
public sealed class TransactionService
{
    private readonly TimeProvider timeProvider;
    private readonly IDbProvider provider;
    private readonly MasterAccessor masterAccessor;
    private readonly ProductAccessor productAccessor;
    private readonly CustomerAccessor customerAccessor;
    private readonly ShiftAccessor shiftAccessor;
    private readonly TransactionAccessor transactionAccessor;
    private readonly InventoryAccessor inventoryAccessor;
    private readonly DailyClosingAccessor dailyClosingAccessor;
    private readonly OrderAccessor orderAccessor;
    private readonly ChangeNotificationService changeNotification;

    public TransactionService(
        TimeProvider timeProvider,
        IDbProvider provider,
        MasterAccessor masterAccessor,
        ProductAccessor productAccessor,
        CustomerAccessor customerAccessor,
        ShiftAccessor shiftAccessor,
        TransactionAccessor transactionAccessor,
        InventoryAccessor inventoryAccessor,
        DailyClosingAccessor dailyClosingAccessor,
        OrderAccessor orderAccessor,
        ChangeNotificationService changeNotification)
    {
        this.timeProvider = timeProvider;
        this.provider = provider;
        this.masterAccessor = masterAccessor;
        this.productAccessor = productAccessor;
        this.customerAccessor = customerAccessor;
        this.shiftAccessor = shiftAccessor;
        this.transactionAccessor = transactionAccessor;
        this.inventoryAccessor = inventoryAccessor;
        this.dailyClosingAccessor = dailyClosingAccessor;
        this.orderAccessor = orderAccessor;
        this.changeNotification = changeNotification;
    }

    //--------------------------------------------------------------------------------
    // Query
    //--------------------------------------------------------------------------------

    public ValueTask<TransactionEntity?> QueryAsync(Guid id, CancellationToken cancellationToken) =>
        transactionAccessor.QueryAsync(id, cancellationToken);

    // レシート番号は完全一致で 1 件
    public ValueTask<TransactionEntity?> QueryByReceiptNoAsync(string receiptNo, CancellationToken cancellationToken) =>
        transactionAccessor.QueryByReceiptNoAsync(receiptNo, cancellationToken);

    public async ValueTask<TransactionDetailView?> QueryDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await transactionAccessor.QueryAsync(id, cancellationToken);
        return entity is null ? null : await LoadDetailAsync(entity, cancellationToken);
    }

    public async ValueTask<TransactionDetailView?> QueryDetailByReceiptNoAsync(string receiptNo, CancellationToken cancellationToken)
    {
        var entity = await transactionAccessor.QueryByReceiptNoAsync(receiptNo, cancellationToken);
        return entity is null ? null : await LoadDetailAsync(entity, cancellationToken);
    }

    // レシート PDF (控え・再発行)。支払方法は削除済みも名前を引く
    public async ValueTask<ReceiptReportView?> QueryReceiptAsync(Guid id, CancellationToken cancellationToken)
    {
        var detail = await QueryDetailAsync(id, cancellationToken);
        if (detail is null)
        {
            return null;
        }

        var transaction = detail.Transaction;
        var store = await masterAccessor.QueryStoreAsync(transaction.StoreId, cancellationToken);
        var terminal = await masterAccessor.QueryTerminalAsync(transaction.TerminalId, cancellationToken);
        var staff = await masterAccessor.QueryStaffAsync(transaction.StaffId, cancellationToken);
        var methods = await masterAccessor.QueryPaymentMethodListAsync(null, true, cancellationToken);
        return new ReceiptReportView
        {
            Detail = detail,
            Store = store,
            TerminalName = terminal?.Name ?? String.Empty,
            StaffName = staff?.Name ?? String.Empty,
            PaymentMethodNames = methods.ToDictionary(static x => x.Id, static x => x.Name),
            TimeZone = StoreService.ResolveTimeZone(store?.TimeZone)
        };
    }

    public async ValueTask<PagedResult<TransactionEntity>> QueryPageAsync(TransactionQueryParameter parameter, CancellationToken cancellationToken)
    {
        var serialNumber = String.IsNullOrWhiteSpace(parameter.SerialNumber) ? null : parameter.SerialNumber.Trim();
        var total = await transactionAccessor.CountAsync(parameter.StoreId, parameter.TerminalId, parameter.StaffId, parameter.ShiftId, parameter.CustomerId, parameter.From, parameter.To, parameter.Type, parameter.Status, serialNumber, cancellationToken);
        var items = await transactionAccessor.QueryListAsync(parameter.StoreId, parameter.TerminalId, parameter.StaffId, parameter.ShiftId, parameter.CustomerId, parameter.From, parameter.To, parameter.Type, parameter.Status, serialNumber, parameter.Sort, parameter.Desc, parameter.Size, parameter.Page * parameter.Size, cancellationToken);
        return new PagedResult<TransactionEntity>((int)total, parameter.Page, parameter.Size, items);
    }

    // 明細などを含む
    public async ValueTask<PagedResult<TransactionDetailView>> QueryDetailPageAsync(TransactionQueryParameter parameter, CancellationToken cancellationToken)
    {
        var page = await QueryPageAsync(parameter, cancellationToken);
        var items = new List<TransactionDetailView>(page.Items.Count);
        foreach (var entity in page.Items)
        {
            items.Add(await LoadDetailAsync(entity, cancellationToken));
        }

        return new PagedResult<TransactionDetailView>(page.Total, page.Page, page.Size, items);
    }

    // 元取引に紐付く返品取引 (取消済みも含む)
    public ValueTask<List<TransactionEntity>> QueryReturnsAsync(Guid originalTransactionId, CancellationToken cancellationToken) =>
        transactionAccessor.QueryReturnListAsync(originalTransactionId, cancellationToken);

    private async ValueTask<TransactionDetailView> LoadDetailAsync(TransactionEntity entity, CancellationToken cancellationToken) =>
        new()
        {
            Transaction = entity,
            Lines = await transactionAccessor.QueryLineListAsync(entity.Id, cancellationToken),
            Serials = await transactionAccessor.QueryLineSerialListAsync(entity.Id, cancellationToken),
            Discounts = await transactionAccessor.QueryDiscountListAsync(entity.Id, cancellationToken),
            TaxSummaries = await transactionAccessor.QueryTaxSummaryListAsync(entity.Id, cancellationToken),
            Payments = await transactionAccessor.QueryPaymentListAsync(entity.Id, cancellationToken),
            Delivery = await transactionAccessor.QueryDeliveryAsync(entity.Id, cancellationToken),
            Order = await orderAccessor.QueryByTransactionAsync(entity.Id, cancellationToken)
        };

    //--------------------------------------------------------------------------------
    // Calculate
    //--------------------------------------------------------------------------------

    // 入力項目から計算項目を求める (登録しない)
    public async ValueTask<TransactionCalculation> CalculateAsync(
        TransactionType type,
        Guid? originalTransactionId,
        IEnumerable<TransactionLineEntity> lines,
        IEnumerable<TransactionDiscountEntity> discounts,
        IEnumerable<TransactionPaymentEntity> payments,
        CancellationToken cancellationToken)
    {
        var settings = await QuerySettingsAsync(cancellationToken);
        var paymentMethods = await QueryPaymentMethodsAsync(cancellationToken);
        if (type == TransactionType.Return)
        {
            var originalLines = originalTransactionId is null ? [] : await transactionAccessor.QueryLineListAsync(originalTransactionId.Value, cancellationToken);
            if (originalLines.Count == 0)
            {
                return new TransactionCalculation(null, new RuleError(ErrorCode.OriginalNotFound, RuleReason.OriginalNotFound));
            }

            var input = ToReturnInput(settings, originalLines, lines, payments, paymentMethods);
            var errors = TransactionLogic.ValidateInput(input);
            return errors.Count > 0 ? new TransactionCalculation(null, errors[0]) : new TransactionCalculation(ReturnLogic.Calculate(input), null);
        }
        else
        {
            var input = ToSalesInput(settings, lines, discounts, payments, paymentMethods);
            var errors = TransactionLogic.ValidateInput(input);
            return errors.Count > 0 ? new TransactionCalculation(null, errors[0]) : new TransactionCalculation(SalesLogic.Calculate(input), null);
        }
    }

    //--------------------------------------------------------------------------------
    // Register
    //--------------------------------------------------------------------------------

    // 冪等: 同じ id が既にあれば既存を返す (内容が違えば DuplicateMismatch)。検証は Pos.Domain の業務ルール。
    // orderId は受注から会計した販売で、完了した取引なら受注を完了にする
    public async ValueTask<TransactionResult> RegisterAsync(TransactionDetailView detail, Guid? orderId, CancellationToken cancellationToken)
    {
        var entity = detail.Transaction;
        var existing = await transactionAccessor.QueryAsync(entity.Id, cancellationToken);
        if (existing is not null)
        {
            return IsSameTransaction(existing, entity)
                ? TransactionResult.Existing(await LoadDetailAsync(existing, cancellationToken))
                : TransactionResult.DuplicateMismatch();
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var settings = await QuerySettingsAsync(cancellationToken);
        var paymentMethods = await QueryPaymentMethodsAsync(cancellationToken);
        var shift = await shiftAccessor.QueryAsync(entity.ShiftId, cancellationToken);
        var receiptNoInUse = await transactionAccessor.QueryByReceiptNoAsync(entity.ReceiptNo, cancellationToken) is not null;
        var products = (await productAccessor.QueryListByIdsAsync(detail.Lines.Select(static x => x.ProductId).Distinct().ToList(), cancellationToken)).ToDictionary(static x => x.Id);
        var customer = entity.CustomerId is null ? null : await customerAccessor.QueryAsync(entity.CustomerId.Value, cancellationToken);
        var claimed = ToClaimedResult(detail);
        var shiftFact = shift is null ? null : new ShiftFact { Id = shift.Id, Status = shift.Status, TerminalId = shift.TerminalId, StoreId = shift.StoreId };
        var dayClosed = await dailyClosingAccessor.QueryByBusinessDateAsync(entity.StoreId, entity.BusinessDate, cancellationToken) is not null;
        var order = (orderId is null) || (entity.Type != TransactionType.Sale) ? null : await orderAccessor.QueryAsync(orderId.Value, cancellationToken);
        var depositBalance = order is null ? 0m : OrderDetailView.DepositBalanceOf(order.Status, await orderAccessor.QueryDepositListAsync(order.Id, cancellationToken));
        var staff = await QueryStaffFactAsync(entity.StaffId, cancellationToken);

        TransactionValidation validation;
        if (entity.Type == TransactionType.Return)
        {
            var original = entity.OriginalTransactionId is null ? null : await transactionAccessor.QueryAsync(entity.OriginalTransactionId.Value, cancellationToken);
            var originalLines = original is null ? [] : await transactionAccessor.QueryLineListAsync(original.Id, cancellationToken);
            var input = ToReturnInput(settings, originalLines, detail.Lines, detail.Payments, paymentMethods);
            var context = new ReturnContext
            {
                StoreId = entity.StoreId,
                TerminalId = entity.TerminalId,
                Shift = shiftFact,
                ReceiptNoInUse = receiptNoInUse,
                Original = original is null ? null : new OriginalTransactionFact { Id = original.Id, Type = original.Type, Status = original.Status },
                HasCustomer = customer is not null,
                DayClosed = dayClosed,
                Staff = staff
            };
            validation = TransactionLogic.ValidateReturn(context, input, claimed);
        }
        else
        {
            var input = ToSalesInput(settings, detail.Lines, detail.Discounts, detail.Payments, paymentMethods);
            var context = new SaleContext
            {
                StoreId = entity.StoreId,
                TerminalId = entity.TerminalId,
                Shift = shiftFact,
                ReceiptNoInUse = receiptNoInUse,
                Products = products.ToDictionary(static x => x.Key, static x => new ProductFact { Id = x.Value.Id, AllowsPriceOverride = x.Value.AllowsPriceOverride, IsActive = x.Value.IsActive }),
                HasCustomer = customer is not null,
                CustomerPointBalance = customer?.PointBalance,
                DayClosed = dayClosed,
                OrderId = orderId,
                Order = order is null ? null : new OrderFact { Id = order.Id, StoreId = order.StoreId, Status = order.Status, DepositBalance = depositBalance },
                Staff = staff,
                DiscountApprovals = await QueryDiscountApprovalsAsync(detail.Discounts, cancellationToken)
            };
            validation = TransactionLogic.ValidateSale(context, input, claimed);
        }

        if (!validation.IsValid)
        {
            return TransactionResult.Invalid(validation);
        }

        // 登録 (取引一式 + 在庫 + ポイント + 端末の連番) を 1 トランザクションで。Completed 以外 (取消済みの再送) は副作用なし
        entity.CreatedAt = now;
        entity.UpdatedAt = now;
        var sale = entity.Type == TransactionType.Sale;
        try
        {
            await provider.UsingTxAsync(async (_, tx) =>
            {
                await InsertDetailAsync(tx, detail, cancellationToken);
                if (entity.Status == TransactionStatus.Completed)
                {
                    if (!sale)
                    {
                        foreach (var line in detail.Lines)
                        {
                            if (await transactionAccessor.AddReturnedQuantityAsync(tx, line.OriginalLineId ?? Guid.Empty, line.Quantity, cancellationToken) == 0)
                            {
                                throw new RuleViolationException(ErrorCode.ReturnQuantityExceeded, RuleReason.ReturnQuantityExceeded);
                            }
                        }
                    }

                    await ApplyInventoryAsync(tx, entity, detail.Lines, products, sale ? -1m : 1m, sale ? InventoryChangeType.Sale : InventoryChangeType.Return, entity.StaffId, entity.TransactedAt, now, cancellationToken);
                    if (customer is not null)
                    {
                        var balance = await ApplyPointsAsync(tx, entity, now, cancellationToken);
                        if (balance is not null)
                        {
                            entity.PointsBalanceAfter = balance;
                            await transactionAccessor.UpdatePointsBalanceAfterAsync(tx, entity.Id, balance, cancellationToken);
                        }
                    }

                    await masterAccessor.UpdateTerminalLastReceiptSeqAsync(tx, entity.TerminalId, ParseReceiptSeq(entity.ReceiptNo), now, cancellationToken);

                    // 受注から会計したら受注を完了にする (検証のあとで状態か前受金が変わっていれば取り消す)
                    if (sale && (orderId is not null) && (await orderAccessor.UpdateCompletedAsync(tx, orderId.Value, entity.Id, entity.TransactedAt, depositBalance, now, cancellationToken) == 0))
                    {
                        throw new RuleViolationException(ErrorCode.OrderNotReady, RuleReason.OrderNotReady);
                    }
                }

                // 締めた日に届いた取引は、締め直すまで日計に入らないことを示す
                if (dayClosed)
                {
                    await dailyClosingAccessor.UpdateHasLateTransactionsAsync(tx, entity.StoreId, entity.BusinessDate, now, cancellationToken);
                }

                await tx.CommitAsync(cancellationToken);
            }, cancellationToken);
        }
        catch (RuleViolationException ex)
        {
            return TransactionResult.Violated(new RuleError(ex.Code, ex.Reason));
        }

        if (sale && (orderId is not null) && (entity.Status == TransactionStatus.Completed))
        {
            detail.Order = await orderAccessor.QueryAsync(orderId.Value, cancellationToken);
        }

        changeNotification.Notify(DataChangeKind.Transaction);
        return TransactionResult.Success(detail, validation.Warnings);
    }

    private async ValueTask InsertDetailAsync(DbTransaction tx, TransactionDetailView detail, CancellationToken cancellationToken)
    {
        var entity = detail.Transaction;
        await transactionAccessor.InsertAsync(tx, entity, cancellationToken);
        foreach (var line in detail.Lines)
        {
            line.TransactionId = entity.Id;
            await transactionAccessor.InsertLineAsync(tx, line, cancellationToken);
        }

        foreach (var serial in detail.Serials)
        {
            await transactionAccessor.InsertLineSerialAsync(tx, serial, cancellationToken);
        }

        var sortNo = 0;
        foreach (var discount in detail.Discounts)
        {
            discount.TransactionId = entity.Id;
            discount.SortNo = ++sortNo;
            await transactionAccessor.InsertDiscountAsync(tx, discount, cancellationToken);
        }

        foreach (var taxSummary in detail.TaxSummaries)
        {
            taxSummary.TransactionId = entity.Id;
            await transactionAccessor.InsertTaxSummaryAsync(tx, taxSummary, cancellationToken);
        }

        foreach (var payment in detail.Payments)
        {
            payment.TransactionId = entity.Id;
            await transactionAccessor.InsertPaymentAsync(tx, payment, cancellationToken);
        }

        if (detail.Delivery is not null)
        {
            detail.Delivery.TransactionId = entity.Id;
            await transactionAccessor.InsertDeliveryAsync(tx, detail.Delivery, cancellationToken);
        }
    }

    // trackInventory の明細ごとに在庫を加減算し、変動履歴を残す
    private async ValueTask ApplyInventoryAsync(
        DbTransaction tx,
        TransactionEntity entity,
        IEnumerable<TransactionLineEntity> lines,
        Dictionary<Guid, ProductEntity> products,
        decimal sign,
        InventoryChangeType type,
        Guid staffId,
        DateTime occurredAt,
        DateTime now,
        CancellationToken cancellationToken)
    {
        foreach (var line in lines)
        {
            if (!products.TryGetValue(line.ProductId, out var product) || !product.TrackInventory)
            {
                continue;
            }

            var delta = sign * line.Quantity;
            var after = await inventoryAccessor.AddQuantityAsync(tx, entity.StoreId, line.ProductId, delta, now, cancellationToken);
            await inventoryAccessor.InsertChangeAsync(tx, new InventoryChangeEntity
            {
                Id = Guid.CreateVersion7(),
                StoreId = entity.StoreId,
                ProductId = line.ProductId,
                Type = type,
                QuantityDelta = delta,
                QuantityAfter = after,
                ReferenceType = InventoryReferenceType.Transaction,
                ReferenceId = entity.Id,
                ReferenceLineId = line.Id,
                StaffId = staffId,
                OccurredAt = occurredAt,
                CreatedAt = now
            }, cancellationToken);
        }
    }

    // ポイント履歴: Sale は Redeem → Earn、Return は Refund → Revoke。戻り値は処理後残高
    private async ValueTask<int?> ApplyPointsAsync(DbTransaction tx, TransactionEntity entity, DateTime now, CancellationToken cancellationToken)
    {
        var customerId = entity.CustomerId!.Value;
        int? balance = null;
        if (entity.Type == TransactionType.Sale)
        {
            if (entity.PointsRedeemed > 0)
            {
                balance = await AddPointsAsync(tx, customerId, entity.Id, PointHistoryType.Redeem, -entity.PointsRedeemed, entity.StaffId, entity.TransactedAt, now, cancellationToken);
            }

            if (entity.PointsEarned > 0)
            {
                balance = await AddPointsAsync(tx, customerId, entity.Id, PointHistoryType.Earn, entity.PointsEarned, entity.StaffId, entity.TransactedAt, now, cancellationToken);
            }
        }
        else
        {
            if (entity.PointsRedeemed != 0)
            {
                balance = await AddPointsAsync(tx, customerId, entity.Id, PointHistoryType.Refund, -entity.PointsRedeemed, entity.StaffId, entity.TransactedAt, now, cancellationToken);
            }

            if (entity.PointsEarned != 0)
            {
                balance = await AddPointsAsync(tx, customerId, entity.Id, PointHistoryType.Revoke, entity.PointsEarned, entity.StaffId, entity.TransactedAt, now, cancellationToken);
            }
        }

        return balance;
    }

    private async ValueTask<int> AddPointsAsync(DbTransaction tx, Guid customerId, Guid transactionId, PointHistoryType type, int points, Guid staffId, DateTime occurredAt, DateTime now, CancellationToken cancellationToken)
    {
        var balance = await customerAccessor.AddPointsAsync(tx, customerId, points, now, cancellationToken);
        await customerAccessor.InsertPointHistoryAsync(tx, new PointHistoryEntity
        {
            Id = Guid.CreateVersion7(),
            CustomerId = customerId,
            Type = type,
            Points = points,
            BalanceAfter = balance,
            TransactionId = transactionId,
            StaffId = staffId,
            OccurredAt = occurredAt,
            CreatedAt = now
        }, cancellationToken);
        return balance;
    }

    //--------------------------------------------------------------------------------
    // Void
    //--------------------------------------------------------------------------------

    // 取消: Status を Voided にし、在庫は逆方向の履歴、ポイントは Void 履歴を追加する。レジ係の取消は店長以上の承認者が要る
    public async ValueTask<TransactionResult> VoidAsync(Guid id, DateTime voidedAt, Guid staffId, Guid? approvedByStaffId, string reason, CancellationToken cancellationToken)
    {
        var entity = await transactionAccessor.QueryAsync(id, cancellationToken);
        if (entity is null)
        {
            return TransactionResult.NotFound();
        }

        var shift = await shiftAccessor.QueryAsync(entity.ShiftId, cancellationToken);
        var context = new VoidContext
        {
            Transaction = new TransactionFact { Id = entity.Id, StoreId = entity.StoreId, Type = entity.Type, Status = entity.Status, HasReturns = await transactionAccessor.CountReturnsAsync(id, cancellationToken) > 0 },
            ShiftStatus = shift?.Status,
            DayClosed = await dailyClosingAccessor.QueryByBusinessDateAsync(entity.StoreId, entity.BusinessDate, cancellationToken) is not null,
            Staff = await QueryStaffFactAsync(staffId, cancellationToken),
            ApproverId = approvedByStaffId,
            Approver = await QueryStaffFactAsync(approvedByStaffId, cancellationToken)
        };
        var validation = TransactionLogic.ValidateVoid(context);
        if (!validation.IsValid)
        {
            return TransactionResult.Invalid(validation);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var lines = await transactionAccessor.QueryLineListAsync(id, cancellationToken);
        var products = (await productAccessor.QueryListByIdsAsync(lines.Select(static x => x.ProductId).Distinct().ToList(), cancellationToken)).ToDictionary(static x => x.Id);
        var sale = entity.Type == TransactionType.Sale;
        try
        {
            await provider.UsingTxAsync(async (_, tx) =>
            {
                if (await transactionAccessor.UpdateVoidedAsync(tx, id, voidedAt, staffId, reason, now, cancellationToken) == 0)
                {
                    throw new RuleViolationException(ErrorCode.ValidationError, RuleReason.AlreadyVoided);
                }

                // 在庫 (逆方向)
                await ApplyInventoryAsync(tx, entity, lines, products, sale ? 1m : -1m, InventoryChangeType.Void, staffId, voidedAt, now, cancellationToken);

                // ポイント (付与を取り消し、利用を戻す)
                var pointsDelta = -entity.PointsEarned + entity.PointsRedeemed;
                if ((entity.CustomerId is not null) && (pointsDelta != 0))
                {
                    await AddPointsAsync(tx, entity.CustomerId.Value, entity.Id, PointHistoryType.Void, pointsDelta, staffId, voidedAt, now, cancellationToken);
                }

                if (sale)
                {
                    // 受注から会計した販売の取消は、受注を引き渡し待ちに戻す
                    await orderAccessor.UpdateReopenedAsync(tx, id, now, cancellationToken);
                }
                else
                {
                    // 返品の取消は元明細の返品数量を戻す
                    foreach (var line in lines.Where(static x => x.OriginalLineId is not null))
                    {
                        await transactionAccessor.AddReturnedQuantityAsync(tx, line.OriginalLineId!.Value, -line.Quantity, cancellationToken);
                    }
                }

                await tx.CommitAsync(cancellationToken);
            }, cancellationToken);
        }
        catch (RuleViolationException ex)
        {
            return TransactionResult.Violated(new RuleError(ex.Code, ex.Reason));
        }

        var voided = await transactionAccessor.QueryAsync(id, cancellationToken);
        changeNotification.Notify(DataChangeKind.Transaction);
        return TransactionResult.Success(await LoadDetailAsync(voided!, cancellationToken));
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private async ValueTask<SettingsEntity> QuerySettingsAsync(CancellationToken cancellationToken) =>
        await masterAccessor.QuerySettingsAsync(cancellationToken) ?? throw new InvalidOperationException("Settings not found.");

    private async ValueTask<Dictionary<Guid, PaymentMethodEntity>> QueryPaymentMethodsAsync(CancellationToken cancellationToken) =>
        (await masterAccessor.QueryPaymentMethodListAsync(null, true, cancellationToken)).ToDictionary(static x => x.Id);

    // 担当・承認者の事実 (null = 指定なし・見つからない。削除済みは無効として扱う)
    private async ValueTask<StaffFact?> QueryStaffFactAsync(Guid? id, CancellationToken cancellationToken)
    {
        if (id is null)
        {
            return null;
        }

        var staff = await masterAccessor.QueryStaffAsync(id.Value, cancellationToken);
        return staff is null ? null : new StaffFact { Id = staff.Id, Role = staff.Role, StoreId = staff.StoreId, IsActive = staff.IsActive && !staff.IsDeleted };
    }

    // 承認が必要な値引 (定義の RequiresApproval) ごとの承認者。任意の値引は承認なし
    private async ValueTask<List<DiscountApprovalFact>> QueryDiscountApprovalsAsync(IEnumerable<TransactionDiscountEntity> discounts, CancellationToken cancellationToken)
    {
        var approvals = new List<DiscountApprovalFact>();
        foreach (var discount in discounts)
        {
            if ((discount.DiscountId is null) || (await masterAccessor.QueryDiscountAsync(discount.DiscountId.Value, cancellationToken) is not { RequiresApproval: true }))
            {
                continue;
            }

            approvals.Add(new DiscountApprovalFact
            {
                LineId = discount.LineId,
                ApproverId = discount.ApprovedByStaffId,
                Approver = await QueryStaffFactAsync(discount.ApprovedByStaffId, cancellationToken)
            });
        }

        return approvals;
    }

    // 同一 id の再送かどうか (主要項目の一致で判定)
    private static bool IsSameTransaction(TransactionEntity existing, TransactionEntity entity) =>
        (existing.Type == entity.Type) &&
        (existing.TerminalId == entity.TerminalId) &&
        (existing.ShiftId == entity.ShiftId) &&
        String.Equals(existing.ReceiptNo, entity.ReceiptNo, StringComparison.Ordinal) &&
        (existing.Total == entity.Total) &&
        (existing.TransactedAt == entity.TransactedAt);

    // {店舗コード}-{端末番号:00}-{連番:000000} の連番
    private static int ParseReceiptSeq(string receiptNo)
    {
        var index = receiptNo.LastIndexOf('-');
        return (index >= 0) && Int32.TryParse(receiptNo.AsSpan(index + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var seq) ? seq : 0;
    }

    // 計算入力 (端末の入力項目と、支払方法の釣り可否)
    private static SalesInput ToSalesInput(SettingsEntity settings, IEnumerable<TransactionLineEntity> lines, IEnumerable<TransactionDiscountEntity> discounts, IEnumerable<TransactionPaymentEntity> payments, IReadOnlyDictionary<Guid, PaymentMethodEntity> paymentMethods) => new()
    {
        TaxRounding = settings.TaxRounding,
        PointBasis = settings.PointBasis,
        Lines = lines.Select(static x => new SalesInputLine
        {
            Id = x.Id,
            LineNo = x.LineNo,
            ProductId = x.ProductId,
            ListPrice = x.ListPrice,
            UnitPrice = x.UnitPrice,
            Quantity = x.Quantity,
            TaxRateId = x.TaxRateId,
            TaxRate = x.TaxRate,
            TaxIncluded = x.TaxIncluded,
            PointRate = x.PointRate
        }).ToList(),
        Discounts = discounts.Select(static x => new SalesInputDiscount { Id = x.Id, LineId = x.LineId, Type = x.Type, Value = x.Value }).ToList(),
        Payments = ToPayments(payments, paymentMethods)
    };

    private static ReturnInput ToReturnInput(SettingsEntity settings, IEnumerable<TransactionLineEntity> originalLines, IEnumerable<TransactionLineEntity> lines, IEnumerable<TransactionPaymentEntity> payments, IReadOnlyDictionary<Guid, PaymentMethodEntity> paymentMethods) => new()
    {
        TaxRounding = settings.TaxRounding,
        OriginalLines = originalLines.Select(static x => new ReturnOriginalLine
        {
            Id = x.Id,
            UnitPrice = x.UnitPrice,
            Quantity = x.Quantity,
            ReturnedQuantity = x.ReturnedQuantity,
            DiscountAmount = x.DiscountAmount,
            AllocatedDiscountAmount = x.AllocatedDiscountAmount,
            PointsEarned = x.PointsEarned,
            PointsRedeemed = x.PointsRedeemed,
            TaxRateId = x.TaxRateId,
            TaxRate = x.TaxRate,
            TaxIncluded = x.TaxIncluded
        }).ToList(),
        Lines = lines.Select(static x => new ReturnInputLine { Id = x.Id, LineNo = x.LineNo, OriginalLineId = x.OriginalLineId ?? Guid.Empty, Quantity = x.Quantity }).ToList(),
        Payments = ToPayments(payments, paymentMethods)
    };

    private static List<SalesInputPayment> ToPayments(IEnumerable<TransactionPaymentEntity> payments, IReadOnlyDictionary<Guid, PaymentMethodEntity> paymentMethods) =>
        payments.Select(x => new SalesInputPayment
        {
            Id = x.Id,
            Kind = x.Kind,
            Amount = x.Amount,
            TenderedAmount = x.TenderedAmount,
            AllowsChange = paymentMethods.TryGetValue(x.PaymentMethodId, out var method) && method.AllowsChange
        }).ToList();

    // 端末が送った計算項目
    private static SalesResult ToClaimedResult(TransactionDetailView detail)
    {
        var entity = detail.Transaction;
        return new SalesResult
        {
            Lines = detail.Lines.Select(static x => new SalesResultLine
            {
                Id = x.Id,
                LineNo = x.LineNo,
                Amount = x.Amount,
                DiscountAmount = x.DiscountAmount,
                AllocatedDiscountAmount = x.AllocatedDiscountAmount,
                NetAmount = x.NetAmount,
                PointsRedeemed = x.PointsRedeemed,
                PointsEarned = x.PointsEarned
            }).ToList(),
            Discounts = detail.Discounts.Select(static x => new SalesResultDiscount { Id = x.Id, Amount = x.Amount }).ToList(),
            TaxSummaries = detail.TaxSummaries.Select(static x => new SalesResultTaxSummary
            {
                TaxRateId = x.TaxRateId,
                Rate = x.Rate,
                TaxIncluded = x.TaxIncluded,
                TaxableAmount = x.TaxableAmount,
                TaxAmount = x.TaxAmount
            }).ToList(),
            Subtotal = entity.Subtotal,
            DiscountTotal = entity.DiscountTotal,
            NetSubtotal = entity.NetSubtotal,
            TaxTotal = entity.TaxTotal,
            Total = entity.Total,
            TenderedTotal = entity.TenderedTotal,
            ChangeAmount = entity.ChangeAmount,
            PointsEarned = entity.PointsEarned,
            PointsRedeemed = entity.PointsRedeemed
        };
    }
}
