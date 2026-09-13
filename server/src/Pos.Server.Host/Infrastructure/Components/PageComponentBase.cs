namespace Pos.Server.Host.Infrastructure.Components;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using Smart.Data;

// ページの基底: 読み込み・実行の状態、エラー表示、確認ダイアログ、編集ダイアログの呼び出し
public abstract class PageComponentBase : AppComponentBase
{
    private CancellationTokenSource? cancellation;

    [Inject]
    public required ISnackbar Snackbar { get; set; }

    [Inject]
    public required IDialogService DialogService { get; set; }

    [Inject]
    public required TimeProvider TimeProvider { get; set; }

    [Inject]
    public required IDialect Dialect { get; set; }

    protected bool IsLoading { get; private set; }

    protected bool IsRunning { get; private set; }

    protected bool IsBusy => IsLoading || IsRunning;

    protected string? ErrorMessage { get; set; }

    protected DateTime UtcNow => TimeProvider.GetUtcNow().UtcDateTime;

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

    // 書き込み。コード重複は Snackbar、それ以外の失敗はバナーに出し、成功時は再読み込みする
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
        catch (DbException ex) when (Dialect.IsDuplicate(ex))
        {
            Snackbar.AddError("コードが重複しています。");
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

    protected ValueTask<bool> ConfirmDeleteAsync(string name) =>
        DialogService.ShowConfirm("削除", $"「{name}」を削除しますか？");

    // 楽観ロック失敗 (VERSION_MISMATCH) の案内
    protected void NotifyVersionMismatch() =>
        Snackbar.AddError("他で更新されています。再読み込みしてください。");

    protected void NotifyNotFound() =>
        Snackbar.AddError("対象が存在しません。");

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
