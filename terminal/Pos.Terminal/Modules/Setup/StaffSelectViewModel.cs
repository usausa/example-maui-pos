namespace Pos.Terminal.Modules.Setup;

public sealed record StaffItem(StaffResponse Staff, string Name, string RoleText, Color RoleColor);

// T-01 スタッフ選択: 所属店舗のスタッフをタップして担当を決める (PIN は Phase 2)
public sealed partial class StaffSelectViewModel : AppViewModelBase
{
    private static readonly Color CashierColor = Color.FromArgb("#78909C");

    private static readonly Color ManagerColor = Color.FromArgb("#1E88E5");

    private static readonly Color AdminColor = Color.FromArgb("#8E24AA");

    private readonly DataAccessor accessor;

    private readonly Settings settings;

    private readonly Session session;

    [ObservableProperty]
    public partial string StoreText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<StaffItem> Items { get; set; } = [];

    public IObserveCommand SelectCommand { get; }

    public StaffSelectViewModel(
        DataAccessor accessor,
        Settings settings,
        Session session)
    {
        this.accessor = accessor;
        this.settings = settings;
        this.session = session;

        SelectCommand = MakeAsyncCommand<StaffItem>(SelectAsync);
    }

    public override async Task OnNavigatedToAsync(INavigationContext context)
    {
        StoreText = session.Store is null ? string.Empty : $"🏪 {session.Store.Name} / {session.Terminal?.Name}";

        var list = settings.StoreId is null ? [] : await accessor.QueryStaffListAsync(settings.StoreId.Value);
        Items = list.Select(static x => new StaffItem(x, x.Name, DisplayText.Name(x.Role), ToColor(x.Role))).ToList();
    }

    private static Color ToColor(StaffRole role) => role switch
    {
        StaffRole.Manager => ManagerColor,
        StaffRole.Admin => AdminColor,
        _ => CashierColor
    };

    private async Task SelectAsync(StaffItem item)
    {
        session.Staff = item.Staff;

        // ログイン後に販売画面を開く設定 (screen-design §1.2)
        if (settings.OpenSalesAfterLogin && session.IsShiftOpen)
        {
            await Navigator.ForwardAsync(ViewId.Sales);
        }
        else
        {
            await Navigator.ForwardAsync(ViewId.Menu);
        }
    }

    protected override Task OnNotifyBackAsync()
    {
        AndroidHelper.MoveTaskToBack();
        return Task.CompletedTask;
    }

    protected override Task OnNotifyFunction1() =>
        Navigator.ForwardAsync(ViewId.Setting, Parameters.Make().WithReturnTo(ViewId.StaffSelect));
}
