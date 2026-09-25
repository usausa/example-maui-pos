namespace Pos.Server.Host.Endpoints;

using Pos.Server.Host.Infrastructure.Filters;

public static class EndpointExtensions
{
    // API の既定は管理画面のログインか端末のトークン。管理だけ・管理者だけ・匿名はエンドポイントごとに付け直す
    public static RouteGroupBuilder MapApiGroup(this IEndpointRouteBuilder endpoints, string prefix) =>
        endpoints.MapGroup(prefix).AddEndpointFilter<RequestMetricsEndpointFilter>().RequireAuthorization(Policies.Api);
}
