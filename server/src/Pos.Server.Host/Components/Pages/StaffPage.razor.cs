namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;

using Pos.Server.Accessors;
using Pos.Server.Host.Components.Dialogs;
using Pos.Server.Host.Infrastructure.Api;
using Pos.Server.Host.Infrastructure.Components;
using Pos.Server.Host.Mappers;
using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Entity;

// S-72 スタッフ
public sealed partial class StaffPage
{
    private List<StaffEntity> items = [];

    private Dictionary<Guid, string> stores = [];

    private bool includeDeleted;

    [Inject]
    public required StaffAccessor StaffAccessor { get; set; }

    [Inject]
    public required StoreAccessor StoreAccessor { get; set; }

    protected override Task OnInitializedAsync() => LoadAsync();

    private Task LoadAsync() =>
        LoadAsync(async () =>
        {
            stores = (await StoreAccessor.QueryListAsync(null, true, "Code", ApiHelper.MaxPageSize, 0, CancellationToken)).ToDictionary(static x => x.Id, static x => x.Name);
            items = await StaffAccessor.QueryListAsync(null, null, includeDeleted, "Code", ApiHelper.MaxPageSize, 0, CancellationToken);
        });

    private async Task AddAsync()
    {
        var form = await ShowEditDialogAsync<StaffEditDialog, StaffForm>("スタッフ追加", new StaffForm());
        if (form is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var now = UtcNow;
            var entity = FormMapper.ToStaffEntity(form);
            entity.Id = Guid.CreateVersion7();
            entity.CreatedAt = now;
            entity.UpdatedAt = now;
            entity.Version = 1;
            await StaffAccessor.InsertAsync(entity, CancellationToken);
            Snackbar.AddSuccess("追加しました。");
        }, LoadAsync);
    }

    private async Task EditAsync(StaffEntity entity)
    {
        var form = await ShowEditDialogAsync<StaffEditDialog, StaffForm>("スタッフ編集", FormMapper.ToStaffForm(entity));
        if (form is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var rows = await StaffAccessor.UpdateAsync(form.Id, form.Code, form.Name, form.Role, form.StoreId, form.IsActive, UtcNow, form.Version, CancellationToken);
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

    private async Task DeleteAsync(StaffEntity entity)
    {
        if (!await ConfirmDeleteAsync(entity.Name))
        {
            return;
        }

        await RunAsync(async () =>
        {
            if (await StaffAccessor.DeleteAsync(entity.Id, UtcNow, CancellationToken) > 0)
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
