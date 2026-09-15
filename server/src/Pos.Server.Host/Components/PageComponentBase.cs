namespace Pos.Server.Host.Components;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using Pos.Server.Host.Components.Dialogs;
using Pos.Server.Services;

// ページの基底: 読み込み・実行の状態、エラー表示、書き込み結果の通知、確認ダイアログ、編集ダイアログの呼び出し
public abstract class PageComponentBase : AppComponentBase
{
    // 管理画面で「全件」を読むときの上限
    protected const int ListLimit = 1000;

    private CancellationTokenSource? cancellation;

    [Inject]
    public required ISnackbar Snackbar { get; set; }

    [Inject]
    public required IDialogService DialogService { get; set; }

    protected bool IsLoading { get; private set; }

    protected bool IsRunning { get; private set; }

    protected bool IsBusy => IsLoading || IsRunning;

    protected string? ErrorMessage { get; set; }

    // 回線が切れたら実行中の処理を止める
    protected CancellationToken CancellationToken => (cancellation ??= new CancellationTokenSource()).Token;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (cancellation is not null))
        {
            cancellation.Cancel();
            cancellation.Dispose();
            cancellation = null;
        }

        base.Dispose(disposing);
    }

    // 読み込み。失敗はバナーに出す
    protected async Task LoadAsync(Func<Task> load)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            await load();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    // 書き込み。失敗はバナーに出し、終わったら再読み込みする
    protected async Task RunAsync(Func<Task> operation, Func<Task>? reload = null)
    {
        IsRunning = true;
        ErrorMessage = null;
        try
        {
            await operation();
            if (reload is not null)
            {
                await reload();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ErrorMessage = ex.Message;
            Snackbar.AddError(ex.Message);
        }
        finally
        {
            IsRunning = false;
        }
    }

    // 書き込み結果の通知 (Success 以外は失敗の理由)
    protected void NotifyResult(DataWriteStatus status, string success, string? duplicate = null, string? inUse = null, string? invalid = null)
    {
        switch (status)
        {
            case DataWriteStatus.Success:
                Snackbar.AddSuccess(success);
                break;
            case DataWriteStatus.NotFound:
                Snackbar.AddError("対象が存在しません。");
                break;
            case DataWriteStatus.Duplicate:
                Snackbar.AddError(duplicate ?? "コードが重複しています。");
                break;
            case DataWriteStatus.VersionMismatch:
                Snackbar.AddError("他で更新されています。再読み込みしてください。");
                break;
            case DataWriteStatus.InUse:
                Snackbar.AddWarning(inUse ?? "使用中のため削除できません。");
                break;
            default:
                Snackbar.AddWarning(invalid ?? "指定が不正です。");
                break;
        }
    }

    protected void NotifyResult<T>(DataWriteResult<T> result, string success, string? duplicate = null, string? inUse = null, string? invalid = null)
        where T : class =>
        NotifyResult(result.Status, success, duplicate, inUse, invalid);

    protected ValueTask<bool> ConfirmDeleteAsync(string name) =>
        DialogService.ShowConfirm("削除", $"「{name}」を削除しますか？");

    // 編集ダイアログ (Title / Form パラメータを持つ) を開き、保存ならフォームを返す。追加のパラメータは configure で渡す
    protected async Task<TForm?> ShowEditDialogAsync<TDialog, TForm>(string title, TForm form, DialogOptions? options = null, Action<DialogParameters>? configure = null)
        where TDialog : ComponentBase
        where TForm : class
    {
        var parameters = new DialogParameters
        {
            { "Title", title },
            { "Form", form }
        };
        configure?.Invoke(parameters);
        var reference = await DialogService.ShowAsync<TDialog>(string.Empty, parameters, options ?? Styles.MediumDialog);
        var result = await reference.Result;
        return (result is { Canceled: false }) ? (TForm)result.Data! : null;
    }
}
