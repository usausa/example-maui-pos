namespace Pos.Server.Host.Endpoints;

using Microsoft.AspNetCore.Authentication;

using Pos.Server.Services;

// 管理画面のログイン・ログアウト (フォームの送信。API ではない)
public static class AuthEndpoints
{
    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/auth");
        group.MapPost("/login", HandleLoginAsync).AllowAnonymous().RequireRateLimiting(RateLimits.Auth);
        group.MapPost("/logout", HandleLogoutAsync).RequireAuthorization();
    }

    //--------------------------------------------------------------------------------
    // Handler
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleLoginAsync(
        HttpContext context,
        AccountService accountService,
        [FromForm] string? name,
        [FromForm] string? password,
        [FromForm] string? returnUrl,
        CancellationToken cancellationToken)
    {
        // 長すぎる入力はハッシュ計算の前に落とす
        var valid = !String.IsNullOrEmpty(name) && (name.Length <= Length.AccountName) &&
                    !String.IsNullOrEmpty(password) && (password.Length <= Length.Password);
        var account = valid ? await accountService.AuthenticateAsync(name!, password!, cancellationToken) : null;
        if (account is null)
        {
            return TypedResults.LocalRedirect($"~/login?error=1&returnUrl={Uri.EscapeDataString(returnUrl ?? string.Empty)}");
        }

        await context.SignInAsync(AuthSchemes.Cookie, new ClaimsPrincipal(AuthClaims.ForAccount(account)));

        // 戻り先はサイト内の相対パスだけ (//host のようなプロトコル相対の転送を防ぐ)
        var target = returnUrl?.TrimStart('/', '\\');
        return TypedResults.LocalRedirect(String.IsNullOrEmpty(target) ? "~/" : "~/" + target);
    }

    private static async ValueTask<IResult> HandleLogoutAsync(HttpContext context)
    {
        await context.SignOutAsync(AuthSchemes.Cookie);
        return TypedResults.LocalRedirect("~/login");
    }
}
