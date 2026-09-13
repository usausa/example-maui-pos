namespace Pos.Server.Host.Components.Controls;

using Microsoft.AspNetCore.Components;

public sealed partial class ProgressOverlay
{
    [Parameter]
    public bool Visible { get; set; }

    [Parameter]
    public double Ratio { get; set; }

    [Parameter]
    public string? Message { get; set; }
}
