namespace Pos.Server.Host.Application.Authentication;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;

using Pos.Server.Services;

// Cookie の検証は HTTP の要求のときだけ行われるので、開いている回線でもアカウントの削除・無効化・変更を 1 分ごとに確かめる
public sealed class AccountAuthenticationStateProvider : RevalidatingServerAuthenticationStateProvider
{
    private readonly AccountService accountService;

    public AccountAuthenticationStateProvider(
        ILoggerFactory loggerFactory,
        AccountService accountService)
        : base(loggerFactory)
    {
        this.accountService = accountService;
    }

    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(1);

    protected override async Task<bool> ValidateAuthenticationStateAsync(AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        // ログインしていない (認証を無効にしたとき) は確かめるものがない
        var account = AuthClaims.AccountOf(authenticationState.User);
        return (account is null) || await accountService.IsSessionValidAsync(account.Id, account.Version, cancellationToken);
    }
}
