namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;

using Pos.Server.Host.Components.Dialogs;
using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Entity;
using Pos.Server.Services;

// 仕入先 (削除は論理削除で、登録済みの入荷は名前を引ける)
public sealed partial class SuppliersPage
{
    private List<SupplierEntity> items = [];
    private bool includeDeleted;

    [Inject]
    public required SupplierService SupplierService { get; set; }

    protected override Task OnInitializedAsync() => LoadAsync();

    private Task LoadAsync() =>
        LoadAsync(async () =>
        {
            items = await SupplierService.QueryListAsync(includeDeleted, CancellationToken);
        });

    private async Task AddAsync()
    {
        var form = await ShowEditDialogAsync<SupplierEditDialog, SupplierForm>("仕入先追加", new SupplierForm());
        if (form is null)
        {
            return;
        }

        await RunAsync(async () => NotifyResult(await SupplierService.InsertAsync(SupplierForm.ToEntity(form), CancellationToken), "追加しました。"), LoadAsync);
    }

    private async Task EditAsync(SupplierEntity entity)
    {
        var form = await ShowEditDialogAsync<SupplierEditDialog, SupplierForm>("仕入先編集", SupplierForm.ToForm(entity));
        if (form is null)
        {
            return;
        }

        await RunAsync(async () => NotifyResult(await SupplierService.UpdateAsync(SupplierForm.ToEntity(form), CancellationToken), "更新しました。"), LoadAsync);
    }

    private async Task DeleteAsync(SupplierEntity entity)
    {
        if (!await ConfirmDeleteAsync(entity.Name))
        {
            return;
        }

        await RunAsync(async () => NotifyResult(await SupplierService.DeleteAsync(entity.Id, CancellationToken), "削除しました。"), LoadAsync);
    }
}
