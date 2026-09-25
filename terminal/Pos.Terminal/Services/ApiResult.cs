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

    // 再送しても解決しない応答 (要確認)。登録が無効 (401) と試行回数の上限 (429) は再登録・時間をおけば送れる
    public bool IsRejected => (Status == ApiStatus.HttpError) && ((int)StatusCode is >= 400 and < 500) && (StatusCode is not (HttpStatusCode.Unauthorized or HttpStatusCode.TooManyRequests));

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
            ApiStatus.HttpError when StatusCode == HttpStatusCode.Unauthorized => "端末の登録が無効です。",
            ApiStatus.HttpError when StatusCode == HttpStatusCode.TooManyRequests => "試行が多すぎます。しばらく待ってからやり直してください。",
            ApiStatus.HttpError => Problem?.Title ?? $"サーバーエラー ({(int)StatusCode})",
            ApiStatus.Unavailable => "サーバーに接続できません。",
            ApiStatus.Canceled => "中断しました。",
            _ => "不明なエラーです。"
        };
}
