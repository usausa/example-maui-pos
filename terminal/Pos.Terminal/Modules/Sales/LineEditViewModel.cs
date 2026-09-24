namespace Pos.Terminal.Modules.Sales;

using Pos.Terminal.Models.Cart;

public sealed record LineEditParameter(CartLine Line, IReadOnlyList<DiscountResponseItem> Discounts);

public enum LineEditResult
{
    Cancel,
    Ok,
    Delete
}

// 明細編集: 数量・単価 (AllowsPriceOverride のみ)・明細値引・シリアル番号・備考。OK で明細に書き戻す
public sealed partial class LineEditViewModel : AppDialogViewModelBase, IPopupInitialize<LineEditParameter>
{
    private readonly IDialog dialog;

    private readonly IPopupNavigator popupNavigator;

    private CartLine line = default!;

    private IReadOnlyList<DiscountResponseItem> discounts = [];

    private decimal quantity;

    private decimal unitPrice;

    private CartDiscount? discount;

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string QuantityText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PriceCaption { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string UnitPriceText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool CanOverridePrice { get; set; }

    [ObservableProperty]
    public partial string DiscountText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasDiscount { get; set; }

    [ObservableProperty]
    public partial string SerialCaption { get; set; } = string.Empty;

    public EntryController Serial { get; }

    public EntryController Note { get; }

    public IObserveCommand DecrementCommand { get; }

    public IObserveCommand IncrementCommand { get; }

    public IObserveCommand InputQuantityCommand { get; }

    public IObserveCommand InputPriceCommand { get; }

    public IObserveCommand DiscountCommand { get; }

    public IObserveCommand ClearDiscountCommand { get; }

    public IObserveCommand DeleteCommand { get; }

    public IObserveCommand CloseCommand { get; }

    public IObserveCommand CommitCommand { get; }

    public LineEditViewModel(
        IDialog dialog,
        IPopupNavigator popupNavigator)
    {
        this.dialog = dialog;
        this.popupNavigator = popupNavigator;

        DecrementCommand = MakeDelegateCommand(() => SetQuantity(quantity - 1));
        IncrementCommand = MakeDelegateCommand(() => SetQuantity(quantity + 1));
        InputQuantityCommand = MakeAsyncCommand(InputQuantityAsync);
        InputPriceCommand = MakeAsyncCommand(InputPriceAsync);
        DiscountCommand = MakeAsyncCommand(ChooseDiscountAsync);
        ClearDiscountCommand = MakeDelegateCommand(() => SetDiscount(null));
        DeleteCommand = MakeAsyncCommand(async () =>
        {
            if (await dialog.AskAsync("この明細を削除しますか？", null, "削除"))
            {
                await popupNavigator.CloseAsync(LineEditResult.Delete);
            }
        });
        CloseCommand = MakeAsyncCommand(async () => await popupNavigator.CloseAsync(LineEditResult.Cancel));
        CommitCommand = MakeAsyncCommand(CommitAsync);
        // キーボードの Enter でも確定する (キーボードが出ている間はシートの下段ボタンが隠れる)
        Serial = new EntryController(CommitCommand);
        Note = new EntryController(CommitCommand);
    }

    public void Initialize(LineEditParameter parameter)
    {
        line = parameter.Line;
        discounts = parameter.Discounts;

        Name = line.Product.Name;
        CanOverridePrice = line.Product.AllowsPriceOverride;
        PriceCaption = CanOverridePrice ? $"単価 (定価 {ViewHelper.Yen(line.Product.Price)})" : "単価";
        SerialCaption = line.Product.RequiresSerial ? "シリアル番号 (必須)" : "シリアル番号";
        Serial.Text = String.Join(",", line.SerialNumbers);
        Note.Text = line.Note;

        SetQuantity(line.Quantity);
        SetUnitPrice(line.UnitPrice);
        SetDiscount(line.Discounts.FirstOrDefault());
    }

    private void SetQuantity(decimal value)
    {
        quantity = Math.Max(1, value);
        QuantityText = ViewHelper.Quantity(quantity);
    }

    private void SetUnitPrice(decimal value)
    {
        unitPrice = value;
        UnitPriceText = ViewHelper.Yen(value);
    }

    private void SetDiscount(CartDiscount? value)
    {
        discount = value;
        HasDiscount = value is not null;
        DiscountText = value is null ? "なし" : $"{value.Name} ({ViewHelper.DiscountValue(value.Type, value.Value)})";
    }

    private async Task InputQuantityAsync()
    {
        var text = await popupNavigator.InputQuantityAsync("数量", quantity);
        if (Decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) && (value > 0))
        {
            SetQuantity(value);
        }
    }

    private async Task InputPriceAsync()
    {
        var text = await popupNavigator.InputAmountAsync("単価", unitPrice);
        if (Decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) && (value >= 0))
        {
            SetUnitPrice(value);
        }
    }

    private async Task ChooseDiscountAsync()
    {
        var selected = await popupNavigator.PopupAsync<DiscountParameter, CartDiscount?>(DialogId.Discount, new DiscountParameter("明細値引", discounts, unitPrice * quantity));
        if (selected is not null)
        {
            SetDiscount(selected);
        }
    }

    private async Task CommitAsync()
    {
        var serials = (Serial.Text ?? string.Empty).Split([',', '、', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (line.Product.RequiresSerial && (serials.Length == 0))
        {
            await dialog.InformationAsync("シリアル番号を入力してください。");
            Serial.Focus();
            return;
        }

        line.Quantity = quantity;
        line.UnitPrice = unitPrice;
        line.Discounts.Clear();
        if (discount is not null)
        {
            line.Discounts.Add(discount);
        }

        line.SerialNumbers.Clear();
        foreach (var serial in serials)
        {
            line.SerialNumbers.Add(serial);
        }

        line.Note = Note.Text.TrimToNull();

        await popupNavigator.CloseAsync(LineEditResult.Ok);
    }
}
