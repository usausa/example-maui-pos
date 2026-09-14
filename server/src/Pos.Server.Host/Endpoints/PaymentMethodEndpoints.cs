namespace Pos.Server.Host.Endpoints;

using Pos.Domain.Rules;
using Pos.Server.Accessors;
using Pos.Server.Host.Application;
using Pos.Server.Host.Infrastructure.Api;
using Pos.Server.Host.Mappers;
using Pos.Shared.PaymentMethods;

using Smart.Data;

public static class PaymentMethodEndpoints
{
    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapPaymentMethodEndpoints(this WebApplication app)
    {
        var group = app.MapGroup(ApiRoutes.PaymentMethods);

        group.MapGet("/", HandleListAsync);
        group.MapGet("/{id:guid}", HandleGetAsync);
        group.MapPost("/", HandleCreateAsync);
        group.MapPut("/{id:guid}", HandleUpdateAsync);
        group.MapDelete("/{id:guid}", HandleDeleteAsync);
    }

    //--------------------------------------------------------------------------------
    // Handler
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleListAsync(
        PaymentMethodAccessor accessor,
        DateTime? updatedSince,
        CancellationToken cancellationToken,
        bool includeDeleted = false)
    {
        var items = await accessor.QueryListAsync(updatedSince, includeDeleted, cancellationToken);
        return TypedResults.Ok(new PaymentMethodListResponse { Total = items.Count, Page = 0, Size = items.Count, Items = items.Select(MasterMapper.ToPaymentMethodResponse).ToList() });
    }

    private static async ValueTask<IResult> HandleGetAsync(
        PaymentMethodAccessor accessor,
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await accessor.QueryAsync(id, cancellationToken);
        return entity is null ? ApiProblems.NotFound() : TypedResults.Ok(MasterMapper.ToPaymentMethodResponse(entity));
    }

    private static async ValueTask<IResult> HandleCreateAsync(
        PaymentMethodAccessor accessor,
        IDialect dialect,
        TimeProvider timeProvider,
        PaymentMethodCreateRequest request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var entity = MasterMapper.ToPaymentMethodEntity(request);
        entity.Id = Guid.CreateVersion7();
        entity.CreatedAt = now;
        entity.UpdatedAt = now;
        entity.Version = 1;

        if (await IsSecondPointsMethodAsync(accessor, entity.Id, request.Kind, request.IsActive, cancellationToken))
        {
            return ApiProblems.Unprocessable(ErrorCode.ValidationError, "ポイントの支払方法は 1 件だけ有効にできます");
        }

        try
        {
            await accessor.InsertAsync(entity, cancellationToken);
        }
        catch (DbException ex) when (dialect.IsDuplicate(ex))
        {
            return ApiProblems.DuplicateCode();
        }

        return TypedResults.Created($"{ApiRoutes.PaymentMethods}/{entity.Id}", MasterMapper.ToPaymentMethodResponse(entity));
    }

    private static async ValueTask<IResult> HandleUpdateAsync(
        PaymentMethodAccessor accessor,
        IDialect dialect,
        TimeProvider timeProvider,
        Guid id,
        PaymentMethodUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (await IsSecondPointsMethodAsync(accessor, id, request.Kind, request.IsActive, cancellationToken))
        {
            return ApiProblems.Unprocessable(ErrorCode.ValidationError, "ポイントの支払方法は 1 件だけ有効にできます");
        }

        int rows;
        try
        {
            rows = await accessor.UpdateAsync(id, request.Code, request.Name, request.ShortName, request.Kind, request.AllowsChange, request.RequiresReference, request.IsActive, request.SortOrder, timeProvider.GetUtcNow().UtcDateTime, request.Version, cancellationToken);
        }
        catch (DbException ex) when (dialect.IsDuplicate(ex))
        {
            return ApiProblems.DuplicateCode();
        }

        var entity = await accessor.QueryAsync(id, cancellationToken);
        if ((entity is null) || entity.IsDeleted)
        {
            return ApiProblems.NotFound();
        }

        return rows == 0 ? ApiProblems.VersionMismatch() : TypedResults.Ok(MasterMapper.ToPaymentMethodResponse(entity));
    }

    private static async ValueTask<IResult> HandleDeleteAsync(
        PaymentMethodAccessor accessor,
        TimeProvider timeProvider,
        Guid id,
        CancellationToken cancellationToken)
    {
        var rows = await accessor.DeleteAsync(id, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        return rows == 0 ? ApiProblems.NotFound() : TypedResults.NoContent();
    }

    // Kind = Points かつ有効な行はちょうど 1 件 (api-design §3.9)
    private static async ValueTask<bool> IsSecondPointsMethodAsync(PaymentMethodAccessor accessor, Guid id, PaymentKind kind, bool isActive, CancellationToken cancellationToken) =>
        (kind == PaymentKind.Points) && isActive && (await accessor.CountActivePointsAsync(id, cancellationToken) > 0);
}
