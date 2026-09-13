namespace Pos.Server.Host.Infrastructure.Api;

using Pos.Domain.Rules;

// DB トランザクション内で見つかった業務ルール違反 (ロールバックして 422 にする)
public sealed class RuleViolationException : Exception
{
    public ErrorCode Code { get; }

    public RuleViolationException(ErrorCode code, string message)
        : base(message)
    {
        Code = code;
    }
}
