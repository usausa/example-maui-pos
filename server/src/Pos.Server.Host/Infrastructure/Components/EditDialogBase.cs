namespace Pos.Server.Host.Infrastructure.Components;

using Microsoft.AspNetCore.Components;

using MudBlazor;

// 編集ダイアログの基底。検証を通ればフォームを結果として閉じる
public abstract class EditDialogBase<TForm> : ComponentBase
    where TForm : class
{
    protected MudForm EditForm { get; set; } = default!;

    [Parameter]
    public required string Title { get; set; }

    [Parameter]
    public required TForm Form { get; set; }

    [CascadingParameter]
    public required IMudDialogInstance MudDialog { get; set; }

    protected async Task OnOkClick()
    {
        await EditForm.ValidateAsync();
        if (EditForm.IsValid)
        {
            MudDialog.Close(DialogResult.Ok(Form));
        }
    }

    protected void OnCancelClick() => MudDialog.Cancel();
}
