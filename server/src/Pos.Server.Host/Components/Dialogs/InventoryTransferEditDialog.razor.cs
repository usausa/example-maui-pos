namespace Pos.Server.Host.Components.Dialogs;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Entity;
using Pos.Server.Models.Parameters;
using Pos.Server.Services;

// 店舗間移動の依頼 (出荷店・入荷店・明細)。明細は行ごとの入力なので、保存のときにフォーム全体を検証する
public sealed partial class InventoryTransferEditDialog
{
    private const int SearchLimit = 20;

    private static readonly InventoryTransferFormValidator Validator = new();

    private MudForm editForm = default!;
    private string? errorMessage;

    [Parameter]
    public required string Title { get; set; }

    [Parameter]
    public required InventoryTransferForm Form { get; set; }

    [CascadingParameter]
    public required IMudDialogInstance MudDialog { get; set; }

    [Inject]
    public required ProductService ProductService { get; set; }

    // 在庫管理対象の販売中の商品をコード / JAN / 名称 / かなで検索
    private async Task<IEnumerable<ProductEntity>> SearchProductsAsync(string? value, CancellationToken cancellationToken)
    {
        var result = await ProductService.QueryPageAsync(new ProductQueryParameter { Keyword = value, IsActive = true, Size = SearchLimit }, cancellationToken);
        return result.Items.Where(static x => x.TrackInventory);
    }

    private void AddLine() => Form.Lines.Add(new InventoryTransferFormLine());

    private void RemoveLine(InventoryTransferFormLine line) => Form.Lines.Remove(line);

    private async Task OnOkClick()
    {
        await editForm.ValidateAsync();
        var result = await Validator.ValidateAsync(Form);
        if (!editForm.IsValid || !result.IsValid)
        {
            errorMessage = result.Errors.FirstOrDefault()?.ErrorMessage;
            return;
        }

        MudDialog.Close(DialogResult.Ok(Form));
    }

    private void OnCancelClick() => MudDialog.Cancel();
}
