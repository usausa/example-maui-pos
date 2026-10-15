namespace Pos.Server.Host.Components;

using Microsoft.AspNetCore.Components;

// 未ログインの要求をログインへ移す (ログイン後に元の画面へ戻す)
public sealed class RedirectToLogin : ComponentBase
{
    [Inject]
    public required NavigationManager Navigation { get; set; }

    protected override void OnInitialized()
    {
        var returnUrl = Navigation.ToBaseRelativePath(Navigation.Uri);
        Navigation.NavigateTo($"login?returnUrl={Uri.EscapeDataString(returnUrl)}", forceLoad: true);
    }
}
