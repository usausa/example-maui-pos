namespace Pos.Terminal.Modules.Inquiry;

using Pos.Contract.Customers;
using Pos.Terminal.Modules.Sales;

// スキャン画面へ行っている間の入力内容。会員編集とスキャンの ViewModel の [Scope] プロパティに Scope プラグインが注入する
public sealed class CustomerDraft
{
    public bool HasDraft { get; set; }

    public CustomerResponseItem? Original { get; set; }

    public ViewId ReturnTo { get; set; }

    public string? Code { get; set; }

    public string? Name { get; set; }

    public string? Kana { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? PostalCode { get; set; }

    public string? Address { get; set; }

    public string? BirthDate { get; set; }

    public string? Note { get; set; }

    public void Clear()
    {
        HasDraft = false;
        Original = null;
        Code = null;
        Name = null;
        Kana = null;
        Phone = null;
        Email = null;
        PostalCode = null;
        Address = null;
        BirthDate = null;
        Note = null;
    }
}

// 会員登録・編集 (オンライン限定)。保存後は呼び出し元へ会員を渡す (販売からならカートにも紐付ける)
public sealed partial class CustomerEditViewModel : AppViewModelBase
{
    private readonly IDialog dialog;

    private readonly IPopupNavigator popupNavigator;

    private CustomerResponseItem? original;

    private ViewId returnTo = ViewId.CustomerInquiry;

    private readonly NetworkService network;

    // 販売からの登録のときにカートの状態を保持する (照会からのときは使わない)
    [Scope]
    public SalesContext SalesContext { get; set; } = default!;

    // スキャン画面へ行っている間の入力内容 (スキャン画面も同じ名前のプロパティで保持する)
    [Scope]
    public CustomerDraft CustomerDraft { get; set; } = default!;

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
        NetworkService network)
    {
        this.dialog = dialog;
        this.popupNavigator = popupNavigator;
        this.network = network;

        InputPhoneCommand = MakeAsyncCommand(async () => PhoneText = await popupNavigator.InputPhoneAsync(PhoneText) ?? PhoneText);
        InputPostalCodeCommand = MakeAsyncCommand(async () => PostalCodeText = await popupNavigator.InputPostalCodeAsync(PostalCodeText) ?? PostalCodeText);
        InputBirthDateCommand = MakeAsyncCommand(InputBirthDateAsync);
    }

    // 生年月日は yyyyMMdd の 8 桁を電卓で入力し、表示の書式に整える
    private async Task InputBirthDateAsync()
    {
        var digits = new string((BirthDateText ?? string.Empty).Where(Char.IsAsciiDigit).ToArray());
        var text = await popupNavigator.InputBirthDateAsync(digits);
        if (text is null)
        {
            return;
        }

        BirthDateText = DateTimeHelper.TryParseCompactDate(text, out var date)
            ? ViewHelper.Date(date)
            : text.Length == 0 ? null : text;
    }

    public override Task OnNavigatedToAsync(INavigationContext context)
    {
        if (CustomerDraft.HasDraft)
        {
            original = CustomerDraft.Original;
            returnTo = CustomerDraft.ReturnTo;
            Code.Text = CustomerDraft.Code;
            Name.Text = CustomerDraft.Name;
            Kana.Text = CustomerDraft.Kana;
            PhoneText = CustomerDraft.Phone;
            Email.Text = CustomerDraft.Email;
            PostalCodeText = CustomerDraft.PostalCode;
            Address.Text = CustomerDraft.Address;
            BirthDateText = CustomerDraft.BirthDate;
            Note.Text = CustomerDraft.Note;
            CustomerDraft.Clear();
        }
        else
        {
            original = context.Parameter.GetCustomer();
            returnTo = context.Parameter.GetReturnTo(ViewId.CustomerInquiry);
            if (original is not null)
            {
                Code.Text = original.Code;
                Name.Text = original.Name;
                Kana.Text = original.Kana;
                PhoneText = original.Phone;
                Email.Text = original.Email;
                PostalCodeText = original.PostalCode;
                Address.Text = original.Address;
                BirthDateText = original.BirthDate is null ? null : ViewHelper.Date(original.BirthDate.Value);
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

    // スキャン画面へ行っている間の入力内容を残す
    private void SaveDraft()
    {
        CustomerDraft.HasDraft = true;
        CustomerDraft.Original = original;
        CustomerDraft.ReturnTo = returnTo;
        CustomerDraft.Code = Code.Text;
        CustomerDraft.Name = Name.Text;
        CustomerDraft.Kana = Kana.Text;
        CustomerDraft.Phone = PhoneText;
        CustomerDraft.Email = Email.Text;
        CustomerDraft.PostalCode = PostalCodeText;
        CustomerDraft.Address = Address.Text;
        CustomerDraft.BirthDate = BirthDateText;
        CustomerDraft.Note = Note.Text;
    }

    private Task<bool> ReturnAsync(CustomerResponseItem? customer) =>
        Navigator.ForwardAsync(returnTo, Parameters.Make().WithCustomer(customer));

    protected override Task OnNotifyBackAsync() => ReturnAsync(original);

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    protected override Task OnNotifyFunction2()
    {
        SaveDraft();
        return Navigator.ForwardAsync(ViewId.Scan, Parameters.Make().WithScan(ScanMode.Customer, ViewId.CustomerEdit));
    }

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
            if (!DateTimeHelper.TryParseDate(BirthDateText.Trim(), out var date))
            {
                await dialog.InformationAsync("生年月日は yyyy/MM/dd で入力してください。");
                return;
            }

            birthDate = date;
        }

        ApiResult<CustomerResponseItem> result;
        if (original is null)
        {
            result = await network.ExecuteAsync(h => h.PostCustomerAsync(new CustomerCreateRequest
            {
                Code = code,
                Name = name,
                Kana = Kana.Text.TrimToNull(),
                Phone = PhoneText.TrimToNull(),
                Email = Email.Text.TrimToNull(),
                PostalCode = PostalCodeText.TrimToNull(),
                Address = Address.Text.TrimToNull(),
                BirthDate = birthDate,
                Note = Note.Text.TrimToNull()
            }));
        }
        else
        {
            result = await network.ExecuteAsync(h => h.PutCustomerAsync(original.Id, new CustomerUpdateRequest
            {
                Code = code,
                Name = name,
                Kana = Kana.Text.TrimToNull(),
                Phone = PhoneText.TrimToNull(),
                Email = Email.Text.TrimToNull(),
                PostalCode = PostalCodeText.TrimToNull(),
                Address = Address.Text.TrimToNull(),
                BirthDate = birthDate,
                Note = Note.Text.TrimToNull(),
                Version = original.Version
            }));
        }

        if (!result.IsSuccess)
        {
            return;
        }

        var saved = result.Content!;
        if (returnTo != ViewId.CustomerInquiry)
        {
            SalesContext.Cart.Customer = saved;
        }

        await dialog.Toast(original is null ? "会員を登録しました。" : "会員を更新しました。");
        await ReturnAsync(saved);
    }
}
