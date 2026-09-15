namespace Pos.Server.Host.Models.Forms;

using FluentValidation;

public sealed class PaymentMethodFormValidator : FormValidator<PaymentMethodForm>
{
    public PaymentMethodFormValidator()
    {
        RuleFor(static x => x.Code).NotEmpty().WithMessage("コードを入力してください。").MaximumLength(Length.Code);
        RuleFor(static x => x.Name).NotEmpty().WithMessage("名称を入力してください。").MaximumLength(Length.PaymentMethodName);
        RuleFor(static x => x.ShortName).MaximumLength(Length.PaymentMethodShortName).WithMessage($"ボタン名は {Length.PaymentMethodShortName} 文字までです。");
    }
}
