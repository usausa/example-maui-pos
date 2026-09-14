namespace Pos.Terminal.Modules.Sales;

using Pos.Contract.Transactions;

// 会計完了: 釣銭 (返品なら返金額) と付与ポイントを大きく見せて、次の会計へ
public sealed partial class CompleteViewModel : AppViewModelBase
{
    private readonly TransactionUsecase transactions;

    private TransactionResponseItem? transaction;

    [ObservableProperty]
    public partial string Title { get; set; } = "会計完了";

    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ChangeCaption { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ChangeText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TotalText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PointsText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ReceiptNoText { get; set; } = string.Empty;

    public CompleteViewModel(TransactionUsecase transactions)
    {
        this.transactions = transactions;
    }

    public override async Task OnNavigatedToAsync(INavigationContext context)
    {
        var id = context.Parameter.GetTransactionId();
        if (id is null)
        {
            await Navigator.PostForwardAsync(ViewId.Menu);
            return;
        }

        await Navigator.PostActionAsync(() => LoadAsync(id.Value));
    }

    private async Task LoadAsync(Guid id)
    {
        transaction = await transactions.QueryAsync(id);
        if (transaction is null)
        {
            await Navigator.ForwardAsync(ViewId.Menu);
            return;
        }

        var isReturn = transaction.Type.IsReturn();
        Title = isReturn ? "返品完了" : "会計完了";
        Message = isReturn ? "✅ 返品を登録しました" : "✅ ありがとうございました";
        ChangeCaption = isReturn ? "返金額" : "お釣り";
        ChangeText = DisplayText.Yen(isReturn ? transaction.Total : transaction.ChangeAmount);
        TotalText = $"合計 {DisplayText.Yen(transaction.Total)}  お預り {DisplayText.Yen(transaction.TenderedTotal)}";
        if ((transaction.PointsEarned != 0) || (transaction.PointsRedeemed != 0))
        {
            var balance = transaction.PointsBalanceAfter is null ? string.Empty : $"  残高 {DisplayText.Points(transaction.PointsBalanceAfter.Value)}";
            PointsText = isReturn
                ? $"ポイント取消 {-transaction.PointsEarned:#,##0}  返還 {-transaction.PointsRedeemed:#,##0}{balance}"
                : $"ポイント付与 {DisplayText.Points(transaction.PointsEarned)}  利用 {transaction.PointsRedeemed:#,##0}{balance}";
        }

        ReceiptNoText = $"No. {transaction.ReceiptNo}";
    }

    protected override Task OnNotifyBackAsync() => OnNotifyFunction4();

    protected override Task OnNotifyFunction2() =>
        transaction is null
            ? Task.CompletedTask
            : Navigator.ForwardAsync(ViewId.Receipt, Parameters.Make().WithReturnTo(ViewId.Complete).WithTransactionId(transaction.Id));

    // 次へ: 返品ならホーム、販売なら新しい会計
    protected override Task OnNotifyFunction4() =>
        transaction?.Type.IsReturn() ?? true
            ? Navigator.ForwardAsync(ViewId.Menu)
            : Navigator.ForwardAsync(ViewId.Sales, Parameters.Make().WithContext(new SalesContext()));
}
