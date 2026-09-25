namespace Pos.Server.Host.Endpoints;

using Pos.Contract.Terminals;
using Pos.Server.Host.Helpers;
using Pos.Server.Models.Entity;
using Pos.Server.Services;

using Smart.Mapper;

public static partial class TerminalEndpoints
{
    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapTerminalEndpoints(this WebApplication app)
    {
        var group = app.MapApiGroup(ApiRoutes.Terminals);
        group.MapGet("/", HandleListAsync);
        group.MapGet("/{id:guid}", HandleGetAsync);
        group.MapPost("/", HandleCreateAsync).RequireAuthorization(Policies.Administrator);
        group.MapPut("/{id:guid}", HandleUpdateAsync).RequireAuthorization(Policies.Administrator);
        group.MapDelete("/{id:guid}", HandleDeleteAsync).RequireAuthorization(Policies.Administrator);

        // 端末の登録 (管理画面で発行したペアリングコードでトークンを受け取る) と、登録済みの端末からの通信
        group.MapPost("/pair", HandlePairAsync).AllowAnonymous().RequireRateLimiting(RateLimits.Auth);
        group.MapPost("/me/heartbeat", HandleHeartbeatAsync).RequireAuthorization(Policies.Terminal);
    }

    //--------------------------------------------------------------------------------
    // Mapper
    //--------------------------------------------------------------------------------

    [Mapper]
    internal static partial TerminalResponseItem ToResponse(TerminalEntity entity);

    [Mapper]
    private static partial TerminalEntity ToEntity(TerminalCreateRequest request);

    [Mapper]
    private static partial TerminalEntity ToEntity(TerminalUpdateRequest request);

    //--------------------------------------------------------------------------------
    // Handler
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleListAsync(
        TerminalService service,
        Guid? storeId,
        DateTime? updatedSince,
        string? sort,
        CancellationToken cancellationToken,
        bool includeDeleted = false,
        bool desc = false,
        [Range(0, Int32.MaxValue)] int page = 0,
        [Range(1, ApiDefaults.MaxPageSize)] int size = ApiDefaults.PageSize)
    {
        var result = await service.QueryPageAsync(storeId, updatedSince, includeDeleted, EnumHelper.Parse(sort, TerminalSort.TerminalNo), desc, page, size, cancellationToken);
        return TypedResults.Ok(new TerminalResponse { Total = result.Total, Page = result.Page, Size = result.Size, Items = result.Items.Select(ToResponse).ToList() });
    }

    private static async ValueTask<IResult> HandleGetAsync(
        TerminalService service,
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await service.QueryAsync(id, cancellationToken);
        return entity is null ? ApiProblems.NotFound() : TypedResults.Ok(ToResponse(entity));
    }

    private static async ValueTask<IResult> HandleCreateAsync(
        TerminalService service,
        TerminalCreateRequest request,
        CancellationToken cancellationToken)
    {
        var entity = ToEntity(request);
        var status = await service.InsertAsync(entity, cancellationToken);
        return status == DataWriteStatus.Success
            ? TypedResults.Created($"{ApiRoutes.Terminals}/{entity.Id}", ToResponse(entity))
            : ApiProblems.DuplicateCode("端末番号が重複しています");
    }

    private static async ValueTask<IResult> HandleUpdateAsync(
        TerminalService service,
        Guid id,
        TerminalUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var entity = ToEntity(request);
        entity.Id = id;
        var result = await service.UpdateAsync(entity, cancellationToken);
        return result.Status == DataWriteStatus.Success
            ? TypedResults.Ok(ToResponse(result.Entity!))
            : ApiProblems.FromStatus(result.Status, duplicateTitle: "端末番号が重複しています");
    }

    // コードの不一致・期限切れ・使用済み、無効・削除済みの端末は 422
    private static async ValueTask<IResult> HandlePairAsync(
        TerminalTokenService tokenService,
        StoreService storeService,
        TerminalPairRequest request,
        CancellationToken cancellationToken)
    {
        var result = await tokenService.PairAsync(request.PairingCode, request.DeviceName, request.AppVersion, cancellationToken);
        if (!result.IsSuccess)
        {
            return ApiProblems.PairingCodeInvalid();
        }

        var store = await storeService.QueryAsync(result.Terminal!.StoreId, cancellationToken);
        return store is null
            ? ApiProblems.PairingCodeInvalid()
            : TypedResults.Ok(new TerminalPairResponse { Token = result.Token!, Terminal = ToResponse(result.Terminal), Store = StoreEndpoints.ToResponse(store) });
    }

    // 最終通信時刻とアプリのバージョンを記録する (ダッシュボードの通信状態)
    private static async ValueTask<IResult> HandleHeartbeatAsync(
        TerminalService service,
        ClaimsPrincipal user,
        TerminalHeartbeatRequest request,
        CancellationToken cancellationToken)
    {
        // 認証を無効にしていても、トークンのない要求はどの端末か分からない
        if (AuthClaims.TerminalOf(user) is not { } terminal)
        {
            return TypedResults.Unauthorized();
        }

        await service.UpdateSeenAsync(terminal.TerminalId, request.AppVersion, cancellationToken);
        return TypedResults.NoContent();
    }

    // 開設中のシフトがある端末は削除できません
    private static async ValueTask<IResult> HandleDeleteAsync(
        TerminalService service,
        Guid id,
        CancellationToken cancellationToken)
    {
        var status = await service.DeleteAsync(id, cancellationToken);
        return status == DataWriteStatus.Success ? TypedResults.NoContent() : ApiProblems.FromStatus(status, inUseTitle: "開設中のシフトがある端末は削除できません");
    }
}
