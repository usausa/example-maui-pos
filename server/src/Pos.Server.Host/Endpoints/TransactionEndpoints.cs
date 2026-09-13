namespace Pos.Server.Host.Endpoints;

using Pos.Domain.Rules;
using Pos.Domain.Sales;
using Pos.Server.Accessors;
using Pos.Server.Host.Application;
using Pos.Server.Host.Infrastructure.Api;
using Pos.Server.Host.Mappers;
using Pos.Server.Models.Entity;
using Pos.Shared.Transactions;

using Smart.Data;

// 取引 (api-design §3.12)。登録は db-design §5.1 の 1 トランザクション
public static class TransactionEndpoints
{
    private static readonly string[] SortColumns = ["TransactedAt", "ReceiptNo", "Total", "BusinessDate"];

    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapTransactionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup(ApiRoutes.Transactions);

        group.MapPost("/", HandleCreateAsync);
        group.MapPost("/calculate", HandleCalculateAsync);
        group.MapGet("/", HandleListAsync);
        group.MapGet("/lookup", HandleLookupAsync);
        group.MapGet("/{id:guid}", HandleGetAsync);
        group.MapPost("/{id:guid}/void", HandleVoidAsync);
    }

    //--------------------------------------------------------------------------------
    // Create
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleCreateAsync(
        TransactionAccessor transactionAccessor,
        ShiftAccessor shiftAccessor,
        ProductAccessor productAccessor,
        CustomerAccessor customerAccessor,
        TerminalAccessor terminalAccessor,
        InventoryAccessor inventoryAccessor,
        SettingsAccessor settingsAccessor,
        PaymentMethodAccessor paymentMethodAccessor,
        IDbProvider provider,
        TimeProvider timeProvider,
        TransactionRequest request,
        CancellationToken cancellationToken)
    {
        // 冪等: 同じ id が既にあれば既存を返す (内容が違えば 409)
        var existing = await transactionAccessor.QueryAsync(request.Id, cancellationToken);
        if (existing is not null)
        {
            return IsSameTransaction(existing, request)
                ? TypedResults.Ok(await TransactionMapper.ToResponseAsync(transactionAccessor, existing, cancellationToken))
                : ApiProblems.DuplicateIdMismatch();
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var settings = await settingsAccessor.QueryAsync(cancellationToken) ?? throw new InvalidOperationException("Settings not found.");
        var paymentMethods = (await paymentMethodAccessor.QueryListAsync(null, true, cancellationToken)).ToDictionary(static x => x.Id);
        var shift = await shiftAccessor.QueryAsync(request.ShiftId, cancellationToken);
        var receiptNoInUse = await transactionAccessor.QueryByReceiptNoAsync(request.ReceiptNo, cancellationToken) is not null;
        var products = (await productAccessor.QueryByIdsAsync(request.Lines.Select(static x => x.ProductId).Distinct().ToList(), cancellationToken)).ToDictionary(static x => x.Id);
        var customer = request.CustomerId is null ? null : await customerAccessor.QueryAsync(request.CustomerId.Value, cancellationToken);
        var claimed = TransactionMapper.ToClaimedResult(request);
        var calculateRequest = TransactionMapper.ToCalculateRequest(request);
        var shiftFact = shift is null ? null : new ShiftFact { Id = shift.Id, Status = shift.Status, TerminalId = shift.TerminalId };

        // 検証 (Pos.Domain の業務ルール)
        TransactionValidation validation;
        if (request.Type == TransactionType.Return)
        {
            var original = request.OriginalTransactionId is null ? null : await transactionAccessor.QueryAsync(request.OriginalTransactionId.Value, cancellationToken);
            var originalLines = original is null ? [] : await transactionAccessor.QueryLinesAsync(original.Id, cancellationToken);
            var input = TransactionMapper.ToReturnInput(calculateRequest, settings.TaxRounding, originalLines, paymentMethods);
            var context = new ReturnContext
            {
                TerminalId = request.TerminalId,
                Shift = shiftFact,
                ReceiptNoInUse = receiptNoInUse,
                Original = original is null ? null : new OriginalTransactionFact { Id = original.Id, Type = original.Type, Status = original.Status },
                HasCustomer = customer is not null
            };
            validation = TransactionRules.ValidateReturn(context, input, claimed);
        }
        else
        {
            var input = TransactionMapper.ToSalesInput(calculateRequest, settings.TaxRounding, settings.PointBasis, paymentMethods);
            var context = new SaleContext
            {
                TerminalId = request.TerminalId,
                Shift = shiftFact,
                ReceiptNoInUse = receiptNoInUse,
                Products = products.ToDictionary(static x => x.Key, static x => new ProductFact { Id = x.Value.Id, AllowsPriceOverride = x.Value.AllowsPriceOverride, IsActive = x.Value.IsActive }),
                HasCustomer = customer is not null,
                CustomerPointBalance = customer?.PointBalance
            };
            validation = TransactionRules.ValidateSale(context, input, claimed);
        }

        var expected = validation.Expected is null ? null : TransactionMapper.ToCalculationResponse(validation.Expected);
        if (!validation.IsValid)
        {
            return ApiProblems.FromValidation(validation, expected);
        }

        // 登録 (取引一式 + 在庫 + ポイント + 端末の連番) を 1 トランザクションで
        var entity = TransactionMapper.ToEntity(request);
        entity.CreatedAt = now;
        entity.UpdatedAt = now;
        if (request.Void is not null)
        {
            entity.VoidedAt = request.Void.VoidedAt;
            entity.VoidedByStaffId = request.Void.VoidedByStaffId;
            entity.VoidReason = request.Void.Reason;
        }

        var applySideEffects = request.Status == TransactionStatus.Completed;
        try
        {
            await provider.UsingTxAsync(async (_, tx) =>
            {
                await InsertTransactionAsync(transactionAccessor, tx, entity, request, cancellationToken);

                if (applySideEffects)
                {
                    if (request.Type == TransactionType.Return)
                    {
                        foreach (var line in request.Lines)
                        {
                            if (await transactionAccessor.AddReturnedQuantityAsync(tx, line.OriginalLineId ?? Guid.Empty, line.Quantity, cancellationToken) == 0)
                            {
                                throw new RuleViolationException(ErrorCode.ReturnQuantityExceeded, "返品数量が返品可能な数量を超えています");
                            }
                        }
                    }

                    await ApplyInventoryAsync(inventoryAccessor, tx, entity, request.Lines, products, request.Type == TransactionType.Sale ? -1m : 1m, request.Type == TransactionType.Sale ? InventoryChangeType.Sale : InventoryChangeType.Return, now, cancellationToken);

                    if (customer is not null)
                    {
                        var balance = await ApplyPointsAsync(customerAccessor, tx, entity, now, cancellationToken);
                        if (balance is not null)
                        {
                            entity.PointsBalanceAfter = balance;
                            await transactionAccessor.UpdatePointsBalanceAfterAsync(tx, entity.Id, balance, cancellationToken);
                        }
                    }

                    await terminalAccessor.UpdateLastReceiptSeqAsync(tx, request.TerminalId, ParseReceiptSeq(request.ReceiptNo), now, cancellationToken);
                }

                await tx.CommitAsync(cancellationToken);
            }, cancellationToken);
        }
        catch (RuleViolationException ex)
        {
            return ApiProblems.Unprocessable(ex.Code, ex.Message);
        }

        var response = await TransactionMapper.ToResponseAsync(transactionAccessor, entity, cancellationToken);
        response.Warnings = validation.Warnings.Select(static x => new TransactionResponseWarning { Code = x.Code.ToCode(), Message = x.Message, LineId = x.LineId }).ToList();
        return TypedResults.Created($"{ApiRoutes.Transactions}/{entity.Id}", response);
    }

    private static async ValueTask InsertTransactionAsync(TransactionAccessor accessor, DbTransaction tx, TransactionEntity entity, TransactionRequest request, CancellationToken cancellationToken)
    {
        await accessor.InsertAsync(tx, entity, cancellationToken);

        foreach (var line in request.Lines)
        {
            var lineEntity = TransactionMapper.ToLineEntity(line);
            lineEntity.TransactionId = entity.Id;
            await accessor.InsertLineAsync(tx, lineEntity, cancellationToken);
            foreach (var serialNumber in line.SerialNumbers)
            {
                await accessor.InsertLineSerialAsync(tx, new TransactionLineSerialEntity { TransactionLineId = line.Id, SerialNumber = serialNumber }, cancellationToken);
            }
        }

        var sortNo = 0;
        foreach (var discount in request.Discounts)
        {
            var discountEntity = TransactionMapper.ToDiscountEntity(discount);
            discountEntity.TransactionId = entity.Id;
            discountEntity.SortNo = ++sortNo;
            await accessor.InsertDiscountAsync(tx, discountEntity, cancellationToken);
        }

        foreach (var taxSummary in request.TaxSummaries)
        {
            var taxEntity = TransactionMapper.ToTaxSummaryEntity(taxSummary);
            taxEntity.TransactionId = entity.Id;
            await accessor.InsertTaxSummaryAsync(tx, taxEntity, cancellationToken);
        }

        foreach (var payment in request.Payments)
        {
            var paymentEntity = TransactionMapper.ToPaymentEntity(payment);
            paymentEntity.TransactionId = entity.Id;
            await accessor.InsertPaymentAsync(tx, paymentEntity, cancellationToken);
        }

        if (request.Delivery is not null)
        {
            var deliveryEntity = TransactionMapper.ToDeliveryEntity(request.Delivery);
            deliveryEntity.TransactionId = entity.Id;
            await accessor.InsertDeliveryAsync(tx, deliveryEntity, cancellationToken);
        }
    }

    // trackInventory の明細ごとに在庫を加減算し、変動履歴を残す
    private static async ValueTask ApplyInventoryAsync(
        InventoryAccessor accessor,
        DbTransaction tx,
        TransactionEntity entity,
        IEnumerable<TransactionRequestLine> lines,
        Dictionary<Guid, ProductEntity> products,
        decimal sign,
        InventoryChangeType type,
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
            var after = await accessor.AddQuantityAsync(tx, entity.StoreId, line.ProductId, delta, now, cancellationToken);
            await accessor.InsertChangeAsync(tx, new InventoryChangeEntity
            {
                Id = Guid.CreateVersion7(),
                StoreId = entity.StoreId,
                ProductId = line.ProductId,
                Type = type,
                QuantityDelta = delta,
                QuantityAfter = after,
                ReferenceType = "Transaction",
                ReferenceId = entity.Id,
                ReferenceLineId = line.Id,
                StaffId = entity.StaffId,
                OccurredAt = entity.TransactedAt,
                CreatedAt = now
            }, cancellationToken);
        }
    }

    // ポイント履歴: Sale は Redeem → Earn、Return は Refund → Revoke (api-design §3.12)。戻り値は処理後残高
    private static async ValueTask<int?> ApplyPointsAsync(CustomerAccessor accessor, DbTransaction tx, TransactionEntity entity, DateTime now, CancellationToken cancellationToken)
    {
        var customerId = entity.CustomerId!.Value;
        int? balance = null;

        if (entity.Type == TransactionType.Sale)
        {
            if (entity.PointsRedeemed > 0)
            {
                balance = await AddPointsAsync(accessor, tx, customerId, entity, PointHistoryType.Redeem, -entity.PointsRedeemed, now, cancellationToken);
            }

            if (entity.PointsEarned > 0)
            {
                balance = await AddPointsAsync(accessor, tx, customerId, entity, PointHistoryType.Earn, entity.PointsEarned, now, cancellationToken);
            }
        }
        else
        {
            if (entity.PointsRedeemed != 0)
            {
                balance = await AddPointsAsync(accessor, tx, customerId, entity, PointHistoryType.Refund, -entity.PointsRedeemed, now, cancellationToken);
            }

            if (entity.PointsEarned != 0)
            {
                balance = await AddPointsAsync(accessor, tx, customerId, entity, PointHistoryType.Revoke, entity.PointsEarned, now, cancellationToken);
            }
        }

        return balance;
    }

    private static async ValueTask<int> AddPointsAsync(CustomerAccessor accessor, DbTransaction tx, Guid customerId, TransactionEntity entity, PointHistoryType type, int points, DateTime now, CancellationToken cancellationToken)
    {
        var balance = await accessor.AddPointsAsync(tx, customerId, points, now, cancellationToken);
        await accessor.InsertPointHistoryAsync(tx, new PointHistoryEntity
        {
            Id = Guid.CreateVersion7(),
            CustomerId = customerId,
            Type = type,
            Points = points,
            BalanceAfter = balance,
            TransactionId = entity.Id,
            StaffId = entity.StaffId,
            OccurredAt = entity.TransactedAt,
            CreatedAt = now
        }, cancellationToken);
        return balance;
    }

    //--------------------------------------------------------------------------------
    // Calculate
    //--------------------------------------------------------------------------------

    // 入力項目だけを送り、計算項目を返す (登録しない)
    private static async ValueTask<IResult> HandleCalculateAsync(
        TransactionAccessor transactionAccessor,
        SettingsAccessor settingsAccessor,
        PaymentMethodAccessor paymentMethodAccessor,
        TransactionCalculateRequest request,
        CancellationToken cancellationToken)
    {
        var settings = await settingsAccessor.QueryAsync(cancellationToken) ?? throw new InvalidOperationException("Settings not found.");
        var paymentMethods = (await paymentMethodAccessor.QueryListAsync(null, true, cancellationToken)).ToDictionary(static x => x.Id);

        SalesResult result;
        if (request.Type == TransactionType.Return)
        {
            var originalLines = request.OriginalTransactionId is null ? [] : await transactionAccessor.QueryLinesAsync(request.OriginalTransactionId.Value, cancellationToken);
            if (originalLines.Count == 0)
            {
                return ApiProblems.Unprocessable(ErrorCode.OriginalNotFound, "元取引が見つかりません");
            }

            var input = TransactionMapper.ToReturnInput(request, settings.TaxRounding, originalLines, paymentMethods);
            var errors = TransactionRules.ValidateInput(input);
            if (errors.Count > 0)
            {
                return ApiProblems.Unprocessable(errors[0].Code, errors[0].Message);
            }

            result = ReturnCalculator.Calculate(input);
        }
        else
        {
            var input = TransactionMapper.ToSalesInput(request, settings.TaxRounding, settings.PointBasis, paymentMethods);
            var errors = TransactionRules.ValidateInput(input);
            if (errors.Count > 0)
            {
                return ApiProblems.Unprocessable(errors[0].Code, errors[0].Message);
            }

            result = SalesCalculator.Calculate(input);
        }

        return TypedResults.Ok(TransactionMapper.ToCalculationResponse(result));
    }

    //--------------------------------------------------------------------------------
    // Query
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleListAsync(
        TransactionAccessor accessor,
        Guid? storeId,
        Guid? terminalId,
        Guid? staffId,
        Guid? shiftId,
        Guid? customerId,
        DateOnly? from,
        DateOnly? to,
        TransactionType? type,
        TransactionStatus? status,
        string? sort,
        CancellationToken cancellationToken,
        bool desc = true,
        [Range(0, Int32.MaxValue)] int page = 0,
        [Range(1, ApiHelper.MaxPageSize)] int size = ApiHelper.DefaultPageSize)
    {
        var total = await accessor.CountAsync(storeId, terminalId, staffId, shiftId, customerId, from, to, type, status, cancellationToken);
        var entities = await accessor.QueryListAsync(storeId, terminalId, staffId, shiftId, customerId, from, to, type, status, SqlHelper.NormalizeSort(SortColumns, "TransactedAt", sort, desc), size, page * size, cancellationToken);
        var items = new List<TransactionResponse>(entities.Count);
        foreach (var entity in entities)
        {
            items.Add(await TransactionMapper.ToResponseAsync(accessor, entity, cancellationToken));
        }

        return TypedResults.Ok(new TransactionListResponse { Total = (int)total, Page = page, Size = size, Items = items });
    }

    // 返品時のレシート番号検索
    private static async ValueTask<IResult> HandleLookupAsync(
        TransactionAccessor accessor,
        string? receiptNo,
        CancellationToken cancellationToken)
    {
        if (String.IsNullOrEmpty(receiptNo))
        {
            return ApiProblems.Problem(StatusCodes.Status400BadRequest, ErrorCode.ValidationError, "receiptNo を指定してください");
        }

        var entity = await accessor.QueryByReceiptNoAsync(receiptNo, cancellationToken);
        return entity is null ? ApiProblems.NotFound("取引が見つかりません") : TypedResults.Ok(await TransactionMapper.ToResponseAsync(accessor, entity, cancellationToken));
    }

    private static async ValueTask<IResult> HandleGetAsync(
        TransactionAccessor accessor,
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await accessor.QueryAsync(id, cancellationToken);
        return entity is null ? ApiProblems.NotFound() : TypedResults.Ok(await TransactionMapper.ToResponseAsync(accessor, entity, cancellationToken));
    }

    //--------------------------------------------------------------------------------
    // Void
    //--------------------------------------------------------------------------------

    // 取消: Status を Voided にし、在庫は逆方向の履歴、ポイントは Void 履歴を追加する (db-design §5.2)
    private static async ValueTask<IResult> HandleVoidAsync(
        TransactionAccessor transactionAccessor,
        ShiftAccessor shiftAccessor,
        ProductAccessor productAccessor,
        CustomerAccessor customerAccessor,
        InventoryAccessor inventoryAccessor,
        IDbProvider provider,
        TimeProvider timeProvider,
        Guid id,
        TransactionVoidRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await transactionAccessor.QueryAsync(id, cancellationToken);
        if (entity is null)
        {
            return ApiProblems.NotFound("取引が見つかりません");
        }

        var shift = await shiftAccessor.QueryAsync(entity.ShiftId, cancellationToken);
        var context = new VoidContext
        {
            Transaction = new TransactionFact { Id = entity.Id, Type = entity.Type, Status = entity.Status, HasReturns = await transactionAccessor.CountReturnsAsync(id, cancellationToken) > 0 },
            ShiftStatus = shift?.Status
        };
        var validation = TransactionRules.ValidateVoid(context);
        if (!validation.IsValid)
        {
            return ApiProblems.FromValidation(validation, null);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var lines = await transactionAccessor.QueryLinesAsync(id, cancellationToken);
        var products = (await productAccessor.QueryByIdsAsync(lines.Select(static x => x.ProductId).Distinct().ToList(), cancellationToken)).ToDictionary(static x => x.Id);

        try
        {
            await provider.UsingTxAsync(async (_, tx) =>
            {
                if (await transactionAccessor.VoidAsync(tx, id, request.VoidedAt, request.StaffId, request.Reason, now, cancellationToken) == 0)
                {
                    throw new RuleViolationException(ErrorCode.ValidationError, "取消済みの取引です");
                }

                // 在庫 (逆方向)
                var sign = entity.Type == TransactionType.Sale ? 1m : -1m;
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
                        Type = InventoryChangeType.Void,
                        QuantityDelta = delta,
                        QuantityAfter = after,
                        ReferenceType = "Transaction",
                        ReferenceId = entity.Id,
                        ReferenceLineId = line.Id,
                        StaffId = request.StaffId,
                        OccurredAt = request.VoidedAt,
                        CreatedAt = now
                    }, cancellationToken);
                }

                // ポイント (付与を取り消し、利用を戻す)
                var pointsDelta = -entity.PointsEarned + entity.PointsRedeemed;
                if ((entity.CustomerId is not null) && (pointsDelta != 0))
                {
                    var balance = await customerAccessor.AddPointsAsync(tx, entity.CustomerId.Value, pointsDelta, now, cancellationToken);
                    await customerAccessor.InsertPointHistoryAsync(tx, new PointHistoryEntity
                    {
                        Id = Guid.CreateVersion7(),
                        CustomerId = entity.CustomerId.Value,
                        Type = PointHistoryType.Void,
                        Points = pointsDelta,
                        BalanceAfter = balance,
                        TransactionId = entity.Id,
                        StaffId = request.StaffId,
                        OccurredAt = request.VoidedAt,
                        CreatedAt = now
                    }, cancellationToken);
                }

                // 返品の取消は元明細の返品数量を戻す
                if (entity.Type == TransactionType.Return)
                {
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
            return ApiProblems.Unprocessable(ex.Code, ex.Message);
        }

        var voided = await transactionAccessor.QueryAsync(id, cancellationToken);
        return TypedResults.Ok(await TransactionMapper.ToResponseAsync(transactionAccessor, voided!, cancellationToken));
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    // 同一 id の再送かどうか (主要項目の一致で判定)
    private static bool IsSameTransaction(TransactionEntity existing, TransactionRequest request) =>
        (existing.Type == request.Type) &&
        (existing.TerminalId == request.TerminalId) &&
        (existing.ShiftId == request.ShiftId) &&
        String.Equals(existing.ReceiptNo, request.ReceiptNo, StringComparison.Ordinal) &&
        (existing.Total == request.Total) &&
        (existing.TransactedAt == request.TransactedAt);

    // {店舗コード}-{端末番号:00}-{連番:000000} の連番
    private static int ParseReceiptSeq(string receiptNo)
    {
        var index = receiptNo.LastIndexOf('-');
        return (index >= 0) && Int32.TryParse(receiptNo.AsSpan(index + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var seq) ? seq : 0;
    }
}
