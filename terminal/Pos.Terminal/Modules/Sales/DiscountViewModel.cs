namespace Pos.Terminal.Modules.Sales;

using Pos.Terminal.Models.Cart;
using Pos.Terminal.Modules.Dialogs;

public sealed record DiscountParameter(string Title, IReadOnlyList<DiscountResponseItem> Discounts, decimal BaseAmount);

public sealed record DiscountItem(DiscountResponseItem Discount, string Name, string ValueText);

// 値引 (取引・明細): 定義済みの選択、または任意額・任意率 + 理由。承認が必要な値引は承認者を選ぶ
public sealed partial class DiscountViewModel : AppDialogViewModelBase, IPopupInitialize<DiscountParameter>
{
    private static readonly ReasonItem[] Reasons =
    [
        new(null, "店長判断"),
        new(null, "キャンペーン"),
        new(null, "傷・汚れ"),
        new(null, "端数調整")
    ];

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

    [ObservableProperty]
    public partial string? ReasonText { get; set; }

    public IObserveCommand SelectCommand { get; }

    public IObserveCommand SelectTypeCommand { get; }

    public IObserveCommand InputValueCommand { get; }

    public IObserveCommand SelectReasonCommand { get; }

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
        InputValueCommand = MakeAsyncCommand(async () => ValueText = await popupNavigator.InputDiscountValueAsync(IsAmount, ValueText) ?? ValueText);
        SelectReasonCommand = MakeAsyncCommand(async () =>
        {
            var reason = await popupNavigator.PopupAsync<ReasonSelectParameter, ReasonSelectResult?>(DialogId.ReasonSelect, new ReasonSelectParameter("値引の理由", Reasons, true));
            if (reason is not null)
            {
                ReasonText = reason.Text;
            }
        });
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
            .Select(static x => new DiscountItem(x, (x.RequiresApproval ? "🔑 " : string.Empty) + x.Name, ViewHelper.DiscountValue(x.Type, x.Value))));
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

            approver = await popupNavigator.ChooseAsync(staff, static x => $"{x.Name} ({ViewHelper.Name(x.Role)})", "承認者");
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

        if (String.IsNullOrWhiteSpace(ReasonText))
        {
            await dialog.InformationAsync("任意の値引には理由が必要です。");
            return;
        }

        await popupNavigator.CloseAsync(new CartDiscount
        {
            Id = Guid.NewGuid(),
            Name = IsAmount ? $"値引 {ViewHelper.Yen(value)}" : $"値引 {value:0.#}%",
            Type = IsAmount ? DiscountType.Amount : DiscountType.Percent,
            Value = IsAmount ? value : value / 100m,
            Reason = ReasonText.Trim()
        });
    }
}
