namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;

using Pos.Server.Accessors;
using Pos.Server.Host.Components.Dialogs;
using Pos.Server.Host.Infrastructure.Components;
using Pos.Server.Host.Mappers;
using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Entity;

// S-55 値引
public sealed partial class DiscountsPage
{
    private List<DiscountEntity> items = [];

    private bool includeDeleted;

    [Inject]
    public required DiscountAccessor DiscountAccessor { get; set; }

    protected override Task OnInitializedAsync() => LoadAsync();

    private Task LoadAsync() =>
        LoadAsync(async () =>
        {
            items = await DiscountAccessor.QueryListAsync(null, includeDeleted, CancellationToken);
        });

    private async Task AddAsync()
    {
        var form = await ShowEditDialogAsync<DiscountEditDialog, DiscountForm>("値引追加", new DiscountForm());
        if (form is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var now = UtcNow;
            var entity = FormMapper.ToDiscountEntity(form);
            entity.Id = Guid.CreateVersion7();
            entity.CreatedAt = now;
            entity.UpdatedAt = now;
            entity.Version = 1;
            await DiscountAccessor.InsertAsync(entity, CancellationToken);
            Snackbar.AddSuccess("追加しました。");
        }, LoadAsync);
    }

    private async Task EditAsync(DiscountEntity entity)
    {
        var form = await ShowEditDialogAsync<DiscountEditDialog, DiscountForm>("値引編集", FormMapper.ToDiscountForm(entity));
        if (form is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var rows = await DiscountAccessor.UpdateAsync(form.Id, form.Code, form.Name, form.Type, form.Value, form.Scope, form.RequiresApproval, form.IsActive, form.SortOrder, UtcNow, form.Version, CancellationToken);
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

    private async Task DeleteAsync(DiscountEntity entity)
    {
        if (!await ConfirmDeleteAsync(entity.Name))
        {
            return;
        }

        await RunAsync(async () =>
        {
            if (await DiscountAccessor.DeleteAsync(entity.Id, UtcNow, CancellationToken) > 0)
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
