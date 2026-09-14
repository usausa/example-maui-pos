namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;

using Pos.Server.Host.Components.Dialogs;
using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Entity;
using Pos.Server.Services;

// S-55 値引
public sealed partial class DiscountsPage
{
    private List<DiscountEntity> items = [];
    private bool includeDeleted;

    [Inject]
    public required DiscountService DiscountService { get; set; }

    protected override Task OnInitializedAsync() => LoadAsync();

    private Task LoadAsync() =>
        LoadAsync(async () =>
        {
            items = await DiscountService.QueryListAsync(null, includeDeleted, CancellationToken);
        });

    private async Task AddAsync()
    {
        var form = await ShowEditDialogAsync<DiscountEditDialog, DiscountForm>("値引追加", new DiscountForm());
        if (form is null)
        {
            return;
        }

        await RunAsync(async () => NotifyResult(await DiscountService.InsertAsync(DiscountForm.ToEntity(form), CancellationToken), "追加しました。"), LoadAsync);
    }

    private async Task EditAsync(DiscountEntity entity)
    {
        var form = await ShowEditDialogAsync<DiscountEditDialog, DiscountForm>("値引編集", DiscountForm.ToForm(entity));
        if (form is null)
        {
            return;
        }

        await RunAsync(async () => NotifyResult(await DiscountService.UpdateAsync(DiscountForm.ToEntity(form), CancellationToken), "更新しました。"), LoadAsync);
    }

    private async Task DeleteAsync(DiscountEntity entity)
    {
        if (!await ConfirmDeleteAsync(entity.Name))
        {
            return;
        }

        await RunAsync(async () => NotifyResult(await DiscountService.DeleteAsync(entity.Id, CancellationToken), "削除しました。"), LoadAsync);
    }
}
