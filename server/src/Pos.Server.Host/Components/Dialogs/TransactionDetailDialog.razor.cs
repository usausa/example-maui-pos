namespace Pos.Server.Host.Components.Dialogs;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using Pos.Server.Accessors;
using Pos.Server.Host.Infrastructure.Components;
using Pos.Server.Models.Entity;

// S-21 取引詳細 (参照のみ。取消・返品は端末で行う)
public sealed partial class TransactionDetailDialog
{
    private TransactionEntity? transaction;

    private List<TransactionLineEntity> lines = [];

    private List<TransactionLineSerialEntity> serials = [];

    private List<TransactionDiscountEntity> discounts = [];

    private List<TransactionTaxSummaryEntity> taxSummaries = [];

    private List<TransactionPaymentEntity> payments = [];

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
    public required TransactionAccessor TransactionAccessor { get; set; }

    [Inject]
    public required CustomerAccessor CustomerAccessor { get; set; }

    protected override Task OnInitializedAsync() => LoadAsync(Id);

    private async Task LoadAsync(Guid id)
    {
        var token = CancellationToken.None;
        transaction = await TransactionAccessor.QueryAsync(id, token);
        if (transaction is null)
        {
            return;
        }

        lines = await TransactionAccessor.QueryLinesAsync(id, token);
        serials = await TransactionAccessor.QueryLineSerialsAsync(id, token);
        discounts = await TransactionAccessor.QueryDiscountsAsync(id, token);
        taxSummaries = await TransactionAccessor.QueryTaxSummariesAsync(id, token);
        payments = await TransactionAccessor.QueryPaymentsAsync(id, token);
        delivery = await TransactionAccessor.QueryDeliveryAsync(id, token);
        customer = transaction.CustomerId is null ? null : await CustomerAccessor.QueryAsync(transaction.CustomerId.Value, token);
        original = transaction.OriginalTransactionId is null ? null : await TransactionAccessor.QueryAsync(transaction.OriginalTransactionId.Value, token);
        returns = await TransactionAccessor.QueryReturnsAsync(id, token);
    }

    // 関連取引へ切り替える
    private Task OpenAsync(Guid id)
    {
        transaction = null;
        return LoadAsync(id);
    }
}
