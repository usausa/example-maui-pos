namespace Pos.Server.Host.Infrastructure.ExceptionHandling;

using Microsoft.AspNetCore.Diagnostics;

using Pos.Server.Host.Application;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService problemDetailsService;

    private readonly ILogger<GlobalExceptionHandler> logger;

    public GlobalExceptionHandler(
        IProblemDetailsService problemDetailsService,
        ILogger<GlobalExceptionHandler> logger)
    {
        this.problemDetailsService = problemDetailsService;
        this.logger = logger;
    }

    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // 画面は ExceptionHandlerMiddleware の再実行 (/error) に任せる (ログもミドルウェアが出す)
        if (!httpContext.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            return ValueTask.FromResult(false);
        }

        // 要求の読み取りの失敗 (壊れた JSON など。ThrowOnBadRequest で例外になる) は、その状態コード (400) で返し、未処理の例外として記録しない
        if (exception is BadHttpRequestException badRequest)
        {
            return WriteProblemAsync(httpContext, exception, badRequest.StatusCode, null);
        }

        logger.ErrorUnhandledException(exception);

        return WriteProblemAsync(httpContext, exception, StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
    }

    private ValueTask<bool> WriteProblemAsync(HttpContext httpContext, Exception exception, int status, string? title)
    {
        httpContext.Response.StatusCode = status;

        return problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title
            }
        });
    }
}
