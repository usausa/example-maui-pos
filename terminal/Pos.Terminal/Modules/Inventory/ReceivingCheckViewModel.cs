namespace Pos.Terminal.Modules.Inventory;

// 検品の明細 (状態の文言と色は画面側の Converter で付ける)
public sealed record ReceivingLineItem(
    ReceivingLine Line,
    ReceivingLineState State,
    string Name,
    string Code,
    string CountText,
    string Detail);

// 検品 (オンライン限定): スキャン・コード入力・行のタップで明細を選んで届いた数を入れ、受領する。
// 数えていない明細は予定 (移動は出荷) の数で受け取る
public sealed partial class ReceivingCheckViewModel : AppViewModelBase
{
    private readonly IDialog dialog;

    private readonly IPopupNavigator popupNavigator;

    private readonly StockUsecase stock;

    private readonly ReceivingUsecase receiving;

    // 一覧・スキャンの画面と共有する状態 (Scope プラグインが注入する)
    [Scope]
    public ReceivingContext ReceivingContext { get; set; } = default!;

    [ObservableProperty]
    public partial ReceivingKind Kind { get; set; }

    [ObservableProperty]
    public partial string Source { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Number { get; set; } = string.Empty;

    // 伝票の備考 (配送の指示など)
    [ObservableProperty]
    public partial string Note { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ProgressText { get; set; } = string.Empty;

    public ObservableCollection<ReceivingLineItem> Items { get; } = [];

    public IObserveCommand InputCodeCommand { get; }

    public IObserveCommand SelectCommand { get; }

    public ReceivingCheckViewModel(
        IDialog dialog,
        IPopupNavigator popupNavigator,
        StockUsecase stock,
        ReceivingUsecase receiving)
    {
        this.dialog = dialog;
        this.popupNavigator = popupNavigator;
        this.stock = stock;
        this.receiving = receiving;

        InputCodeCommand = MakeAsyncCommand(InputCodeAsync);
        SelectCommand = MakeAsyncCommand<ReceivingLineItem>(x => InputCountAsync(x.Line));
    }

    public override async Task OnNavigatedToAsync(INavigationContext context)
    {
        if (ReceivingContext.Document is not { } document)
        {
            await Navigator.PostForwardAsync(ViewId.ReceivingList);
            return;
        }

        Kind = document.Kind;
        Source = document.Source;
        Number = document.Number ?? string.Empty;
        Note = document.Note ?? string.Empty;
        Refresh();

        var scanned = context.Parameter.GetScanResult();
        if (scanned is not null)
        {
            await Navigator.PostActionAsync(() => HandleCodeAsync(scanned));
        }
    }

    private void Refresh()
    {
        var document = ReceivingContext.Document!;
        Items.Replace(document.Lines.Select(x => ToItem(x, ReceivingContext.Counts.TryGetValue(x.Id, out var count) ? count : null)));
        ProgressText = $"確認 {ReceivingContext.Counts.Count} / {document.Lines.Count} 点";
    }

    private static ReceivingLineItem ToItem(ReceivingLine line, decimal? count)
    {
        var state = count switch
        {
            null => ReceivingLineState.Unchecked,
            { } value when value == line.Quantity => ReceivingLineState.Match,
            { } value when value < line.Quantity => ReceivingLineState.Shortage,
            _ => ReceivingLineState.Excess
        };
        var difference = count is { } counted && (counted != line.Quantity) ? $"  差 {(counted > line.Quantity ? "+" : string.Empty)}{ViewHelper.Quantity(counted - line.Quantity)}" : string.Empty;
        return new ReceivingLineItem(line, state, line.ProductName, line.ProductCode, count is null ? "-" : ViewHelper.Quantity(count.Value), $"予定 {ViewHelper.Quantity(line.Quantity)}{difference}");
    }

    // コードは電卓で入力する (キーボードに依存しない)
    private async Task InputCodeAsync()
    {
        var code = await popupNavigator.InputProductCodeAsync();
        if (!String.IsNullOrEmpty(code))
        {
            await HandleCodeAsync(code);
        }
    }

    // 商品コードは明細と、JAN は端末の商品マスタと突き合わせる
    private async Task HandleCodeAsync(string code)
    {
        var document = ReceivingContext.Document!;
        var line = document.Lines.FirstOrDefault(x => x.ProductCode == code);
        if (line is null)
        {
            var product = await stock.FindProductAsync(code);
            if (product is null)
            {
                await dialog.InformationAsync($"商品が見つかりません: {code}");
                return;
            }

            line = document.Lines.FirstOrDefault(x => x.ProductId == product.Id);
            if (line is null)
            {
                await dialog.InformationAsync($"この伝票にない商品です。\n{product.Name}");
                return;
            }
        }

        await InputCountAsync(line);
    }

    // 初期値は数えた数、まだなら予定の数
    private async Task InputCountAsync(ReceivingLine line)
    {
        var current = ReceivingContext.Counts.TryGetValue(line.Id, out var count) ? count : line.Quantity;
        var text = await popupNavigator.InputStockAsync($"届いた数 (予定 {ViewHelper.Quantity(line.Quantity)})", current);
        if ((text is null) || !Decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var quantity) || (quantity < 0))
        {
            return;
        }

        ReceivingContext.Counts[line.Id] = quantity;
        Refresh();
    }

    protected override async Task OnNotifyBackAsync()
    {
        if ((ReceivingContext.Counts.Count > 0) && !await dialog.AskAsync("数えた数を破棄して戻りますか？", null, "破棄"))
        {
            return;
        }

        await Navigator.ForwardAsync(ViewId.ReceivingList);
    }

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    protected override Task OnNotifyFunction2() =>
        Navigator.ForwardAsync(ViewId.Scan, Parameters.Make().WithScan(ScanMode.ProductOnce, ViewId.ReceivingCheck));

    protected override async Task OnNotifyFunction4()
    {
        if (ReceivingContext.Document is not { } document)
        {
            return;
        }

        var uncounted = document.Lines.Count(x => !ReceivingContext.Counts.ContainsKey(x.Id));
        var message = uncounted > 0
            ? $"未確認の {uncounted} 点は予定の数で受け取ります。\n受領しますか？"
            : "受領しますか？\n届いた数が在庫に入ります。";
        if (!await dialog.AskAsync(message, "受領", "受領"))
        {
            return;
        }

        if (await receiving.ReceiveAsync(document, ReceivingContext.Counts))
        {
            await dialog.Toast("受領しました。");
            await Navigator.ForwardAsync(ViewId.ReceivingList);
        }
    }
}
