namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;

using Pos.Server.Host.Components.Dialogs;
using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Entity;
using Pos.Server.Services;

// スタッフ。追加・編集・削除と PIN の設定は管理者だけ
public sealed partial class StaffPage
{
    private List<StaffEntity> items = [];
    private Dictionary<Guid, string> stores = [];
    private bool includeDeleted;

    [Inject]
    public required StaffService StaffService { get; set; }

    [Inject]
    public required StoreService StoreService { get; set; }

    protected override Task OnInitializedAsync() => LoadAsync();

    // 本部 (StoreId なし) は全店舗
    private string StoreName(Guid? storeId) => storeId is null ? "全店舗" : stores.GetValueOrDefault(storeId.Value, "-");

    private Task LoadAsync() =>
        LoadAsync(async () =>
        {
            stores = (await StoreService.QueryAllAsync(true, CancellationToken)).ToDictionary(static x => x.Id, static x => x.Name);
            items = await StaffService.QueryAllAsync(includeDeleted, CancellationToken);
        });

    private async Task AddAsync()
    {
        var form = await ShowEditDialogAsync<StaffEditDialog, StaffForm>("スタッフ追加", new StaffForm());
        if (form is null)
        {
            return;
        }

        await RunAsync(async () => NotifyResult(await StaffService.InsertAsync(StaffForm.ToEntity(form), CancellationToken), "追加しました。"), LoadAsync);
    }

    private async Task EditAsync(StaffEntity entity)
    {
        var form = await ShowEditDialogAsync<StaffEditDialog, StaffForm>("スタッフ編集", StaffForm.ToForm(entity));
        if (form is null)
        {
            return;
        }

        await RunAsync(async () => NotifyResult(await StaffService.UpdateAsync(StaffForm.ToEntity(form), CancellationToken), "更新しました。"), LoadAsync);
    }

    // PIN がないスタッフは端末で担当に選べない
    private async Task SetPinAsync(StaffEntity entity)
    {
        var form = await ShowEditDialogAsync<StaffPinDialog, StaffPinForm>("PIN 設定", new StaffPinForm { Id = entity.Id, Name = entity.Name, Version = entity.Version }, Styles.SmallDialog);
        if (form is null)
        {
            return;
        }

        await RunAsync(async () => NotifyResult(await StaffService.UpdatePinAsync(form.Id, form.Pin, form.Version, CancellationToken), "PIN を設定しました。"), LoadAsync);
    }

    private async Task DeleteAsync(StaffEntity entity)
    {
        if (!await ConfirmDeleteAsync(entity.Name))
        {
            return;
        }

        await RunAsync(async () => NotifyResult(await StaffService.DeleteAsync(entity.Id, CancellationToken), "削除しました。"), LoadAsync);
    }
}
