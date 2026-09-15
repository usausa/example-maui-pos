namespace Pos.Terminal.Modules.Sales;

using Pos.Terminal.Models.Cart;

// 配送先: 宛名・電話・郵便番号・住所・希望日・時間帯・備考
public sealed partial class DeliveryViewModel : AppViewModelBase
{
    private static readonly string[] TimeSlots = ["指定なし", "午前中", "12-14 時", "14-16 時", "16-18 時", "18-20 時", "19-21 時"];

    private readonly IDialog dialog;

    // 販売の画面間で共有する状態 (Scope プラグインが同じインスタンスを注入し、どの画面からも参照されなくなると破棄する)
    [Scope]
    public SalesContext SalesContext { get; set; } = default!;

    public EntryController RecipientName { get; } = new();

    [ObservableProperty]
    public partial string? PhoneText { get; set; }

    [ObservableProperty]
    public partial string? PostalCodeText { get; set; }

    public EntryController Address { get; } = new();

    public EntryController Note { get; } = new();

    [ObservableProperty]
    public partial bool HasCustomer { get; set; }

    [ObservableProperty]
    public partial bool HasRequestedDate { get; set; }

    [ObservableProperty]
    public partial DateTime RequestedDate { get; set; } = DateTime.Today;

    [ObservableProperty]
    public partial string TimeSlotText { get; set; } = TimeSlots[0];

    public IObserveCommand SetDateCommand { get; }

    public IObserveCommand ClearDateCommand { get; }

    public IObserveCommand SelectTimeSlotCommand { get; }

    public IObserveCommand InputPhoneCommand { get; }

    public IObserveCommand InputPostalCodeCommand { get; }

    public DeliveryViewModel(
        IDialog dialog,
        IPopupNavigator popupNavigator)
    {
        this.dialog = dialog;

        InputPhoneCommand = MakeAsyncCommand(async () => PhoneText = await popupNavigator.InputPhoneAsync(PhoneText) ?? PhoneText);
        InputPostalCodeCommand = MakeAsyncCommand(async () => PostalCodeText = await popupNavigator.InputPostalCodeAsync(PostalCodeText) ?? PostalCodeText);

        SetDateCommand = MakeDelegateCommand(() =>
        {
            RequestedDate = DateTime.Today.AddDays(1);
            HasRequestedDate = true;
        });
        ClearDateCommand = MakeDelegateCommand(() => HasRequestedDate = false);
        SelectTimeSlotCommand = MakeAsyncCommand(async () =>
        {
            var index = await dialog.ChooseAsync(TimeSlots, "時間帯", Array.IndexOf(TimeSlots, TimeSlotText));
            if (index >= 0)
            {
                TimeSlotText = TimeSlots[index];
            }
        });
    }

    public override Task OnNavigatedToAsync(INavigationContext context)
    {
        HasCustomer = SalesContext.Cart.Customer is not null;

        var delivery = SalesContext.Cart.Delivery;
        if (delivery is not null)
        {
            RecipientName.Text = delivery.RecipientName;
            PhoneText = delivery.Phone;
            PostalCodeText = delivery.PostalCode;
            Address.Text = delivery.Address;
            HasRequestedDate = delivery.RequestedDate is not null;
            RequestedDate = delivery.RequestedDate?.ToDateTime(TimeOnly.MinValue) ?? DateTime.Today;
            TimeSlotText = delivery.TimeSlot ?? TimeSlots[0];
            Note.Text = delivery.Note;
        }

        return Task.CompletedTask;
    }

    private Task<bool> ReturnAsync() => Navigator.ForwardAsync(ViewId.Sales);

    protected override Task OnNotifyBackAsync() => ReturnAsync();

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    // 会員の住所を転記
    protected override Task OnNotifyFunction2()
    {
        var customer = SalesContext.Cart.Customer;
        if (customer is not null)
        {
            RecipientName.Text = customer.Name;
            PhoneText = customer.Phone;
            PostalCodeText = customer.PostalCode;
            Address.Text = customer.Address;
        }

        return Task.CompletedTask;
    }

    protected override async Task OnNotifyFunction3()
    {
        if (await dialog.AskAsync("配送先を解除しますか？", null, "解除"))
        {
            SalesContext.Cart.Delivery = null;
            await ReturnAsync();
        }
    }

    protected override async Task OnNotifyFunction4()
    {
        if (String.IsNullOrWhiteSpace(RecipientName.Text))
        {
            await dialog.InformationAsync("宛名を入力してください。");
            RecipientName.Focus();
            return;
        }

        if (String.IsNullOrWhiteSpace(Address.Text))
        {
            await dialog.InformationAsync("住所を入力してください。");
            Address.Focus();
            return;
        }

        SalesContext.Cart.Delivery = new CartDelivery
        {
            RecipientName = RecipientName.Text.Trim(),
            Phone = PhoneText.TrimToNull(),
            PostalCode = PostalCodeText.TrimToNull(),
            Address = Address.Text.Trim(),
            RequestedDate = HasRequestedDate ? DateOnly.FromDateTime(RequestedDate) : null,
            TimeSlot = TimeSlotText == TimeSlots[0] ? null : TimeSlotText,
            Note = Note.Text.TrimToNull()
        };
        await ReturnAsync();
    }
}
