namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;

using Pos.Server.Accessors;
using Pos.Server.Host.Components.Dialogs;
using Pos.Server.Host.Infrastructure.Components;
using Pos.Server.Host.Mappers;
using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Entity;

// S-44 調整理由
public sealed partial class AdjustmentReasonsPage
{
    private List<AdjustmentReasonEntity> items = [];

    private bool includeDeleted;

    [Inject]
    public required AdjustmentReasonAccessor AdjustmentReasonAccessor { get; set; }

    protected override Task OnInitializedAsync() => LoadAsync();

    private Task LoadAsync() =>
        LoadAsync(async () =>
        {
            items = await AdjustmentReasonAccessor.QueryListAsync(null, includeDeleted, CancellationToken);
        });

    private async Task AddAsync()
    {
        var form = await ShowEditDialogAsync<AdjustmentReasonEditDialog, AdjustmentReasonForm>("調整理由追加", new AdjustmentReasonForm(), Styles.SmallDialog);
        if (form is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var now = UtcNow;
            var entity = FormMapper.ToAdjustmentReasonEntity(form);
            entity.Id = Guid.CreateVersion7();
            entity.CreatedAt = now;
            entity.UpdatedAt = now;
            entity.Version = 1;
            await AdjustmentReasonAccessor.InsertAsync(entity, CancellationToken);
            Snackbar.AddSuccess("追加しました。");
        }, LoadAsync);
    }

    private async Task EditAsync(AdjustmentReasonEntity entity)
    {
        var form = await ShowEditDialogAsync<AdjustmentReasonEditDialog, AdjustmentReasonForm>("調整理由編集", FormMapper.ToAdjustmentReasonForm(entity), Styles.SmallDialog);
        if (form is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var rows = await AdjustmentReasonAccessor.UpdateAsync(form.Id, form.Code, form.Name, form.SortOrder, form.IsActive, UtcNow, form.Version, CancellationToken);
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

    private async Task DeleteAsync(AdjustmentReasonEntity entity)
    {
        if (!await ConfirmDeleteAsync(entity.Name))
        {
            return;
        }

        await RunAsync(async () =>
        {
            if (await AdjustmentReasonAccessor.DeleteAsync(entity.Id, UtcNow, CancellationToken) > 0)
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
