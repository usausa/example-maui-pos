namespace Pos.Server.Host.Models.Forms;

using FluentValidation;

public sealed class SettingsFormValidator : FormValidator<SettingsForm>
{
    public SettingsFormValidator()
    {
        RuleFor(static x => x.CompanyName).NotEmpty().WithMessage("会社名を入力してください。").MaximumLength(100);
        RuleFor(static x => x.Currency).NotEmpty().WithMessage("通貨を入力してください。").Length(3).WithMessage("通貨は 3 文字 (JPY など) で入力してください。");
        RuleFor(static x => x.BusinessDayStartTime)
            .NotEmpty().WithMessage("営業日切替時刻を入力してください。")
            .Must(static x => TimeOnly.TryParseExact(x, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)).WithMessage("HH:mm の形式で入力してください。");
    }
}
