namespace Pos.Shared.Common;

using Pos.Shared.Transactions;

// RFC 9457 Problem Details + errorCode / errors / expected (api-design §2.4)
public sealed class ProblemResponse
{
    public string? Type { get; set; }

    public string? Title { get; set; }

    public int? Status { get; set; }

    public string? Detail { get; set; }

    public string? Instance { get; set; }

    public string? TraceId { get; set; }

    // api-design §5 (UPPER_SNAKE_CASE)。ErrorCode.ToCode() と対応する
    public string? ErrorCode { get; set; }

    public IReadOnlyDictionary<string, string[]>? Errors { get; set; }

    // 取引検証時のサーバ計算結果
    public TransactionCalculationResponse? Expected { get; set; }
}
