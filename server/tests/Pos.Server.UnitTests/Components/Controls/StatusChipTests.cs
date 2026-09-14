namespace Pos.Server.Components.Controls;

using MudBlazor;

using Pos.Server.Host.Application;
using Pos.Server.Host.Components.Controls;

public sealed class StatusChipTests : MudBlazorTestBase
{
    [Fact]
    public void RenderShowsTextAndColor()
    {
        // Arrange & Act
        var cut = Render<StatusChip>(parameters => parameters.Add(static x => x.Value, ViewHelper.StatusChip(TransactionStatus.Voided)));

        // Assert
        Assert.Contains("❌ 取消", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("mud-chip-color-error", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void DifferenceChipReflectsSign()
    {
        Assert.Equal(Color.Success, ViewHelper.DifferenceChip(0m).Color);
        Assert.Equal(Color.Warning, ViewHelper.DifferenceChip(100m).Color);
        Assert.Equal(Color.Error, ViewHelper.DifferenceChip(-100m).Color);
        Assert.Equal("-", ViewHelper.DifferenceChip(null).Text);
    }
}
