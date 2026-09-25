namespace Pos.Server.Host.Application.Authentication;

// 端末のトークンで認証された要求が、本文・クエリの店舗・端末と一致するか。
// 管理画面のログイン (Cookie) の要求と、認証を無効にしたときは確かめない
public sealed class TerminalAccess
{
    private readonly AuthSetting setting;

    public TerminalAccess(AuthSetting setting)
    {
        this.setting = setting;
    }

    // terminalId が null なら店舗だけを確かめる
    public bool CanAccess(ClaimsPrincipal user, Guid storeId, Guid? terminalId = null)
    {
        var terminal = RequestTerminal(user);
        return (terminal is null) || ((terminal.StoreId == storeId) && ((terminalId is null) || (terminal.TerminalId == terminalId)));
    }

    public bool CanAccessTerminal(ClaimsPrincipal user, Guid terminalId)
    {
        var terminal = RequestTerminal(user);
        return (terminal is null) || (terminal.TerminalId == terminalId);
    }

    // 確かめる対象の端末 (null = 確かめない)
    private TerminalIdentity? RequestTerminal(ClaimsPrincipal user) =>
        setting.Enabled ? AuthClaims.TerminalOf(user) : null;
}
