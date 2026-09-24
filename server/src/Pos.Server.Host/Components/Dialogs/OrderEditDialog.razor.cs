namespace Pos.Server.Host.Components.Dialogs;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Entity;
using Pos.Server.Models.Parameters;
using Pos.Server.Services;

// 受注の登録・変更 (連絡先・希望日・備考・明細)。明細は行ごとの入力なので、保存のときにフォーム全体を検証する
public sealed partial class OrderEditDialog
{
    private const int SearchLimit = 20;

    private static readonly OrderFormValidator Validator = new();

    private MudForm editForm = default!;
    private List<StaffEntity> staffList = [];
    private string? errorMessage;

    [Parameter]
    public required string Title { get; set; }

    [Parameter]
    public required OrderForm Form { get; set; }

    [CascadingParameter]
    public required IMudDialogInstance MudDialog { get; set; }

    [Inject]
    public required ProductService ProductService { get; set; }

    [Inject]
    public required CustomerService CustomerService { get; set; }

    [Inject]
    public required StaffService StaffService { get; set; }

    protected override async Task OnInitializedAsync()
    {
        staffList = await StaffService.QueryAllAsync(false, CancellationToken.None);
    }

    // 販売中の商品をコード / JAN / 名称 / かなで検索
    private async Task<IEnumerable<ProductEntity>> SearchProductsAsync(string? value, CancellationToken cancellationToken) =>
        (await ProductService.QueryPageAsync(new ProductQueryParameter { Keyword = value, IsActive = true, Size = SearchLimit }, cancellationToken)).Items;

    private async Task<IEnumerable<CustomerEntity>> SearchCustomersAsync(string? value, CancellationToken cancellationToken) =>
        (await CustomerService.QueryPageAsync(new CustomerQueryParameter { Keyword = value, Size = SearchLimit }, cancellationToken)).Items;

    // 会員を選んだら、空の宛名と電話に会員のものを入れる
    private void OnCustomerChanged(CustomerEntity? customer)
    {
        Form.Customer = customer;
        if (customer is null)
        {
            return;
        }

        if (String.IsNullOrWhiteSpace(Form.CustomerName))
        {
            Form.CustomerName = customer.Name;
        }

        if (String.IsNullOrWhiteSpace(Form.Phone))
        {
            Form.Phone = customer.Phone;
        }
    }

    // 商品を選んだら単価をその商品の売価にする
    private static void OnProductChanged(OrderFormLine line, ProductEntity? product)
    {
        line.Product = product;
        if (product is not null)
        {
            line.UnitPrice = product.Price;
        }
    }

    private void AddLine() => Form.Lines.Add(new OrderFormLine());

    private void RemoveLine(OrderFormLine line) => Form.Lines.Remove(line);

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
