namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using Pos.Server.Accessors;
using Pos.Server.Host.Components.Dialogs;
using Pos.Server.Host.Infrastructure.Api;
using Pos.Server.Host.Infrastructure.Components;
using Pos.Server.Host.Mappers;
using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Entity;

// S-71 レジ端末
public sealed partial class TerminalsPage
{
    private List<TerminalEntity> items = [];

    private Dictionary<Guid, string> stores = [];

    private bool includeDeleted;

    [Inject]
    public required TerminalAccessor TerminalAccessor { get; set; }

    [Inject]
    public required StoreAccessor StoreAccessor { get; set; }

    protected override Task OnInitializedAsync() => LoadAsync();

    private Task LoadAsync() =>
        LoadAsync(async () =>
        {
            stores = (await StoreAccessor.QueryListAsync(null, true, "Code", ApiHelper.MaxPageSize, 0, CancellationToken)).ToDictionary(static x => x.Id, static x => x.Name);
            items = await TerminalAccessor.QueryListAsync(null, null, includeDeleted, "StoreId, TerminalNo", ApiHelper.MaxPageSize, 0, CancellationToken);
        });

    private string StoreName(Guid storeId) => stores.GetValueOrDefault(storeId, "-");

    private async Task AddAsync()
    {
        var form = await ShowEditDialogAsync<TerminalEditDialog, TerminalForm>("端末追加", new TerminalForm());
        if (form is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var now = UtcNow;
            var entity = FormMapper.ToTerminalEntity(form);
            entity.Id = Guid.CreateVersion7();
            entity.CreatedAt = now;
            entity.UpdatedAt = now;
            entity.Version = 1;
            await TerminalAccessor.InsertAsync(entity, CancellationToken);
            Snackbar.AddSuccess("追加しました。");
        }, LoadAsync);
    }

    private async Task EditAsync(TerminalEntity entity)
    {
        var form = await ShowEditDialogAsync<TerminalEditDialog, TerminalForm>("端末編集", FormMapper.ToTerminalForm(entity));
        if (form is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var rows = await TerminalAccessor.UpdateAsync(form.Id, form.StoreId!.Value, form.TerminalNo, form.Name, form.IsActive, UtcNow, form.Version, CancellationToken);
            if (rows > 0)
            {
                Snackbar.AddSuccess("更新しました。");
            }
            else
            {
                NotifyVersionMismatch();
            }
        }, LoadAsync);
    }

    // 開設中のシフトがある端末は削除できない (api-design §3.3)
    private async Task DeleteAsync(TerminalEntity entity)
    {
        if (!await ConfirmDeleteAsync(entity.Name))
        {
            return;
        }

        await RunAsync(async () =>
        {
            if (await TerminalAccessor.CountOpenShiftAsync(entity.Id, CancellationToken) > 0)
            {
                Snackbar.AddWarning("開設中のシフトがある端末は削除できません。");
                return;
            }

            if (await TerminalAccessor.DeleteAsync(entity.Id, UtcNow, CancellationToken) > 0)
            {
                Snackbar.AddSuccess("削除しました。");
            }
            else
            {
                NotifyNotFound();
            }
        }, LoadAsync);
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
