namespace Pos.Server.SampleData;

// 4xx / 5xx の応答 (Problem Details の errorCode 付き)
internal sealed class ApiException : Exception
{
    public HttpStatusCode StatusCode { get; }

    public string? ErrorCode { get; }

    public ApiException()
    {
    }

    public ApiException(string message)
        : base(message)
    {
    }

    public ApiException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public ApiException(HttpStatusCode statusCode, string? errorCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }
}
