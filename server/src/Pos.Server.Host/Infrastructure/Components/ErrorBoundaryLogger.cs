namespace Pos.Server.Host.Infrastructure.Components;

using Microsoft.AspNetCore.Components.Web;

using Pos.Server.Host.Application;

public sealed class ErrorBoundaryLogger : IErrorBoundaryLogger
{
    private readonly ILogger<ErrorBoundaryLogger> log;

    public ErrorBoundaryLogger(ILogger<ErrorBoundaryLogger> log)
    {
        this.log = log;
    }

    public ValueTask LogErrorAsync(Exception exception)
    {
        log.ErrorUnhandledException(exception);
        return ValueTask.CompletedTask;
    }
}
