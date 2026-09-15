namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;

using Pos.Server.Host.Components.Dialogs;
using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Entity;
using Pos.Server.Services;

// 調整理由
public sealed partial class AdjustmentReasonsPage
{
    private List<AdjustmentReasonEntity> items = [];
    private bool includeDeleted;

    [Inject]
    public required AdjustmentReasonService AdjustmentReasonService { get; set; }

    protected override Task OnInitializedAsync() => LoadAsync();

    private Task LoadAsync() =>
        LoadAsync(async () =>
        {
            items = await AdjustmentReasonService.QueryListAsync(null, includeDeleted, CancellationToken);
        });

    private async Task AddAsync()
    {
        var form = await ShowEditDialogAsync<AdjustmentReasonEditDialog, AdjustmentReasonForm>("調整理由追加", new AdjustmentReasonForm(), Styles.SmallDialog);
        if (form is null)
        {
            return;
        }

        await RunAsync(async () => NotifyResult(await AdjustmentReasonService.InsertAsync(AdjustmentReasonForm.ToEntity(form), CancellationToken), "追加しました。"), LoadAsync);
    }

    private async Task EditAsync(AdjustmentReasonEntity entity)
    {
        var form = await ShowEditDialogAsync<AdjustmentReasonEditDialog, AdjustmentReasonForm>("調整理由編集", AdjustmentReasonForm.ToForm(entity), Styles.SmallDialog);
        if (form is null)
        {
            return;
        }

        await RunAsync(async () => NotifyResult(await AdjustmentReasonService.UpdateAsync(AdjustmentReasonForm.ToEntity(form), CancellationToken), "更新しました。"), LoadAsync);
    }

    private async Task DeleteAsync(AdjustmentReasonEntity entity)
    {
        if (!await ConfirmDeleteAsync(entity.Name))
        {
            return;
        }

        await RunAsync(async () => NotifyResult(await AdjustmentReasonService.DeleteAsync(entity.Id, CancellationToken), "削除しました。"), LoadAsync);
    }
}
