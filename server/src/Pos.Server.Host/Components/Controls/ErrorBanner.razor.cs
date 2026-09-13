namespace Pos.Server.Host.Components.Controls;

using Microsoft.AspNetCore.Components;

public sealed partial class ErrorBanner
{
    [Parameter]
    public string? Message { get; set; }

    [Parameter]
    public EventCallback OnClear { get; set; }
}
