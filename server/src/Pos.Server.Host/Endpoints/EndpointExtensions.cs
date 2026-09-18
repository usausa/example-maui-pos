namespace Pos.Server.Host.Endpoints;

using Pos.Server.Host.Infrastructure.Filters;

public static class EndpointExtensions
{
    public static RouteGroupBuilder MapApiGroup(this IEndpointRouteBuilder endpoints, string prefix) =>
        endpoints.MapGroup(prefix).AddEndpointFilter<RequestMetricsEndpointFilter>();
}
