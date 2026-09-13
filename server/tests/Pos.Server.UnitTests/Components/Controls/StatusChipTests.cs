namespace Pos.Server.Components.Controls;

using MudBlazor;

using Pos.Domain;
using Pos.Server.Host.Application;
using Pos.Server.Host.Components.Controls;

public sealed class StatusChipTests : MudBlazorTestBase
{
    [Fact]
    public void RenderShowsTextAndColor()
    {
        // Arrange & Act
        var cut = Render<StatusChip>(parameters => parameters.Add(static x => x.Value, ChipText.Status(TransactionStatus.Voided)));

        // Assert
        Assert.Contains("❌ 取消", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("mud-chip-color-error", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void DifferenceChipReflectsSign()
    {
        Assert.Equal(Color.Success, ChipText.Difference(0m).Color);
        Assert.Equal(Color.Warning, ChipText.Difference(100m).Color);
        Assert.Equal(Color.Error, ChipText.Difference(-100m).Color);
        Assert.Equal("-", ChipText.Difference(null).Text);
    }
}
