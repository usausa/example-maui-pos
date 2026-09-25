namespace Pos.Server.Host.Application.Authentication;

using Microsoft.AspNetCore.Authentication.Cookies;

// 認可のポリシー。認証を無効にしたときはすべて素通しになる
public static class Policies
{
    // API の既定: 管理画面のログイン (Cookie) か端末のトークン
    public const string Api = nameof(Api);

    // 管理画面のログインだけ (管理画面だけが使う API)
    public const string Admin = nameof(Admin);

    // 管理画面の管理者 (マスタ・会社設定・ユーザー・端末登録の変更)
    public const string Administrator = nameof(Administrator);

    // 端末のトークンだけ (terminals/me)
    public const string Terminal = nameof(Terminal);
}

public static class AuthSchemes
{
    public const string Cookie = CookieAuthenticationDefaults.AuthenticationScheme;

    public const string Terminal = nameof(Terminal);
}

public static class RateLimits
{
    // ログインとペアリング (総当たりを防ぐ)
    public const string Auth = nameof(Auth);
}
