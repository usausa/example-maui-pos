namespace Pos.Server.Host.Endpoints;

using Pos.Domain.Rules;
using Pos.Server.Accessors;
using Pos.Server.Host.Application;
using Pos.Server.Host.Infrastructure.Api;
using Pos.Server.Host.Infrastructure.Reports;
using Pos.Server.Host.Mappers;
using Pos.Server.Models;
using Pos.Server.Models.Entity;
using Pos.Shared.Shifts;

using Smart.Data;

// レジ開閉・現金管理 (api-design §3.13)
public static class ShiftEndpoints
{
    private static readonly string[] SortColumns = ["OpenedAt", "BusinessDate", "ClosedAt"];

    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapShiftEndpoints(this WebApplication app)
    {
        var group = app.MapGroup(ApiRoutes.Shifts);

        group.MapPost("/", HandleOpenAsync);
        group.MapGet("/current", HandleCurrentAsync);
        group.MapGet("/", HandleListAsync);
        group.MapGet("/{id:guid}", HandleGetAsync);
        group.MapPost("/{id:guid}/cash-events", HandleCashEventAsync);
        group.MapGet("/{id:guid}/cash-events", HandleCashEventListAsync);
        group.MapPost("/{id:guid}/close", HandleCloseAsync);
        group.MapGet("/{id:guid}/summary", HandleSummaryAsync);
        group.MapGet("/{id:guid}/summary/pdf", HandleSummaryPdfAsync);
    }

    //--------------------------------------------------------------------------------
    // Handler
    //--------------------------------------------------------------------------------

    // 開設。同じ id は 200 で既存を返し、端末に Open のシフトがあれば 409
    private static async ValueTask<IResult> HandleOpenAsync(
        ShiftAccessor accessor,
        IDialect dialect,
        TimeProvider timeProvider,
        ShiftOpenRequest request,
        CancellationToken cancellationToken)
    {
        var existing = await accessor.QueryAsync(request.Id, cancellationToken);
        if (existing is not null)
        {
            return (existing.TerminalId == request.TerminalId) && (existing.BusinessDate == request.BusinessDate)
                ? TypedResults.Ok(await ShiftMapper.ToResponseAsync(accessor, existing, cancellationToken))
                : ApiProblems.DuplicateIdMismatch();
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var entity = ShiftMapper.ToEntity(request);
        entity.Status = ShiftStatus.Open;
        entity.CreatedAt = now;
        entity.UpdatedAt = now;

        try
        {
            await accessor.InsertAsync(entity, cancellationToken);
        }
        catch (DbException ex) when (dialect.IsDuplicate(ex))
        {
            return ApiProblems.Problem(StatusCodes.Status409Conflict, ErrorCode.TerminalHasOpenShift, "この端末には開設中のシフトがあります");
        }

        return TypedResults.Created($"{ApiRoutes.Shifts}/{entity.Id}", await ShiftMapper.ToResponseAsync(accessor, entity, cancellationToken));
    }

    private static async ValueTask<IResult> HandleCurrentAsync(
        ShiftAccessor accessor,
        Guid terminalId,
        CancellationToken cancellationToken)
    {
        var entity = await accessor.QueryCurrentAsync(terminalId, cancellationToken);
        return entity is null ? ApiProblems.NotFound("開設中のシフトはありません") : TypedResults.Ok(await ShiftMapper.ToResponseAsync(accessor, entity, cancellationToken));
    }

    private static async ValueTask<IResult> HandleListAsync(
        ShiftAccessor accessor,
        Guid? storeId,
        Guid? terminalId,
        ShiftStatus? status,
        DateOnly? from,
        DateOnly? to,
        string? sort,
        CancellationToken cancellationToken,
        bool desc = true,
        [Range(0, Int32.MaxValue)] int page = 0,
        [Range(1, ApiHelper.MaxPageSize)] int size = ApiHelper.DefaultPageSize)
    {
        var total = await accessor.CountAsync(storeId, terminalId, status, from, to, cancellationToken);
        var entities = await accessor.QueryListAsync(storeId, terminalId, status, from, to, SqlHelper.NormalizeSort(SortColumns, "OpenedAt", sort, desc), size, page * size, cancellationToken);
        var items = new List<ShiftResponse>(entities.Count);
        foreach (var entity in entities)
        {
            items.Add(await ShiftMapper.ToResponseAsync(accessor, entity, cancellationToken));
        }

        return TypedResults.Ok(new ShiftListResponse { Total = (int)total, Page = page, Size = size, Items = items });
    }

    private static async ValueTask<IResult> HandleGetAsync(
        ShiftAccessor accessor,
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await accessor.QueryAsync(id, cancellationToken);
        return entity is null ? ApiProblems.NotFound() : TypedResults.Ok(await ShiftMapper.ToResponseAsync(accessor, entity, cancellationToken));
    }

    // 入出金 (Open のみ)。同じ id は 200 で既存を返す
    private static async ValueTask<IResult> HandleCashEventAsync(
        ShiftAccessor accessor,
        TimeProvider timeProvider,
        Guid id,
        CashEventRequest request,
        CancellationToken cancellationToken)
    {
        var existing = await accessor.QueryCashEventAsync(request.Id, cancellationToken);
        if (existing is not null)
        {
            return (existing.ShiftId == id) && (existing.Amount == request.Amount) && (existing.Type == request.Type)
                ? TypedResults.Ok(ShiftMapper.ToCashEventResponse(existing))
                : ApiProblems.DuplicateIdMismatch();
        }

        var shift = await accessor.QueryAsync(id, cancellationToken);
        if (shift is null)
        {
            return ApiProblems.Unprocessable(ErrorCode.ShiftNotFound, "シフトが見つかりません");
        }

        if (shift.Status != ShiftStatus.Open)
        {
            return ApiProblems.Unprocessable(ErrorCode.ShiftClosed, "精算済みのシフトには登録できません");
        }

        var entity = ShiftMapper.ToCashEventEntity(request);
        entity.ShiftId = id;
        entity.CreatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await accessor.InsertCashEventAsync(entity, cancellationToken);

        return TypedResults.Created($"{ApiRoutes.Shifts}/{id}/cash-events/{entity.Id}", ShiftMapper.ToCashEventResponse(entity));
    }

    private static async ValueTask<IResult> HandleCashEventListAsync(
        ShiftAccessor accessor,
        Guid id,
        CancellationToken cancellationToken,
        [Range(0, Int32.MaxValue)] int page = 0,
        [Range(1, ApiHelper.MaxPageSize)] int size = ApiHelper.DefaultPageSize)
    {
        if (await accessor.QueryAsync(id, cancellationToken) is null)
        {
            return ApiProblems.NotFound();
        }

        var total = await accessor.CountCashEventsAsync(id, cancellationToken);
        var items = await accessor.QueryCashEventListAsync(id, size, page * size, cancellationToken);
        return TypedResults.Ok(new CashEventListResponse { Total = (int)total, Page = page, Size = size, Items = items.Select(ShiftMapper.ToCashEventResponse).ToList() });
    }

    // 精算: 集計を確定して Closed にする (取引・入出金は送信済みであること、api-design §3.13)
    private static async ValueTask<IResult> HandleCloseAsync(
        ShiftAccessor accessor,
        IDbProvider provider,
        TimeProvider timeProvider,
        Guid id,
        ShiftCloseRequest request,
        CancellationToken cancellationToken)
    {
        var shift = await accessor.QueryAsync(id, cancellationToken);
        if (shift is null)
        {
            return ApiProblems.NotFound();
        }

        if (shift.Status == ShiftStatus.Closed)
        {
            // 同じ内容の再送は 200
            return shift.ActualCash == request.ActualCash
                ? TypedResults.Ok(await ShiftMapper.ToResponseAsync(accessor, shift, cancellationToken))
                : ApiProblems.Unprocessable(ErrorCode.ShiftClosed, "既に精算済みです");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var totals = await accessor.QueryTotalsAsync(id, cancellationToken) ?? new ShiftTotals(0, 0, 0, 0, 0, 0, 0, 0, 0);
        var expectedCash = ShiftMapper.ExpectedCash(shift.OpeningCash, totals);

        await provider.UsingTxAsync(async (_, tx) =>
        {
            await accessor.CloseAsync(tx, id, request.ClosedAt, request.ClosedByStaffId, request.ActualCash, expectedCash, request.ActualCash - expectedCash, totals, request.Note, now, cancellationToken);
            foreach (var denomination in request.Denominations)
            {
                await accessor.InsertDenominationAsync(tx, new ShiftDenominationEntity { ShiftId = id, Denomination = denomination.Denomination, Count = denomination.Count }, cancellationToken);
            }

            await tx.CommitAsync(cancellationToken);
        }, cancellationToken);

        var closed = await accessor.QueryAsync(id, cancellationToken);
        return TypedResults.Ok(await ShiftMapper.ToResponseAsync(accessor, closed!, cancellationToken));
    }

    private static async ValueTask<IResult> HandleSummaryAsync(
        ShiftAccessor accessor,
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await accessor.QueryAsync(id, cancellationToken);
        return entity is null ? ApiProblems.NotFound() : TypedResults.Ok(await ShiftMapper.ToSummaryResponseAsync(accessor, entity, cancellationToken));
    }

    // 精算レポート PDF (D-37)
    private static async ValueTask<IResult> HandleSummaryPdfAsync(
        ShiftAccessor accessor,
        StoreAccessor storeAccessor,
        TerminalAccessor terminalAccessor,
        StaffAccessor staffAccessor,
        ShiftReportBuilder reportBuilder,
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await accessor.QueryAsync(id, cancellationToken);
        if (entity is null)
        {
            return ApiProblems.NotFound();
        }

        var summary = await ShiftMapper.ToSummaryResponseAsync(accessor, entity, cancellationToken);
        var store = await storeAccessor.QueryAsync(entity.StoreId, cancellationToken);
        var terminal = await terminalAccessor.QueryAsync(entity.TerminalId, cancellationToken);
        var openedBy = await staffAccessor.QueryAsync(entity.OpenedByStaffId, cancellationToken);
        var closedBy = entity.ClosedByStaffId is null ? null : await staffAccessor.QueryAsync(entity.ClosedByStaffId.Value, cancellationToken);
        var data = new ShiftReportData(
            summary,
            store?.Name ?? String.Empty,
            terminal?.Name ?? String.Empty,
            openedBy?.Name ?? String.Empty,
            closedBy?.Name,
            ReportText.ResolveTimeZone(store?.TimeZone));

        var bytes = reportBuilder.Build(data);
        return TypedResults.File(bytes, "application/pdf", $"shift-report-{entity.BusinessDate:yyyyMMdd}-{terminal?.TerminalNo ?? 0:00}.pdf");
    }
}
