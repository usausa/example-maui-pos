namespace Pos.Terminal.Modules.Sales;

using Pos.Terminal.Models.Cart;

public sealed record DiscountItem(DiscountResponseItem Discount, string Name, string ValueText);

// 取引値引: 定義済みの選択、または任意額・任意率 + 理由
public sealed partial class DiscountViewModel : AppDialogViewModelBase, IPopupInitialize<DiscountParameter>
{
    private readonly IDialog dialog;

    private readonly IPopupNavigator popupNavigator;

    private readonly Session session;

    private readonly DataAccessor accessor;

    private decimal baseAmount;

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    public ObservableCollection<DiscountItem> Items { get; } = [];

    [ObservableProperty]
    public partial bool IsAmount { get; set; } = true;

    [ObservableProperty]
    public partial string? ValueText { get; set; }

    public EntryController Reason { get; } = new();

    public IObserveCommand SelectCommand { get; }

    public IObserveCommand SelectTypeCommand { get; }

    public IObserveCommand InputValueCommand { get; }

    public IObserveCommand CloseCommand { get; }

    public IObserveCommand CommitCommand { get; }

    public DiscountViewModel(
        IDialog dialog,
        IPopupNavigator popupNavigator,
        Session session,
        DataAccessor accessor)
    {
        this.dialog = dialog;
        this.popupNavigator = popupNavigator;
        this.session = session;
        this.accessor = accessor;

        SelectCommand = MakeAsyncCommand<DiscountItem>(SelectAsync);
        SelectTypeCommand = MakeDelegateCommand<string>(x => IsAmount = x == "Amount");
        InputValueCommand = MakeAsyncCommand(async () => ValueText = await popupNavigator.InputNumberAsync(IsAmount ? "値引額 (¥)" : "値引率 (%)", ValueText ?? "0", 7) ?? ValueText);
        CloseCommand = MakeAsyncCommand(async () => await popupNavigator.CloseAsync());
        CommitCommand = MakeAsyncCommand(CommitAsync);
    }

    public void Initialize(DiscountParameter parameter)
    {
        Title = parameter.Title;
        baseAmount = parameter.BaseAmount;
        Items.Replace(parameter.Discounts
            .Where(static x => x.IsActive && !x.IsDeleted)
            .OrderBy(static x => x.SortOrder)
            .Select(static x => new DiscountItem(x, (x.RequiresApproval ? "🔑 " : string.Empty) + x.Name, DiscountChooser.Describe(x.Type, x.Value))));
    }

    private async Task SelectAsync(DiscountItem item)
    {
        var definition = item.Discount;
        StaffResponseItem? approver = null;
        if (definition.RequiresApproval)
        {
            var staff = session.StoreId is null
                ? []
                : (await accessor.QueryStaffListAsync(session.StoreId.Value)).Where(static x => x.Role is StaffRole.Manager or StaffRole.Admin).ToList();
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
        if (!Decimal.TryParse(ValueText, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) || (value <= 0))
        {
            await dialog.InformationAsync("値を入力してください。");
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
