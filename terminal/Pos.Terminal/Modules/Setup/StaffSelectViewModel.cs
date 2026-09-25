namespace Pos.Terminal.Modules.Setup;

// 役割の文言と色は画面側の Converter で付ける
public sealed record StaffItem(StaffResponseItem Staff, string Name, StaffRole Role, bool HasPin);

// スタッフ選択: 所属店舗のスタッフをタップし、PIN で本人を確かめて担当を決める (PIN 未設定のスタッフは選べない)
public sealed partial class StaffSelectViewModel : AppViewModelBase
{
    private readonly IDialog dialog;

    private readonly Settings settings;

    private readonly Session session;

    private readonly DataAccessor accessor;

    private readonly PinService pins;

    [ObservableProperty]
    public partial string StoreText { get; set; } = string.Empty;

    public ObservableCollection<StaffItem> Items { get; } = [];

    public IObserveCommand SelectCommand { get; }

    // 根の画面: 戻るはプラットフォームに任せる (タスクを背面へ)
    public override bool HandlesBack => false;

    public StaffSelectViewModel(
        IDialog dialog,
        Settings settings,
        Session session,
        DataAccessor accessor,
        PinService pins)
    {
        this.dialog = dialog;
        this.settings = settings;
        this.session = session;
        this.accessor = accessor;
        this.pins = pins;

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
        Items.Replace(list.Select(static x => new StaffItem(x, x.Name, x.Role, x.PinHash is not null)));
    }

    private async Task SelectAsync(StaffItem item)
    {
        if (!item.HasPin)
        {
            await dialog.InformationAsync($"{item.Name} は PIN が設定されていません。\n管理画面のスタッフで PIN を設定してください。");
            return;
        }

        // 3 回違えば担当の選択に戻る
        if (!await pins.VerifyAsync(item.Staff, $"{item.Name} の PIN"))
        {
            return;
        }

        session.Staff = item.Staff;

        // ログイン後に販売画面を開く設定
        await (settings.OpenSalesAfterLogin && session.IsShiftOpen
            ? Navigator.ForwardAsync(ViewId.Sales)
            : Navigator.ForwardAsync(ViewId.Menu));
    }

    protected override Task OnNotifyFunction1() =>
        Navigator.ForwardAsync(ViewId.Setting, Parameters.Make().WithReturnTo(ViewId.StaffSelect));
}
