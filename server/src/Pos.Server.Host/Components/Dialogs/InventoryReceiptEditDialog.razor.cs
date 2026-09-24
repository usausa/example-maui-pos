namespace Pos.Server.Host.Components.Dialogs;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Entity;
using Pos.Server.Models.Parameters;
using Pos.Server.Services;

// 入荷予定の登録 (入荷する店舗・仕入先・納品書番号・入荷予定日・明細)。明細は行ごとの入力なので、保存のときにフォーム全体を検証する
public sealed partial class InventoryReceiptEditDialog
{
    private const int SearchLimit = 20;

    private static readonly InventoryReceiptFormValidator Validator = new();

    private MudForm editForm = default!;
    private List<SupplierEntity> suppliers = [];
    private string? errorMessage;

    [Parameter]
    public required string Title { get; set; }

    [Parameter]
    public required InventoryReceiptForm Form { get; set; }

    [CascadingParameter]
    public required IMudDialogInstance MudDialog { get; set; }

    [Inject]
    public required ProductService ProductService { get; set; }

    [Inject]
    public required SupplierService SupplierService { get; set; }

    protected override async Task OnInitializedAsync()
    {
        suppliers = (await SupplierService.QueryListAsync(false, CancellationToken.None)).Where(static x => x.IsActive).ToList();
    }

    // 在庫管理対象の販売中の商品をコード / JAN / 名称 / かなで検索
    private async Task<IEnumerable<ProductEntity>> SearchProductsAsync(string? value, CancellationToken cancellationToken)
    {
        var result = await ProductService.QueryPageAsync(new ProductQueryParameter { Keyword = value, IsActive = true, Size = SearchLimit }, cancellationToken);
        return result.Items.Where(static x => x.TrackInventory);
    }

    // 商品を選んだら仕入単価をその商品の原価にする
    private static void OnProductChanged(InventoryReceiptFormLine line, ProductEntity? product)
    {
        line.Product = product;
        if (product is not null)
        {
            line.Cost = product.Cost;
        }
    }

    private void AddLine() => Form.Lines.Add(new InventoryReceiptFormLine());

    private void RemoveLine(InventoryReceiptFormLine line) => Form.Lines.Remove(line);

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
