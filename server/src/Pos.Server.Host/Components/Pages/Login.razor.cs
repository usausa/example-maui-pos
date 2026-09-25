namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;

// ログイン (静的 SSR のフォームを /auth/login に送る。失敗すると error を付けて戻る)
public sealed partial class Login
{
    [SupplyParameterFromQuery(Name = "error")]
    public string? Error { get; set; }

    [SupplyParameterFromQuery(Name = "returnUrl")]
    public string? ReturnPath { get; set; }

    private string? ErrorText => Error switch
    {
        null or "" => null,
        "limit" => "ログインの試行が多すぎます。しばらく待ってからやり直してください。",
        _ => "ID またはパスワードが違います。"
    };
}
