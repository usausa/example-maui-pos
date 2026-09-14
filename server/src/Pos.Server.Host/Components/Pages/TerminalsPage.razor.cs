namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using Pos.Server.Host.Components.Dialogs;
using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Entity;
using Pos.Server.Services;

// S-71 レジ端末
public sealed partial class TerminalsPage
{
    private List<TerminalEntity> items = [];
    private Dictionary<Guid, string> stores = [];
    private bool includeDeleted;

    [Inject]
    public required TerminalService TerminalService { get; set; }

    [Inject]
    public required StoreService StoreService { get; set; }

    protected override Task OnInitializedAsync() => LoadAsync();

    private string StoreName(Guid storeId) => stores.GetValueOrDefault(storeId, "-");

    private Task LoadAsync() =>
        LoadAsync(async () =>
        {
            stores = (await StoreService.QueryAllAsync(true, CancellationToken)).ToDictionary(static x => x.Id, static x => x.Name);
            items = await TerminalService.QueryAllAsync(includeDeleted, CancellationToken);
        });

    private async Task AddAsync()
    {
        var form = await ShowEditDialogAsync<TerminalEditDialog, TerminalForm>("端末追加", new TerminalForm());
        if (form is null)
        {
            return;
        }

        await RunAsync(async () => NotifyResult(await TerminalService.InsertAsync(TerminalForm.ToEntity(form), CancellationToken), "追加しました。", duplicate: "端末番号が重複しています。"), LoadAsync);
    }

    private async Task EditAsync(TerminalEntity entity)
    {
        var form = await ShowEditDialogAsync<TerminalEditDialog, TerminalForm>("端末編集", TerminalForm.ToForm(entity));
        if (form is null)
        {
            return;
        }

        await RunAsync(async () => NotifyResult(await TerminalService.UpdateAsync(TerminalForm.ToEntity(form), CancellationToken), "更新しました。", duplicate: "端末番号が重複しています。"), LoadAsync);
    }

    private async Task DeleteAsync(TerminalEntity entity)
    {
        if (!await ConfirmDeleteAsync(entity.Name))
        {
            return;
        }

        await RunAsync(async () => NotifyResult(await TerminalService.DeleteAsync(entity.Id, CancellationToken), "削除しました。", inUse: "開設中のシフトがある端末は削除できません。"), LoadAsync);
    }

    private Task<IDialogReference> ShowQrAsync(TerminalEntity entity) =>
        DialogService.ShowAsync<TerminalQrDialog>(
            string.Empty,
            new DialogParameters
            {
                { nameof(TerminalQrDialog.Terminal), entity },
                { nameof(TerminalQrDialog.StoreName), StoreName(entity.StoreId) }
            },
            Styles.SmallDialog);
}
