namespace Pos.Server.Host.Components.Pages;

using FluentValidation;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Entity;
using Pos.Server.Services;

using Smart.Mapper;

// 会社設定
public sealed partial class SettingsPage
{
    private static readonly SettingsFormValidator Validator = new();

    private MudForm Form { get; set; } = default!;

    private SettingsForm settings = new();

    [Inject]
    public required SettingsService SettingsService { get; set; }

    protected override Task OnInitializedAsync() => LoadAsync();

    private Task LoadAsync() =>
        LoadAsync(async () =>
        {
            var entity = await SettingsService.QueryAsync(CancellationToken);
            settings = entity is null ? new SettingsForm() : ToForm(entity);
        });

    private async Task SaveAsync()
    {
        await Form.ValidateAsync();
        if (!Form.IsValid)
        {
            return;
        }

        await RunAsync(async () => NotifyResult(await SettingsService.UpdateAsync(ToEntity(settings), CancellationToken), "保存しました。"), LoadAsync);
    }

    // Entity ↔ フォーム (サーバ付与項目はサービスが設定する)
    [Mapper]
    private static partial SettingsForm ToForm(SettingsEntity entity);

    [Mapper]
    private static partial SettingsEntity ToEntity(SettingsForm form);

    //--------------------------------------------------------------------------------
    // Form
    //--------------------------------------------------------------------------------

    // このページだけで使うフォーム
    private sealed class SettingsForm
    {
        public string CompanyName { get; set; } = string.Empty;

        public string Currency { get; set; } = "JPY";

        public TaxRounding TaxRounding { get; set; } = TaxRounding.Floor;

        public PointBasis PointBasis { get; set; } = PointBasis.TaxIncluded;

        // HH:mm
        public string BusinessDayStartTime { get; set; } = "05:00";

        public int Version { get; set; }
    }

    private sealed class SettingsFormValidator : FormValidator<SettingsForm>
    {
        public SettingsFormValidator()
        {
            RuleFor(static x => x.CompanyName).NotEmpty().WithMessage("会社名を入力してください。").MaximumLength(Length.CompanyName);
            RuleFor(static x => x.Currency).NotEmpty().WithMessage("通貨を入力してください。").Length(Length.Currency).WithMessage($"通貨は {Length.Currency} 文字 (JPY など) で入力してください。");
            RuleFor(static x => x.BusinessDayStartTime)
                .NotEmpty().WithMessage("営業日切替時刻を入力してください。")
                .Must(static x => TimeOnly.TryParseExact(x, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)).WithMessage("HH:mm の形式で入力してください。");
        }
    }
}
