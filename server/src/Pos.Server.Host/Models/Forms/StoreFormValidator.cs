namespace Pos.Server.Host.Models.Forms;

using FluentValidation;

public sealed class StoreFormValidator : FormValidator<StoreForm>
{
    public StoreFormValidator()
    {
        RuleFor(static x => x.Code).NotEmpty().WithMessage("コードを入力してください。").MaximumLength(Length.StoreCode);
        RuleFor(static x => x.Name).NotEmpty().WithMessage("名称を入力してください。").MaximumLength(Length.Name);
        RuleFor(static x => x.PostalCode).MaximumLength(Length.PostalCode);
        RuleFor(static x => x.Address).MaximumLength(Length.Address);
        RuleFor(static x => x.Phone).MaximumLength(Length.Phone);
        RuleFor(static x => x.RegistrationNo).MaximumLength(Length.RegistrationNo);
        RuleFor(static x => x.ReceiptHeader).MaximumLength(Length.ReceiptText);
        RuleFor(static x => x.ReceiptFooter).MaximumLength(Length.ReceiptText);
        RuleFor(static x => x.TimeZone)
            .NotEmpty().WithMessage("タイムゾーンを入力してください。")
            .Must(static x => TimeZoneInfo.TryFindSystemTimeZoneById(x, out _)).WithMessage("タイムゾーン ID が正しくありません。");
    }
}
