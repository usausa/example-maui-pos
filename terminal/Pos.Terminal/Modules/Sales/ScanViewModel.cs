namespace Pos.Terminal.Modules.Sales;

using BarcodeScanning;

using Pos.Terminal.Modules.Inquiry;
using Pos.Terminal.Modules.Inventory;
using Pos.Terminal.Modules.Returns;

// スキャン: 商品モードは読むたびに明細追加して継続、他のモードは 1 件読んだら呼び出し元へ戻る
public sealed partial class ScanViewModel : AppViewModelBase
{
    private static readonly TimeSpan SameCodeInterval = TimeSpan.FromSeconds(2);

    private readonly IPopupNavigator popupNavigator;

    private readonly DataAccessor accessor;

    private readonly SalesUsecase salesUsecase;

    private ScanMode mode;

    private ViewId returnTo;

    private ViewId? callerReturnTo;

    private Dictionary<Guid, TaxRateResponseItem> taxRates = [];

    private string lastValue = string.Empty;

    private DateTime lastDetected;

    private bool returning;

    // 途中の画面なので、呼び出し元の機能の状態を保持する (商品モードはカートに追加する)
    [Scope]
    public SalesContext SalesContext { get; set; } = default!;

    [Scope]
    public ReturnContext ReturnContext { get; set; } = default!;

    [Scope]
    public StockContext StockContext { get; set; } = default!;

    [Scope]
    public ReceivingContext ReceivingContext { get; set; } = default!;

    [Scope]
    public CustomerDraft CustomerDraft { get; set; } = default!;

    public BarcodeController Controller { get; } = new();

    [ObservableProperty]
    public partial string Title { get; set; } = "スキャン";

    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    public IObserveCommand DetectCommand { get; }

    public ScanViewModel(
        IPopupNavigator popupNavigator,
        DataAccessor accessor,
        SalesUsecase salesUsecase)
    {
        this.popupNavigator = popupNavigator;
        this.accessor = accessor;
        this.salesUsecase = salesUsecase;

        Controller.TapToFocus = true;
        Controller.VibrationOnDetect = true;

        DetectCommand = MakeAsyncCommand<IReadOnlySet<BarcodeResult>>(DetectAsync);
    }

    public override async Task OnNavigatedToAsync(INavigationContext context)
    {
        mode = context.Parameter.GetScanMode();
        returnTo = context.Parameter.GetReturnTo(ViewId.Sales);
        callerReturnTo = context.Parameter.GetCallerReturnTo();
        Title = mode switch
        {
            ScanMode.Product or ScanMode.ProductOnce => "スキャン (商品)",
            ScanMode.Customer => "スキャン (会員)",
            ScanMode.Receipt => "スキャン (レシート)",
            _ => "スキャン"
        };

        await Navigator.PostActionAsync(PrepareAsync);
    }

    private async Task PrepareAsync()
    {
        if (mode == ScanMode.Product)
        {
            taxRates = (await accessor.QueryTaxRateListAsync()).ToDictionary(static x => x.Id);
        }

        if (await Permissions.RequestCameraAsync())
        {
            Controller.Enable = true;
        }
        else
        {
            Message = "カメラの権限がありません";
        }
    }

    public override Task OnNavigatingFromAsync(INavigationContext context)
    {
        Controller.Enable = false;
        Controller.TorchOn = false;
        return Task.CompletedTask;
    }

    private Task DetectAsync(IReadOnlySet<BarcodeResult> results)
    {
        if ((results.Count == 0) || returning)
        {
            return Task.CompletedTask;
        }

        var value = results.First().DisplayValue;
        if (String.IsNullOrEmpty(value))
        {
            return Task.CompletedTask;
        }

        // 同じコードが連続して検出されるのを抑える
        var now = DateTime.UtcNow;
        var same = (value == lastValue) && (now - lastDetected < SameCodeInterval);
        lastValue = value;
        lastDetected = now;
        if (same)
        {
            return Task.CompletedTask;
        }

        return HandleAsync(value);
    }

    private async Task HandleAsync(string value)
    {
        if (mode == ScanMode.Product)
        {
            await AddProductAsync(value);
            return;
        }

        // 1 件読んだら呼び出し元へ
        returning = true;
        Controller.Enable = false;
        await Navigator.ForwardAsync(returnTo, Parameters.Make().WithScanResult(value).WithCallerReturnTo(callerReturnTo));
    }

    private async Task AddProductAsync(string value)
    {
        var product = await accessor.QueryProductByBarcodeAsync(value) ?? await accessor.QueryProductByCodeAsync(value);
        if (product is null)
        {
            Message = $"❌ 商品が見つかりません: {value}";
            return;
        }

        if (!product.IsActive || product.IsDeleted)
        {
            Message = $"❌ 取扱終了: {product.Name}";
            return;
        }

        if (!taxRates.TryGetValue(product.TaxRateId, out var taxRate))
        {
            Message = "❌ マスタを同期してください";
            return;
        }

        var line = SalesContext.Cart.Add(product, taxRate);
        Message = $"✓ {product.Name} を追加 (×{ViewHelper.Quantity(line.Quantity)})  {ViewHelper.Yen(salesUsecase.Calculate(SalesContext.Cart, []).Total)}";
    }

    protected override Task OnNotifyBackAsync() =>
        Navigator.ForwardAsync(returnTo, Parameters.Make().WithCallerReturnTo(callerReturnTo));

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    protected override Task OnNotifyFunction2()
    {
        Controller.ToggleTorch();
        return Task.CompletedTask;
    }

    protected override async Task OnNotifyFunction3()
    {
        // 数字のコードは電卓で入力する (キーボードに依存しない)
        var text = await popupNavigator.InputProductCodeAsync();
        if (!String.IsNullOrWhiteSpace(text))
        {
            await HandleAsync(text.Trim());
        }
    }

    protected override Task OnNotifyFunction4() => OnNotifyBackAsync();
}
