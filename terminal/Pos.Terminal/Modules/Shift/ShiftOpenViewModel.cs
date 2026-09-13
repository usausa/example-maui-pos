namespace Pos.Terminal.Modules.Shift;

using Pos.Shared.Shifts;
using Pos.Terminal.Models.Entity;

using Smart.Data;

// T-03 レジ開設: 営業日・釣銭準備金・担当でシフトを開き、Outbox 経由でサーバへ送る
public sealed partial class ShiftOpenViewModel : AppViewModelBase
{
    private readonly IDialog dialog;

    private readonly IPopupNavigator popupNavigator;

    private readonly IDbProvider provider;

    private readonly DataAccessor accessor;

    private readonly Settings settings;

    private readonly Session session;

    private readonly NetworkOperator network;

    private readonly SyncWorker syncWorker;

    private ViewId returnTo = ViewId.Menu;

    private decimal openingCash;

    [ObservableProperty]
    public partial string BusinessDateText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StaffName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OpeningCashText { get; set; } = DisplayText.Yen(0);

    public IObserveCommand InputCashCommand { get; }

    public ShiftOpenViewModel(
        IDialog dialog,
        IPopupNavigator popupNavigator,
        IDbProvider provider,
        DataAccessor accessor,
        Settings settings,
        Session session,
        NetworkOperator network,
        SyncWorker syncWorker)
    {
        this.dialog = dialog;
        this.popupNavigator = popupNavigator;
        this.provider = provider;
        this.accessor = accessor;
        this.settings = settings;
        this.session = session;
        this.network = network;
        this.syncWorker = syncWorker;

        InputCashCommand = MakeAsyncCommand(InputCashAsync);
    }

    public override async Task OnNavigatedToAsync(INavigationContext context)
    {
        returnTo = context.Parameter.GetReturnTo(ViewId.Menu);
        BusinessDateText = DisplayText.Date(session.BusinessDate);
        StaffName = session.Staff?.Name ?? string.Empty;

        // サーバに開設中のシフトが残っていれば引き継ぐ (再インストール時など)
        if ((settings.TerminalId is not null) && network.IsConnected)
        {
            var result = await network.ExecuteAsync(h => h.GetCurrentShiftAsync(settings.TerminalId.Value), notify: false);
            if (result is { IsSuccess: true, Content: { Status: ShiftStatus.Open } shift })
            {
                await AdoptAsync(shift);
                await dialog.InformationAsync("サーバに開設中のシフトがあるため引き継ぎました。");
                await Navigator.ForwardAsync(returnTo);
            }
        }
    }

    private async ValueTask AdoptAsync(ShiftResponse shift)
    {
        var entity = new LocalShiftEntity
        {
            Id = shift.Id,
            StoreId = shift.StoreId,
            TerminalId = shift.TerminalId,
            Status = shift.Status,
            BusinessDate = shift.BusinessDate,
            OpenedAt = shift.OpenedAt,
            OpenedByStaffId = shift.OpenedByStaffId,
            OpeningCash = shift.OpeningCash,
            Note = shift.Note
        };
        await provider.UsingTxAsync(async (_, tx) =>
        {
            await accessor.InsertShiftAsync(tx, entity);
            await tx.CommitAsync();
        });
        session.CurrentShift = entity;
    }

    private async Task InputCashAsync()
    {
        var text = await popupNavigator.InputNumberAsync("釣銭準備金", openingCash.ToString("0", CultureInfo.InvariantCulture), 8);
        if ((text is not null) && Decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            openingCash = value;
            OpeningCashText = DisplayText.Yen(value);
        }
    }

    protected override Task OnNotifyBackAsync() => Navigator.ForwardAsync(ViewId.Menu);

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    protected override async Task OnNotifyFunction4()
    {
        if ((settings.StoreId is null) || (settings.TerminalId is null) || (session.Staff is null))
        {
            return;
        }

        if (!await dialog.AskAsync($"釣銭準備金 {DisplayText.Yen(openingCash)} でレジを開設しますか？", null, "開設"))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var entity = new LocalShiftEntity
        {
            Id = Guid.NewGuid(),
            StoreId = settings.StoreId.Value,
            TerminalId = settings.TerminalId.Value,
            Status = ShiftStatus.Open,
            BusinessDate = session.BusinessDate,
            OpenedAt = now,
            OpenedByStaffId = session.Staff.Id,
            OpeningCash = openingCash
        };
        var request = new ShiftOpenRequest
        {
            Id = entity.Id,
            StoreId = entity.StoreId,
            TerminalId = entity.TerminalId,
            BusinessDate = entity.BusinessDate,
            OpenedAt = entity.OpenedAt,
            OpenedByStaffId = entity.OpenedByStaffId,
            OpeningCash = entity.OpeningCash
        };

        await provider.UsingTxAsync(async (_, tx) =>
        {
            await accessor.InsertShiftAsync(tx, entity);
            await accessor.InsertOutboxAsync(tx, SyncWorker.CreateEntry(OutboxKind.ShiftOpen, entity.Id, request, now));
            await tx.CommitAsync();
        });

        session.CurrentShift = entity;
        await syncWorker.UpdateCountsAsync();
        syncWorker.Trigger();

        await Navigator.ForwardAsync(returnTo);
    }
}
