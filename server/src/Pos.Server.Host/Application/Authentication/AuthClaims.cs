namespace Pos.Server.Host.Application.Authentication;

using Pos.Server.Models.Entity;
using Pos.Server.Models.Views;

// 管理画面のログイン (Cookie) の利用者。役割はポリシーがクレームで確かめる
public sealed record AccountIdentity(Guid Id, string Name, int Version);

// 端末のトークンで認証された端末
public sealed record TerminalIdentity(Guid TerminalId, Guid StoreId);

// クレームの組み立てと読み取り。認証を無効にしても、ログインやトークンがあればクレームは付く
public static class AuthClaims
{
    private const string AccountVersion = "account_version";
    private const string TerminalId = "terminal_id";
    private const string StoreId = "store_id";

    public static ClaimsIdentity ForAccount(AccountEntity account) =>
        new(
            [
                new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()),
                new Claim(ClaimTypes.Name, account.Name),
                new Claim(ClaimTypes.Role, account.Role.ToString()),
                new Claim(AccountVersion, account.Version.ToString(CultureInfo.InvariantCulture))
            ],
            AuthSchemes.Cookie);

    public static ClaimsIdentity ForTerminal(TerminalTokenView token) =>
        new(
            [
                new Claim(ClaimTypes.Name, token.TerminalName),
                new Claim(TerminalId, token.TerminalId.ToString()),
                new Claim(StoreId, token.StoreId.ToString())
            ],
            AuthSchemes.Terminal);

    public static AccountIdentity? AccountOf(ClaimsPrincipal user)
    {
        var identity = FindIdentity(user, AuthSchemes.Cookie);
        return (identity is not null) &&
               Guid.TryParse(identity.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) &&
               Int32.TryParse(identity.FindFirst(AccountVersion)?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var version)
            ? new AccountIdentity(id, identity.Name ?? string.Empty, version)
            : null;
    }

    public static TerminalIdentity? TerminalOf(ClaimsPrincipal user)
    {
        var identity = FindIdentity(user, AuthSchemes.Terminal);
        return (identity is not null) &&
               Guid.TryParse(identity.FindFirst(TerminalId)?.Value, out var terminalId) &&
               Guid.TryParse(identity.FindFirst(StoreId)?.Value, out var storeId)
            ? new TerminalIdentity(terminalId, storeId)
            : null;
    }

    // 複数のスキームで認証した要求は、スキームごとの ID を持つ
    private static ClaimsIdentity? FindIdentity(ClaimsPrincipal user, string scheme) =>
        user.Identities.FirstOrDefault(x => x.IsAuthenticated && (x.AuthenticationType == scheme));
}
