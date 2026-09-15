namespace Pos.Terminal.Modules.Setup;

// 役割の文言と色は画面側の Converter で付ける
public sealed record StaffItem(StaffResponseItem Staff, string Name, StaffRole Role);

// スタッフ選択: 所属店舗のスタッフをタップして担当を決める
public sealed partial class StaffSelectViewModel : AppViewModelBase
{
    private readonly Settings settings;

    private readonly Session session;

    private readonly DataAccessor accessor;

    [ObservableProperty]
    public partial string StoreText { get; set; } = string.Empty;

    public ObservableCollection<StaffItem> Items { get; } = [];

    public IObserveCommand SelectCommand { get; }

    // 根の画面: 戻るはプラットフォームに任せる (タスクを背面へ)
    public override bool HandlesBack => false;

    public StaffSelectViewModel(
        Settings settings,
        Session session,
        DataAccessor accessor)
    {
        this.settings = settings;
        this.session = session;
        this.accessor = accessor;

        SelectCommand = MakeAsyncCommand<StaffItem>(SelectAsync);
    }

    public override async Task OnNavigatedToAsync(INavigationContext context)
    {
        StoreText = session.Store is null ? string.Empty : $"🏪 {session.Store.Name} / {session.Terminal?.Name}";
        await Navigator.PostActionAsync(LoadAsync);
    }

    private async Task LoadAsync()
    {
        var list = session.StoreId is null ? [] : await accessor.QueryStaffListAsync(session.StoreId.Value);
        Items.Replace(list.Select(static x => new StaffItem(x, x.Name, x.Role)));
    }

    private Task SelectAsync(StaffItem item)
    {
        session.Staff = item.Staff;

        // ログイン後に販売画面を開く設定
        return settings.OpenSalesAfterLogin && session.IsShiftOpen
            ? Navigator.ForwardAsync(ViewId.Sales)
            : Navigator.ForwardAsync(ViewId.Menu);
    }

    protected override Task OnNotifyFunction1() =>
        Navigator.ForwardAsync(ViewId.Setting, Parameters.Make().WithReturnTo(ViewId.StaffSelect));
}
