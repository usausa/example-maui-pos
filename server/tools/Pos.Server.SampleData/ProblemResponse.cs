namespace Pos.Server.SampleData;

using Pos.Contract.Transactions;

// RFC 9457 Problem Details + errorCode / errors / expected (JSON の読み取り先)
#pragma warning disable CA1812
internal sealed class ProblemResponse
{
    public string? Type { get; set; }

    public string? Title { get; set; }

    public int? Status { get; set; }

    public string? Detail { get; set; }

    public string? Instance { get; set; }

    public string? TraceId { get; set; }

    // UPPER_SNAKE_CASE (ErrorCode.ToCode() と対応する)
    public string? ErrorCode { get; set; }

    public IReadOnlyDictionary<string, string[]>? Errors { get; set; }

    // 取引検証時のサーバ計算結果
    public TransactionCalculationResponse? Expected { get; set; }
}
#pragma warning restore CA1812
