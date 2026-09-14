namespace Pos.Terminal.Modules.Sales;

using Pos.Terminal.Models.Sales;

public sealed record LineEditParameter(CartLine Line, IReadOnlyList<DiscountResponse> Discounts);

public enum LineEditResult
{
    Cancel,
    Ok,
    Delete
}

// P-13 明細編集: 数量・単価 (AllowsPriceOverride のみ)・明細値引・シリアル番号・備考。OK で明細に書き戻す
public sealed partial class LineEditViewModel : AppDialogViewModelBase, IPopupInitialize<LineEditParameter>
{
    private readonly IDialog dialog;

    private readonly IPopupNavigator popupNavigator;

    private readonly DataAccessor accessor;

    private readonly Settings settings;

    private CartLine line = default!;

    private IReadOnlyList<DiscountResponse> discounts = [];

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

    public EntryController Serial { get; } = new();

    public EntryController Note { get; } = new();

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
        IPopupNavigator popupNavigator,
        DataAccessor accessor,
        Settings settings)
    {
        this.dialog = dialog;
        this.popupNavigator = popupNavigator;
        this.accessor = accessor;
        this.settings = settings;

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
    }

    public void Initialize(LineEditParameter parameter)
    {
        line = parameter.Line;
        discounts = parameter.Discounts;

        Name = line.Product.Name;
        CanOverridePrice = line.Product.AllowsPriceOverride;
        PriceCaption = CanOverridePrice ? $"単価 (定価 {DisplayText.Yen(line.Product.Price)})" : "単価";
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
        QuantityText = DisplayText.Quantity(quantity);
    }

    private void SetUnitPrice(decimal value)
    {
        unitPrice = value;
        UnitPriceText = DisplayText.Yen(value);
    }

    private void SetDiscount(CartDiscount? value)
    {
        discount = value;
        HasDiscount = value is not null;
        DiscountText = value is null ? "なし" : $"{value.Name} ({DiscountChooser.Describe(value.Type, value.Value)})";
    }

    private async Task InputQuantityAsync()
    {
        var text = await popupNavigator.InputNumberAsync("数量", DisplayText.Quantity(quantity), 4);
        if (Decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) && (value > 0))
        {
            SetQuantity(value);
        }
    }

    private async Task InputPriceAsync()
    {
        var text = await popupNavigator.InputNumberAsync("単価", unitPrice.ToString("0", CultureInfo.InvariantCulture), 8);
        if (Decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) && (value >= 0))
        {
            SetUnitPrice(value);
        }
    }

    private async Task ChooseDiscountAsync()
    {
        var selected = await DiscountChooser.ChooseAsync(dialog, accessor, settings, new DiscountParameter("明細値引", discounts, unitPrice * quantity));
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

        line.Note = String.IsNullOrWhiteSpace(Note.Text) ? null : Note.Text.Trim();

        await popupNavigator.CloseAsync(LineEditResult.Ok);
    }
}
