namespace Pos.Server.Host.Components.Dialogs;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using Pos.Server.Models.Entity;
using Pos.Server.Services;

// S-21 取引詳細 (参照のみ。取消・返品は端末で行う)
public sealed partial class TransactionDetailDialog
{
    private TransactionEntity? transaction;
    private IReadOnlyList<TransactionLineEntity> lines = [];
    private IReadOnlyList<TransactionLineSerialEntity> serials = [];
    private IReadOnlyList<TransactionDiscountEntity> discounts = [];
    private IReadOnlyList<TransactionTaxSummaryEntity> taxSummaries = [];
    private IReadOnlyList<TransactionPaymentEntity> payments = [];
    private TransactionDeliveryEntity? delivery;
    private CustomerEntity? customer;
    private TransactionEntity? original;
    private List<TransactionEntity> returns = [];

    [Parameter]
    public Guid Id { get; set; }

    [Parameter]
    public required NameLookup Names { get; set; }

    [CascadingParameter]
    public required IMudDialogInstance MudDialog { get; set; }

    [Inject]
    public required TransactionService TransactionService { get; set; }

    [Inject]
    public required CustomerService CustomerService { get; set; }

    protected override Task OnInitializedAsync() => LoadAsync(Id);

    private async Task LoadAsync(Guid id)
    {
        var token = CancellationToken.None;
        var detail = await TransactionService.QueryDetailAsync(id, token);
        if (detail is null)
        {
            return;
        }

        transaction = detail.Transaction;
        lines = detail.Lines;
        serials = detail.Serials;
        discounts = detail.Discounts;
        taxSummaries = detail.TaxSummaries;
        payments = detail.Payments;
        delivery = detail.Delivery;
        customer = transaction.CustomerId is null ? null : await CustomerService.QueryAsync(transaction.CustomerId.Value, token);
        original = transaction.OriginalTransactionId is null ? null : await TransactionService.QueryAsync(transaction.OriginalTransactionId.Value, token);
        returns = await TransactionService.QueryReturnsAsync(id, token);
    }

    // 関連取引へ切り替える
    private Task OpenAsync(Guid id)
    {
        transaction = null;
        return LoadAsync(id);
    }
}
