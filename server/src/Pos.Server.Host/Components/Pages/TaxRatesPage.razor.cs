namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;

using Pos.Server.Host.Components.Dialogs;
using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Entity;
using Pos.Server.Services;

// S-54 税率 (既定は 1 件だけ)
public sealed partial class TaxRatesPage
{
    private List<TaxRateEntity> items = [];
    private bool includeDeleted;

    [Inject]
    public required TaxRateService TaxRateService { get; set; }

    protected override Task OnInitializedAsync() => LoadAsync();

    private Task LoadAsync() =>
        LoadAsync(async () =>
        {
            items = await TaxRateService.QueryListAsync(null, includeDeleted, CancellationToken);
        });

    private async Task AddAsync()
    {
        var form = await ShowEditDialogAsync<TaxRateEditDialog, TaxRateForm>("税率追加", new TaxRateForm());
        if (form is null)
        {
            return;
        }

        await RunAsync(async () => NotifyResult(await TaxRateService.InsertAsync(TaxRateForm.ToEntity(form), CancellationToken), "追加しました。"), LoadAsync);
    }

    private async Task EditAsync(TaxRateEntity entity)
    {
        var form = await ShowEditDialogAsync<TaxRateEditDialog, TaxRateForm>("税率編集", TaxRateForm.ToForm(entity));
        if (form is null)
        {
            return;
        }

        await RunAsync(async () => NotifyResult(await TaxRateService.UpdateAsync(TaxRateForm.ToEntity(form), CancellationToken), "更新しました。"), LoadAsync);
    }

    private async Task DeleteAsync(TaxRateEntity entity)
    {
        if (!await ConfirmDeleteAsync(entity.Name))
        {
            return;
        }

        await RunAsync(async () => NotifyResult(await TaxRateService.DeleteAsync(entity.Id, CancellationToken), "削除しました。", inUse: "使用中の商品がある税率は削除できません。"), LoadAsync);
    }
}
