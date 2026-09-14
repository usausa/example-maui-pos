namespace Pos.Terminal.Services;

public enum ApiStatus
{
    // 2xx
    Success,
    // 4xx / 5xx (Problem Details が読めれば Problem に入る)
    HttpError,
    // 通信不可・タイムアウト
    Unavailable,
    Canceled
}

// API 呼び出しの結果。409 / 422 の errorCode を読むため Problem Details を保持する
public sealed class ApiResult<T>
{
    public ApiStatus Status { get; }

    public HttpStatusCode StatusCode { get; }

    public T? Content { get; }

    public ProblemResponse? Problem { get; }

    public Exception? Exception { get; }

    public bool IsSuccess => Status == ApiStatus.Success;

    public bool IsNotFound => (Status == ApiStatus.HttpError) && (StatusCode == HttpStatusCode.NotFound);

    // 再送しても解決しない応答 (要確認)
    public bool IsRejected => (Status == ApiStatus.HttpError) && ((int)StatusCode is >= 400 and < 500);

    public string? ErrorCode => Problem?.ErrorCode;

    public ApiResult(ApiStatus status, HttpStatusCode statusCode, T? content, ProblemResponse? problem, Exception? exception)
    {
        Status = status;
        StatusCode = statusCode;
        Content = content;
        Problem = problem;
        Exception = exception;
    }

    // 利用者向けの失敗理由
    public string Message =>
        Status switch
        {
            ApiStatus.Success => string.Empty,
            ApiStatus.HttpError => Problem?.Title ?? $"サーバーエラー ({(int)StatusCode})",
            ApiStatus.Unavailable => "サーバーに接続できません。",
            ApiStatus.Canceled => "中断しました。",
            _ => "不明なエラーです。"
        };
}
