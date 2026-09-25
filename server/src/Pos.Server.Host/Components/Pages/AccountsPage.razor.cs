namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

using Pos.Server.Host.Components.Dialogs;
using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Entity;
using Pos.Server.Services;

// ユーザー (管理画面のアカウント)。管理者だけが開ける
public sealed partial class AccountsPage
{
    private List<AccountEntity> items = [];

    // ログイン中のアカウント (認証を無効にしてログインしていなければ null)
    private Guid? currentId;

    [CascadingParameter]
    public required Task<AuthenticationState> AuthenticationState { get; set; }

    [Inject]
    public required AccountService AccountService { get; set; }

    protected override async Task OnInitializedAsync()
    {
        currentId = AuthClaims.AccountOf((await AuthenticationState).User)?.Id;
        await LoadAsync();
    }

    private bool IsSelf(AccountEntity entity) => entity.Id == currentId;

    private Task LoadAsync() =>
        LoadAsync(async () => items = await AccountService.QueryAllAsync(CancellationToken));

    private async Task AddAsync()
    {
        var form = await ShowEditDialogAsync<AccountEditDialog, AccountForm>("ユーザー追加", new AccountForm());
        if (form is null)
        {
            return;
        }

        await RunAsync(async () => NotifyResult(await AccountService.InsertAsync(form.Name.Trim(), form.Password, form.Role, CancellationToken), "追加しました。", duplicate: "ID が重複しています。"), LoadAsync);
    }

    private async Task EditAsync(AccountEntity entity)
    {
        var form = await ShowEditDialogAsync<AccountEditDialog, AccountForm>("ユーザー編集", AccountForm.ToForm(entity));
        if (form is null)
        {
            return;
        }

        await RunAsync(async () => NotifyResult(await AccountService.UpdateAsync(form.Id, form.Role, form.IsActive, form.Version, currentId ?? Guid.Empty, CancellationToken), "更新しました。", invalid: "自分自身は変更できません。"), LoadAsync);
    }

    private async Task ChangePasswordAsync(AccountEntity entity)
    {
        var form = await ShowEditDialogAsync<AccountPasswordDialog, AccountPasswordForm>("パスワード変更", new AccountPasswordForm { Id = entity.Id, Name = entity.Name, Version = entity.Version }, Styles.SmallDialog);
        if (form is null)
        {
            return;
        }

        await RunAsync(async () => NotifyResult(await AccountService.UpdatePasswordAsync(form.Id, form.Password, form.Version, CancellationToken), "パスワードを変更しました。"), LoadAsync);
    }

    private async Task DeleteAsync(AccountEntity entity)
    {
        if (!await ConfirmDeleteAsync(entity.Name))
        {
            return;
        }

        await RunAsync(async () => NotifyResult(await AccountService.DeleteAsync(entity.Id, currentId ?? Guid.Empty, CancellationToken), "削除しました。", invalid: "自分自身は削除できません。"), LoadAsync);
    }
}
