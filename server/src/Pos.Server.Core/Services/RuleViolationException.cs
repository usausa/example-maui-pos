namespace Pos.Server.Services;

// DB トランザクション内で見つかった業務ルール違反 (ロールバックして呼び出し側へ返す)
public sealed class RuleViolationException : Exception
{
    public ErrorCode Code { get; }

    public RuleReason Reason { get; }

    public RuleViolationException(ErrorCode code, RuleReason reason)
        : base(reason.ToString())
    {
        Code = code;
        Reason = reason;
    }
}
