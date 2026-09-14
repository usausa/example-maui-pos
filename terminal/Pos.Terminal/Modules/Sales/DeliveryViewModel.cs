namespace Pos.Terminal.Modules.Sales;

using Pos.Terminal.Models.Sales;

// T-16 配送先: 宛名・電話・郵便番号・住所・希望日・時間帯・備考
public sealed partial class DeliveryViewModel : AppViewModelBase
{
    private static readonly string[] TimeSlots = ["指定なし", "午前中", "12-14 時", "14-16 時", "16-18 時", "18-20 時", "19-21 時"];

    private readonly IDialog dialog;

    private readonly SalesState sales;

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
        IPopupNavigator popupNavigator,
        SalesState sales)
    {
        this.dialog = dialog;
        this.sales = sales;

        InputPhoneCommand = MakeAsyncCommand(async () => PhoneText = await popupNavigator.InputDigitsAsync("電話番号", PhoneText, 13) ?? PhoneText);
        InputPostalCodeCommand = MakeAsyncCommand(async () => PostalCodeText = await popupNavigator.InputDigitsAsync("郵便番号", PostalCodeText, 7) ?? PostalCodeText);

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
        HasCustomer = sales.Cart.Customer is not null;

        var delivery = sales.Cart.Delivery;
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

    protected override Task OnNotifyBackAsync() => Navigator.ForwardAsync(ViewId.Sales);

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    // 会員の住所を転記
    protected override Task OnNotifyFunction2()
    {
        var customer = sales.Cart.Customer;
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
            sales.Cart.Delivery = null;
            await Navigator.ForwardAsync(ViewId.Sales);
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

        sales.Cart.Delivery = new CartDelivery
        {
            RecipientName = RecipientName.Text.Trim(),
            Phone = Trim(PhoneText),
            PostalCode = Trim(PostalCodeText),
            Address = Address.Text.Trim(),
            RequestedDate = HasRequestedDate ? DateOnly.FromDateTime(RequestedDate) : null,
            TimeSlot = TimeSlotText == TimeSlots[0] ? null : TimeSlotText,
            Note = Trim(Note.Text)
        };
        await Navigator.ForwardAsync(ViewId.Sales);
    }

    private static string? Trim(string? value) => String.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
