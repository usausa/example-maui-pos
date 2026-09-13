namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;

using Pos.Server.Accessors;
using Pos.Server.Host.Components.Dialogs;
using Pos.Server.Host.Infrastructure.Api;
using Pos.Server.Host.Infrastructure.Components;
using Pos.Server.Host.Mappers;
using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Entity;

// S-70 店舗
public sealed partial class StoresPage
{
    private List<StoreEntity> items = [];

    private bool includeDeleted;

    [Inject]
    public required StoreAccessor StoreAccessor { get; set; }

    [Inject]
    public required TerminalAccessor TerminalAccessor { get; set; }

    [Inject]
    public required InventoryAccessor InventoryAccessor { get; set; }

    protected override Task OnInitializedAsync() => LoadAsync();

    private Task LoadAsync() =>
        LoadAsync(async () =>
        {
            items = await StoreAccessor.QueryListAsync(null, includeDeleted, "Code", ApiHelper.MaxPageSize, 0, CancellationToken);
        });

    private async Task AddAsync()
    {
        var form = await ShowEditDialogAsync<StoreEditDialog, StoreForm>("店舗追加", new StoreForm());
        if (form is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var now = UtcNow;
            var entity = FormMapper.ToStoreEntity(form);
            entity.Id = Guid.CreateVersion7();
            entity.CreatedAt = now;
            entity.UpdatedAt = now;
            entity.Version = 1;
            await StoreAccessor.InsertAsync(entity, CancellationToken);
            Snackbar.AddSuccess("追加しました。");
        }, LoadAsync);
    }

    private async Task EditAsync(StoreEntity entity)
    {
        var form = await ShowEditDialogAsync<StoreEditDialog, StoreForm>("店舗編集", FormMapper.ToStoreForm(entity));
        if (form is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var rows = await StoreAccessor.UpdateAsync(form.Id, form.Code, form.Name, form.PostalCode, form.Address, form.Phone, form.RegistrationNo, form.ReceiptHeader, form.ReceiptFooter, form.TimeZone, form.IsActive, UtcNow, form.Version, CancellationToken);
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

    // 端末・在庫がある店舗は削除できない (api-design §3.2)
    private async Task DeleteAsync(StoreEntity entity)
    {
        if (!await ConfirmDeleteAsync(entity.Name))
        {
            return;
        }

        await RunAsync(async () =>
        {
            if ((await TerminalAccessor.CountAsync(entity.Id, null, false, CancellationToken) > 0) ||
                (await InventoryAccessor.CountLevelsAsync(entity.Id, null, null, false, null, CancellationToken) > 0))
            {
                Snackbar.AddWarning("端末または在庫がある店舗は削除できません。");
                return;
            }

            if (await StoreAccessor.DeleteAsync(entity.Id, UtcNow, CancellationToken) > 0)
            {
                Snackbar.AddSuccess("削除しました。");
            }
            else
            {
                NotifyNotFound();
            }
        }, LoadAsync);
    }
}
