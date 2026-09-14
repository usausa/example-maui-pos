namespace Pos.Terminal.Modules.Shift;

using Pos.Contract.Shifts;

public sealed class DenominationItem : NotificationObject
{
    private readonly Action changed;

    private int count;

    public int Denomination { get; }

    public string Label { get; }

    public int Count
    {
        get => count;
        set
        {
            if (SetProperty(ref count, value))
            {
                RaisePropertyChanged(nameof(CountText));
                RaisePropertyChanged(nameof(SubtotalText));
                changed();
            }
        }
    }

    public string CountText
    {
        get => count.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value2) && (value2 >= 0))
            {
                Count = value2;
            }
        }
    }

    public string SubtotalText => DisplayText.Yen((decimal)Denomination * count);

    public ICommand IncrementCommand { get; }

    public ICommand DecrementCommand { get; }

    public DenominationItem(int denomination, int count, Action changed)
    {
        this.changed = changed;
        this.count = count;
        Denomination = denomination;
        Label = DisplayText.Yen(denomination);
        IncrementCommand = new DelegateCommand(() => Count++);
        DecrementCommand = new DelegateCommand(() => Count = Math.Max(0, Count - 1));
    }
}

public sealed record DenominationsResult(IReadOnlyList<ShiftCloseRequestDenomination> Denominations, decimal Total);

// 金種別入力 (精算)。枚数から実査金額を求める
public sealed partial class DenominationsViewModel : AppDialogViewModelBase, IPopupInitialize<IReadOnlyList<ShiftCloseRequestDenomination>>
{
    private static readonly int[] Denominations = [10000, 5000, 2000, 1000, 500, 100, 50, 10, 5, 1];

    private readonly IPopupNavigator popupNavigator;

    public ObservableCollection<DenominationItem> Items { get; } = [];

    [ObservableProperty]
    public partial string TotalText { get; set; } = DisplayText.Yen(0);

    public IObserveCommand CloseCommand { get; }

    public IObserveCommand CommitCommand { get; }

    public IObserveCommand InputCountCommand { get; }

    public DenominationsViewModel(IPopupNavigator popupNavigator)
    {
        this.popupNavigator = popupNavigator;

        InputCountCommand = MakeAsyncCommand<DenominationItem>(InputCountAsync);
        CloseCommand = MakeAsyncCommand(async () => await popupNavigator.CloseAsync());
        CommitCommand = MakeAsyncCommand(async () => await popupNavigator.CloseAsync(new DenominationsResult(
            Items.Where(static x => x.Count > 0).Select(static x => new ShiftCloseRequestDenomination { Denomination = x.Denomination, Count = x.Count }).ToList(),
            Total())));
    }

    public void Initialize(IReadOnlyList<ShiftCloseRequestDenomination> parameter)
    {
        var counts = parameter.ToDictionary(static x => x.Denomination, static x => x.Count);
        Items.Replace(Denominations.Select(x => new DenominationItem(x, counts.GetValueOrDefault(x), UpdateTotal)));
        UpdateTotal();
    }

    // 枚数は電卓で入力する (キーボードに依存しない)
    private async Task InputCountAsync(DenominationItem item)
    {
        var text = await popupNavigator.InputNumberAsync($"{item.Label} の枚数", item.CountText, 4);
        if (Int32.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) && (count >= 0))
        {
            item.Count = count;
        }
    }

    private decimal Total() => Items.Sum(static x => (decimal)x.Denomination * x.Count);

    private void UpdateTotal()
    {
        TotalText = DisplayText.Yen(Total());
    }
}
