namespace Pos.Terminal.Modules.Sales;

using Pos.Terminal.Models.Sales;

public sealed record DiscountItem(DiscountResponse Discount, string Name, string ValueText);

// P-15 取引値引: 定義済みの選択、または任意額・任意率 + 理由
public sealed partial class DiscountViewModel : AppDialogViewModelBase, IPopupInitialize<DiscountParameter>
{
    private readonly IDialog dialog;

    private readonly IPopupNavigator popupNavigator;

    private readonly DataAccessor accessor;

    private readonly Settings settings;

    private decimal baseAmount;

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<DiscountItem> Items { get; set; } = [];

    [ObservableProperty]
    public partial bool IsAmount { get; set; } = true;

    public EntryController Value { get; } = new();

    public EntryController Reason { get; } = new();

    public IObserveCommand SelectCommand { get; }

    public IObserveCommand SelectTypeCommand { get; }

    public IObserveCommand CloseCommand { get; }

    public IObserveCommand CommitCommand { get; }

    public DiscountViewModel(
        IDialog dialog,
        IPopupNavigator popupNavigator,
        DataAccessor accessor,
        Settings settings)
    {
        this.dialog = dialog;
        this.popupNavigator = popupNavigator;
        this.accessor = accessor;
        this.settings = settings;

        SelectCommand = MakeAsyncCommand<DiscountItem>(SelectAsync);
        SelectTypeCommand = MakeDelegateCommand<string>(x => IsAmount = x == "Amount");
        CloseCommand = MakeAsyncCommand(async () => await popupNavigator.CloseAsync());
        CommitCommand = MakeAsyncCommand(CommitAsync);
    }

    public void Initialize(DiscountParameter parameter)
    {
        Title = parameter.Title;
        baseAmount = parameter.BaseAmount;
        Items = parameter.Discounts
            .Where(static x => x.IsActive && !x.IsDeleted)
            .OrderBy(static x => x.SortOrder)
            .Select(static x => new DiscountItem(x, (x.RequiresApproval ? "🔑 " : string.Empty) + x.Name, DiscountChooser.Describe(x.Type, x.Value)))
            .ToList();
    }

    private async Task SelectAsync(DiscountItem item)
    {
        var definition = item.Discount;
        StaffResponse? approver = null;
        if (definition.RequiresApproval)
        {
            var staff = settings.StoreId is null
                ? []
                : (await accessor.QueryStaffListAsync(settings.StoreId.Value)).Where(static x => x.Role is StaffRole.Manager or StaffRole.Admin).ToList();
            if (staff.Count == 0)
            {
                await dialog.InformationAsync("承認できるスタッフ (店長・管理者) が登録されていません。");
                return;
            }

            approver = await dialog.ChooseAsync(staff, static x => $"{x.Name} ({DisplayText.Name(x.Role)})", "承認者");
            if (approver is null)
            {
                return;
            }
        }

        await popupNavigator.CloseAsync(new CartDiscount
        {
            Id = Guid.NewGuid(),
            DiscountId = definition.Id,
            Name = definition.Name,
            Type = definition.Type,
            Value = definition.Value,
            ApprovedByStaffId = approver?.Id
        });
    }

    private async Task CommitAsync()
    {
        if (!Decimal.TryParse(Value.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) || (value <= 0))
        {
            await dialog.InformationAsync("値を入力してください。");
            Value.Focus();
            return;
        }

        if (IsAmount ? value > baseAmount : value > 100)
        {
            await dialog.InformationAsync(IsAmount ? "値引額が金額を超えています。" : "値引率は 100% 以下にしてください。");
            return;
        }

        if (String.IsNullOrWhiteSpace(Reason.Text))
        {
            await dialog.InformationAsync("任意の値引には理由が必要です。");
            Reason.Focus();
            return;
        }

        await popupNavigator.CloseAsync(new CartDiscount
        {
            Id = Guid.NewGuid(),
            Name = IsAmount ? $"値引 {DisplayText.Yen(value)}" : $"値引 {value:0.#}%",
            Type = IsAmount ? DiscountType.Amount : DiscountType.Percent,
            Value = IsAmount ? value : value / 100m,
            Reason = Reason.Text.Trim()
        });
    }
}
