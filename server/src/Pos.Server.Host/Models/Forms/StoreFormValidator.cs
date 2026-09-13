namespace Pos.Server.Host.Models.Forms;

using FluentValidation;

public sealed class StoreFormValidator : FormValidator<StoreForm>
{
    public StoreFormValidator()
    {
        RuleFor(static x => x.Code).NotEmpty().WithMessage("コードを入力してください。").MaximumLength(20);
        RuleFor(static x => x.Name).NotEmpty().WithMessage("名称を入力してください。").MaximumLength(100);
        RuleFor(static x => x.PostalCode).MaximumLength(10);
        RuleFor(static x => x.Address).MaximumLength(200);
        RuleFor(static x => x.Phone).MaximumLength(20);
        RuleFor(static x => x.RegistrationNo).MaximumLength(20);
        RuleFor(static x => x.ReceiptHeader).MaximumLength(200);
        RuleFor(static x => x.ReceiptFooter).MaximumLength(200);
        RuleFor(static x => x.TimeZone)
            .NotEmpty().WithMessage("タイムゾーンを入力してください。")
            .Must(static x => TimeZoneInfo.TryFindSystemTimeZoneById(x, out _)).WithMessage("タイムゾーン ID が正しくありません。");
    }
}
