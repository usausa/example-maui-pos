namespace Pos.Terminal.Modules.Inquiry;

using Pos.Shared.Customers;

// スキャン画面へ行っている間の入力内容
public sealed class CustomerDraft
{
    public CustomerResponse? Original { get; set; }

    public ViewId ReturnTo { get; set; }

    public bool ApplyToCart { get; set; }

    public string? Code { get; set; }

    public string? Name { get; set; }

    public string? Kana { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? PostalCode { get; set; }

    public string? Address { get; set; }

    public string? BirthDate { get; set; }

    public string? Note { get; set; }
}

// T-62 会員登録・編集 (オンライン限定)。保存後は呼び出し元へ会員を渡す
public sealed partial class CustomerEditViewModel : AppViewModelBase
{
    private readonly IDialog dialog;

    private readonly IPopupNavigator popupNavigator;

    private readonly NetworkOperator network;

    private readonly SalesState sales;

    private CustomerResponse? original;

    private ViewId returnTo = ViewId.CustomerInquiry;

    private bool applyToCart;

    [ObservableProperty]
    public partial string Title { get; set; } = "会員登録";

    public EntryController Code { get; } = new();

    public EntryController Name { get; } = new();

    public EntryController Kana { get; } = new();

    [ObservableProperty]
    public partial string? PhoneText { get; set; }

    public EntryController Email { get; } = new();

    [ObservableProperty]
    public partial string? PostalCodeText { get; set; }

    public EntryController Address { get; } = new();

    [ObservableProperty]
    public partial string? BirthDateText { get; set; }

    public EntryController Note { get; } = new();

    public IObserveCommand InputPhoneCommand { get; }

    public IObserveCommand InputPostalCodeCommand { get; }

    public IObserveCommand InputBirthDateCommand { get; }

    public CustomerEditViewModel(
        IDialog dialog,
        IPopupNavigator popupNavigator,
        NetworkOperator network,
        SalesState sales)
    {
        this.dialog = dialog;
        this.popupNavigator = popupNavigator;
        this.network = network;
        this.sales = sales;

        InputPhoneCommand = MakeAsyncCommand(async () => PhoneText = await popupNavigator.InputDigitsAsync("電話番号", PhoneText, 13) ?? PhoneText);
        InputPostalCodeCommand = MakeAsyncCommand(async () => PostalCodeText = await popupNavigator.InputDigitsAsync("郵便番号", PostalCodeText, 7) ?? PostalCodeText);
        InputBirthDateCommand = MakeAsyncCommand(InputBirthDateAsync);
    }

    // 生年月日は yyyyMMdd の 8 桁を電卓で入力し、yyyy/MM/dd に整える
    private async Task InputBirthDateAsync()
    {
        var digits = new string((BirthDateText ?? string.Empty).Where(Char.IsAsciiDigit).ToArray());
        var text = await popupNavigator.InputDigitsAsync("生年月日 (yyyyMMdd)", digits, 8);
        if (text is null)
        {
            return;
        }

        BirthDateText = DateOnly.TryParseExact(text, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? DisplayText.Date(date)
            : text.Length == 0 ? null : text;
    }

    public override Task OnNavigatedToAsync(INavigationContext context)
    {
        var draft = context.Parameter.GetState<CustomerDraft>();
        if (draft is not null)
        {
            original = draft.Original;
            returnTo = draft.ReturnTo;
            applyToCart = draft.ApplyToCart;
            Code.Text = draft.Code;
            Name.Text = draft.Name;
            Kana.Text = draft.Kana;
            PhoneText = draft.Phone;
            Email.Text = draft.Email;
            PostalCodeText = draft.PostalCode;
            Address.Text = draft.Address;
            BirthDateText = draft.BirthDate;
            Note.Text = draft.Note;
        }
        else
        {
            original = context.Parameter.GetCustomer();
            returnTo = context.Parameter.GetReturnTo(ViewId.CustomerInquiry);
            applyToCart = context.Parameter.GetApplyToCart();
            if (original is not null)
            {
                Code.Text = original.Code;
                Name.Text = original.Name;
                Kana.Text = original.Kana;
                PhoneText = original.Phone;
                Email.Text = original.Email;
                PostalCodeText = original.PostalCode;
                Address.Text = original.Address;
                BirthDateText = original.BirthDate is null ? null : DisplayText.Date(original.BirthDate.Value);
                Note.Text = original.Note;
            }
        }

        Title = original is null ? "会員登録" : "会員編集";

        var scanned = context.Parameter.GetScanResult();
        if (scanned is not null)
        {
            Code.Text = scanned;
        }

        (original is null ? Code : Name).Focus();
        return Task.CompletedTask;
    }

    private CustomerDraft ToDraft() => new()
    {
        Original = original,
        ReturnTo = returnTo,
        ApplyToCart = applyToCart,
        Code = Code.Text,
        Name = Name.Text,
        Kana = Kana.Text,
        Phone = PhoneText,
        Email = Email.Text,
        PostalCode = PostalCodeText,
        Address = Address.Text,
        BirthDate = BirthDateText,
        Note = Note.Text
    };

    private Task<bool> ReturnAsync(CustomerResponse? customer) =>
        Navigator.ForwardAsync(returnTo, Parameters.Make().WithCustomer(customer));

    protected override Task OnNotifyBackAsync() => ReturnAsync(original);

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    protected override Task OnNotifyFunction2() =>
        Navigator.ForwardAsync(ViewId.Scan, Parameters.Make().WithScan(ScanMode.Customer, ViewId.CustomerEdit).WithState(ToDraft()));

    protected override Task OnNotifyFunction3()
    {
        foreach (var entry in new[] { Code, Name, Kana, Email, Address, Note })
        {
            entry.Text = null;
        }

        PhoneText = null;
        PostalCodeText = null;
        BirthDateText = null;

        Code.Focus();
        return Task.CompletedTask;
    }

    protected override async Task OnNotifyFunction4()
    {
        var code = Code.Text?.Trim();
        var name = Name.Text?.Trim();
        if (String.IsNullOrEmpty(code))
        {
            await dialog.InformationAsync("会員番号を入力してください。");
            Code.Focus();
            return;
        }

        if (String.IsNullOrEmpty(name))
        {
            await dialog.InformationAsync("氏名を入力してください。");
            Name.Focus();
            return;
        }

        DateOnly? birthDate = null;
        if (!String.IsNullOrWhiteSpace(BirthDateText))
        {
            if (!DateOnly.TryParseExact(BirthDateText.Trim(), ["yyyy/MM/dd", "yyyy-MM-dd", "yyyyMMdd"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                await dialog.InformationAsync("生年月日は yyyy/MM/dd で入力してください。");
                return;
            }

            birthDate = date;
        }

        ApiResult<CustomerResponse> result;
        if (original is null)
        {
            result = await network.ExecuteAsync(h => h.PostCustomerAsync(new CustomerCreateRequest
            {
                Code = code,
                Name = name,
                Kana = Trim(Kana.Text),
                Phone = Trim(PhoneText),
                Email = Trim(Email.Text),
                PostalCode = Trim(PostalCodeText),
                Address = Trim(Address.Text),
                BirthDate = birthDate,
                Note = Trim(Note.Text)
            }));
        }
        else
        {
            result = await network.ExecuteAsync(h => h.PutCustomerAsync(original.Id, new CustomerUpdateRequest
            {
                Code = code,
                Name = name,
                Kana = Trim(Kana.Text),
                Phone = Trim(PhoneText),
                Email = Trim(Email.Text),
                PostalCode = Trim(PostalCodeText),
                Address = Trim(Address.Text),
                BirthDate = birthDate,
                Note = Trim(Note.Text),
                Version = original.Version
            }));
        }

        if (!result.IsSuccess)
        {
            return;
        }

        var saved = result.Content!;
        if (applyToCart)
        {
            sales.Cart.Customer = saved;
        }

        await dialog.Toast(original is null ? "会員を登録しました。" : "会員を更新しました。");
        await ReturnAsync(saved);
    }

    private static string? Trim(string? value) => String.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
