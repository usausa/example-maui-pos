namespace Pos.Terminal.Modules.Shift;

using Pos.Shared.Shifts;
using Pos.Terminal.Models.Entity;

using Smart.Data;

// T-50 入出金: 種別・金額・理由を Outbox 経由でサーバへ送る
public sealed partial class CashEventViewModel : AppViewModelBase
{
    private readonly IDialog dialog;

    private readonly IPopupNavigator popupNavigator;

    private readonly IDbProvider provider;

    private readonly DataAccessor accessor;

    private readonly Session session;

    private readonly SyncWorker syncWorker;

    private CashEventType type = CashEventType.PaidIn;

    private decimal amount;

    [ObservableProperty]
    public partial bool IsPaidIn { get; set; } = true;

    [ObservableProperty]
    public partial bool IsPaidOut { get; set; }

    [ObservableProperty]
    public partial bool IsNoSale { get; set; }

    [ObservableProperty]
    public partial bool AmountEnabled { get; set; } = true;

    [ObservableProperty]
    public partial string AmountText { get; set; } = DisplayText.Yen(0);

    public EntryController Reason { get; } = new();

    public IObserveCommand SelectTypeCommand { get; }

    public IObserveCommand InputAmountCommand { get; }

    public CashEventViewModel(
        IDialog dialog,
        IPopupNavigator popupNavigator,
        IDbProvider provider,
        DataAccessor accessor,
        Session session,
        SyncWorker syncWorker)
    {
        this.dialog = dialog;
        this.popupNavigator = popupNavigator;
        this.provider = provider;
        this.accessor = accessor;
        this.session = session;
        this.syncWorker = syncWorker;

        SelectTypeCommand = MakeDelegateCommand<string>(SelectType);
        InputAmountCommand = MakeAsyncCommand(InputAmountAsync);
    }

    private void SelectType(string value)
    {
        type = Enum.Parse<CashEventType>(value);
        IsPaidIn = type == CashEventType.PaidIn;
        IsPaidOut = type == CashEventType.PaidOut;
        IsNoSale = type == CashEventType.NoSale;
        AmountEnabled = type != CashEventType.NoSale;
        if (type == CashEventType.NoSale)
        {
            amount = 0;
            AmountText = DisplayText.Yen(0);
        }
    }

    private async Task InputAmountAsync()
    {
        var text = await popupNavigator.InputNumberAsync("金額", amount.ToString("0", CultureInfo.InvariantCulture), 8);
        if ((text is not null) && Decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            amount = value;
            AmountText = DisplayText.Yen(value);
        }
    }

    protected override Task OnNotifyBackAsync() => Navigator.ForwardAsync(ViewId.Menu);

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    protected override async Task OnNotifyFunction4()
    {
        var shift = session.CurrentShift;
        if ((shift is null) || (session.Staff is null))
        {
            return;
        }

        if ((type != CashEventType.NoSale) && (amount <= 0))
        {
            await dialog.InformationAsync("金額を入力してください。");
            return;
        }

        if (!await dialog.AskAsync($"{DisplayText.Name(type)} {DisplayText.Yen(amount)} を登録しますか？", null, "確定"))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var entity = new LocalCashEventEntity
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            Type = type,
            Amount = amount,
            Reason = String.IsNullOrWhiteSpace(Reason.Text) ? null : Reason.Text.Trim(),
            StaffId = session.Staff.Id,
            OccurredAt = now
        };
        var request = new CashEventRequest
        {
            Id = entity.Id,
            Type = entity.Type,
            Amount = entity.Amount,
            Reason = entity.Reason,
            StaffId = entity.StaffId,
            OccurredAt = entity.OccurredAt
        };

        await provider.UsingTxAsync(async (_, tx) =>
        {
            await accessor.InsertCashEventAsync(tx, entity);
            await accessor.InsertOutboxAsync(tx, SyncWorker.CreateEntry(OutboxKind.CashEvent, shift.Id, request, now));
            await tx.CommitAsync();
        });

        await syncWorker.UpdateCountsAsync();
        syncWorker.Trigger();

        await dialog.Toast($"{DisplayText.Name(type)}を登録しました。");
        await Navigator.ForwardAsync(ViewId.Menu);
    }
}
