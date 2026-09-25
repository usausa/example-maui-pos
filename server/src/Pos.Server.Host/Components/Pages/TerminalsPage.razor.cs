namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using Pos.Server.Host.Components.Dialogs;
using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Entity;
using Pos.Server.Models.Views;
using Pos.Server.Services;

// レジ端末。端末の登録 (ペアリングコードの発行・登録の解除) とマスタの変更は管理者だけ
public sealed partial class TerminalsPage
{
    private List<TerminalEntity> items = [];
    private Dictionary<Guid, string> stores = [];
    private Dictionary<Guid, TerminalRegistrationView> registrations = [];
    private bool includeDeleted;

    [Inject]
    public required TerminalService TerminalService { get; set; }

    [Inject]
    public required TerminalTokenService TerminalTokenService { get; set; }

    [Inject]
    public required StoreService StoreService { get; set; }

    protected override Task OnInitializedAsync() => LoadAsync();

    private string StoreName(Guid storeId) => stores.GetValueOrDefault(storeId, "-");

    private TerminalRegistrationView? Registration(Guid terminalId) => registrations.GetValueOrDefault(terminalId);

    private Task LoadAsync() =>
        LoadAsync(async () =>
        {
            stores = (await StoreService.QueryAllAsync(true, CancellationToken)).ToDictionary(static x => x.Id, static x => x.Name);
            registrations = (await TerminalTokenService.QueryRegistrationListAsync(CancellationToken)).ToDictionary(static x => x.TerminalId);
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

    // 発行したコードは 10 分間、一度だけ使える。前の登録はペアリングが済むまで有効
    private async Task IssuePairingCodeAsync(TerminalEntity entity)
    {
        TerminalPairingCode? code = null;
        await RunAsync(async () => code = await TerminalTokenService.IssuePairingCodeAsync(entity.Id, CancellationToken));
        if (code is null)
        {
            return;
        }

        await DialogService.ShowAsync<TerminalPairingDialog>(
            string.Empty,
            new DialogParameters
            {
                { nameof(TerminalPairingDialog.Terminal), entity },
                { nameof(TerminalPairingDialog.StoreName), StoreName(entity.StoreId) },
                { nameof(TerminalPairingDialog.Code), code }
            },
            Styles.SmallDialog);
    }

    // 解除した端末は次の通信で初期設定に戻る (端末の未送信のデータは再登録後に送る)
    private async Task RevokeAsync(TerminalEntity entity)
    {
        if (!await DialogService.ShowConfirm("登録の解除", $"「{entity.Name}」の登録を解除しますか？ 端末は次の通信で初期設定に戻ります。"))
        {
            return;
        }

        await RunAsync(async () =>
        {
            await TerminalTokenService.RevokeAsync(entity.Id, CancellationToken);
            Snackbar.AddSuccess("登録を解除しました。");
        }, LoadAsync);
    }
}
