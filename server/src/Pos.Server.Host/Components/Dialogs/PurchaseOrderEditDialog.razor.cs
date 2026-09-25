namespace Pos.Server.Host.Components.Dialogs;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Entity;
using Pos.Server.Models.Parameters;
using Pos.Server.Services;

// 発注の登録と変更 (発注する店舗・仕入先・希望納期・備考・明細)。明細は行ごとの入力なので、保存のときにフォーム全体を検証する
public sealed partial class PurchaseOrderEditDialog
{
    private const int SearchLimit = 20;

    private static readonly PurchaseOrderFormValidator Validator = new();

    private MudForm editForm = default!;
    private List<SupplierEntity> suppliers = [];
    private string? errorMessage;

    [Parameter]
    public required string Title { get; set; }

    [Parameter]
    public required PurchaseOrderForm Form { get; set; }

    [CascadingParameter]
    public required IMudDialogInstance MudDialog { get; set; }

    [Inject]
    public required ProductService ProductService { get; set; }

    [Inject]
    public required SupplierService SupplierService { get; set; }

    // 選べる仕入先は有効なものだけ (変更では今の仕入先も残す)
    protected override async Task OnInitializedAsync()
    {
        suppliers = (await SupplierService.QueryListAsync(false, CancellationToken.None)).Where(x => x.IsActive || (x.Id == Form.SupplierId)).ToList();
    }

    // 在庫管理対象の販売中の商品をコード / JAN / 名称 / かなで検索
    private async Task<IEnumerable<ProductEntity>> SearchProductsAsync(string? value, CancellationToken cancellationToken)
    {
        var result = await ProductService.QueryPageAsync(new ProductQueryParameter { Keyword = value, IsActive = true, Size = SearchLimit }, cancellationToken);
        return result.Items.Where(static x => x.TrackInventory);
    }

    // 商品を選んだら仕入単価をその商品の原価にする
    private static void OnProductChanged(PurchaseOrderFormLine line, ProductEntity? product)
    {
        line.Product = product;
        if (product is not null)
        {
            line.Cost = product.Cost;
        }
    }

    private void AddLine() => Form.Lines.Add(new PurchaseOrderFormLine());

    private void RemoveLine(PurchaseOrderFormLine line) => Form.Lines.Remove(line);

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
