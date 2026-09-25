namespace Pos.Server.Host.Application.Authentication;

using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Authentication;

using Pos.Server.Services;

// 端末のトークン (Authorization: Bearer)。要求ごとに DB で照合するので、管理画面での解除が次の要求から効く
public sealed class TerminalAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private const string BearerPrefix = "Bearer ";

    private readonly TerminalTokenService tokenService;

    public TerminalAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        TerminalTokenService tokenService)
        : base(options, logger, encoder)
    {
        this.tokenService = tokenService;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Bearer がない要求は他のスキーム (Cookie) に任せる
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var token = header[BearerPrefix.Length..].Trim();
        var view = token.Length == 0 ? null : await tokenService.AuthenticateAsync(token, Context.RequestAborted);
        if (view is null)
        {
            return AuthenticateResult.Fail("Invalid terminal token.");
        }

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(AuthClaims.ForTerminal(view)), Scheme.Name));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = "Bearer";
        return Task.CompletedTask;
    }
}
