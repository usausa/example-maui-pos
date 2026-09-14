namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;

using Pos.Server.Host.Components.Dialogs;
using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Entity;
using Pos.Server.Services;

// S-56 支払方法 (Kind = Points かつ有効な行はちょうど 1 件)
public sealed partial class PaymentMethodsPage
{
    private List<PaymentMethodEntity> items = [];
    private bool includeDeleted;

    [Inject]
    public required PaymentMethodService PaymentMethodService { get; set; }

    protected override Task OnInitializedAsync() => LoadAsync();

    private Task LoadAsync() =>
        LoadAsync(async () =>
        {
            items = await PaymentMethodService.QueryListAsync(null, includeDeleted, CancellationToken);
        });

    private async Task AddAsync()
    {
        var form = await ShowEditDialogAsync<PaymentMethodEditDialog, PaymentMethodForm>("支払方法追加", new PaymentMethodForm());
        if (form is null)
        {
            return;
        }

        await RunAsync(async () => NotifyResult(await PaymentMethodService.InsertAsync(PaymentMethodForm.ToEntity(form), CancellationToken), "追加しました。", invalid: "ポイントの支払方法は 1 件だけ有効にできます。"), LoadAsync);
    }

    private async Task EditAsync(PaymentMethodEntity entity)
    {
        var form = await ShowEditDialogAsync<PaymentMethodEditDialog, PaymentMethodForm>("支払方法編集", PaymentMethodForm.ToForm(entity));
        if (form is null)
        {
            return;
        }

        await RunAsync(async () => NotifyResult(await PaymentMethodService.UpdateAsync(PaymentMethodForm.ToEntity(form), CancellationToken), "更新しました。", invalid: "ポイントの支払方法は 1 件だけ有効にできます。"), LoadAsync);
    }

    private async Task DeleteAsync(PaymentMethodEntity entity)
    {
        if (!await ConfirmDeleteAsync(entity.Name))
        {
            return;
        }

        await RunAsync(async () => NotifyResult(await PaymentMethodService.DeleteAsync(entity.Id, CancellationToken), "削除しました。"), LoadAsync);
    }
}
