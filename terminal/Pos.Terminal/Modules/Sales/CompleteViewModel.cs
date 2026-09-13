namespace Pos.Terminal.Modules.Sales;

// T-21 会計完了: 釣銭 (返品なら返金額) と付与ポイントを大きく見せて、次の会計へ
public sealed partial class CompleteViewModel : AppViewModelBase
{
    private readonly SalesState sales;

    private bool isReturn;

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

    public CompleteViewModel(SalesState sales)
    {
        this.sales = sales;
    }

    public override async Task OnNavigatedToAsync(INavigationContext context)
    {
        var transaction = sales.Completed;
        if (transaction is null)
        {
            await Navigator.ForwardAsync(ViewId.Menu);
            return;
        }

        isReturn = transaction.Type == TransactionType.Return;
        Title = isReturn ? "返品完了" : "会計完了";
        Message = isReturn ? "✅ 返品を登録しました" : "✅ お買い上げありがとうございました";
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
        Navigator.ForwardAsync(ViewId.Receipt, Parameters.Make().WithReturnTo(ViewId.Complete));

    protected override Task OnNotifyFunction4()
    {
        if (isReturn)
        {
            sales.ResetReturn();
            sales.Completed = null;
            return Navigator.ForwardAsync(ViewId.Menu);
        }

        sales.ResetSale();
        sales.Completed = null;
        return Navigator.ForwardAsync(ViewId.Sales);
    }
}
