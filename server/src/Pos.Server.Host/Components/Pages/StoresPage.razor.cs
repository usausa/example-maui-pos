namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;

using Pos.Server.Host.Components.Dialogs;
using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Entity;
using Pos.Server.Services;

// S-70 店舗
public sealed partial class StoresPage
{
    private List<StoreEntity> items = [];
    private bool includeDeleted;

    [Inject]
    public required StoreService StoreService { get; set; }

    protected override Task OnInitializedAsync() => LoadAsync();

    private Task LoadAsync() =>
        LoadAsync(async () =>
        {
            items = await StoreService.QueryAllAsync(includeDeleted, CancellationToken);
        });

    private async Task AddAsync()
    {
        var form = await ShowEditDialogAsync<StoreEditDialog, StoreForm>("店舗追加", new StoreForm());
        if (form is null)
        {
            return;
        }

        await RunAsync(async () => NotifyResult(await StoreService.InsertAsync(StoreForm.ToEntity(form), CancellationToken), "追加しました。"), LoadAsync);
    }

    private async Task EditAsync(StoreEntity entity)
    {
        var form = await ShowEditDialogAsync<StoreEditDialog, StoreForm>("店舗編集", StoreForm.ToForm(entity));
        if (form is null)
        {
            return;
        }

        await RunAsync(async () => NotifyResult(await StoreService.UpdateAsync(StoreForm.ToEntity(form), CancellationToken), "更新しました。"), LoadAsync);
    }

    private async Task DeleteAsync(StoreEntity entity)
    {
        if (!await ConfirmDeleteAsync(entity.Name))
        {
            return;
        }

        await RunAsync(async () => NotifyResult(await StoreService.DeleteAsync(entity.Id, CancellationToken), "削除しました。", inUse: "端末または在庫がある店舗は削除できません。"), LoadAsync);
    }
}
