namespace Pos.Server.Components;

using Bunit;

using Microsoft.AspNetCore.Components.Rendering;

using MudBlazor;

using Pos.Server.Host.Components;
using Pos.Server.Models;

public sealed class PageComponentBaseTests : MudBlazorTestBase
{
    // 表の読み込みの失敗はバナーに出して空の表にし、通ったら表が出した失敗だけを消す (ほかの読み込みの失敗は残す)
    [Fact]
    public async Task LoadGridShowsFailureAndClearsOnlyItsOwn()
    {
        // Arrange
        var cut = Render<GridPage>();

        // Act / Assert: 失敗はバナーに出し (ページを描き直す)、空の表を返す
        var failed = await cut.InvokeAsync(() => cut.Instance.LoadGrid(static () => throw new InvalidOperationException("表の失敗")));
        Assert.Equal(0, failed.TotalItems);
        Assert.Equal("表の失敗", cut.Markup);

        // Act / Assert: 通れば表の失敗を消す
        var loaded = await cut.InvokeAsync(() => cut.Instance.LoadGrid(static () => ValueTask.FromResult(new PagedResult<int>(2, 0, 10, [1, 2]))));
        Assert.Equal(2, loaded.TotalItems);
        Assert.Empty(cut.Markup);

        // Act / Assert: ほかの読み込みの失敗は、表が通っても残す
        await cut.InvokeAsync(() => cut.Instance.Load(static () => throw new InvalidOperationException("読み込みの失敗")));
        await cut.InvokeAsync(() => cut.Instance.LoadGrid(static () => ValueTask.FromResult(new PagedResult<int>(0, 0, 10, []))));
        Assert.Equal("読み込みの失敗", cut.Instance.Error);
    }

    // 基底の読み込みを呼び、バナーの文言だけを描くページ
    public sealed class GridPage : PageComponentBase
    {
        public string? Error => ErrorMessage;

        public Task<GridData<int>> LoadGrid(Func<ValueTask<PagedResult<int>>> load) => LoadGridAsync(load);

        public Task Load(Func<Task> load) => LoadAsync(load);

        protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, ErrorMessage);
    }
}
