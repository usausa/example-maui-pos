namespace Pos.Server.Host.Models.Forms;

using FluentValidation;

public sealed class PaymentMethodFormValidator : FormValidator<PaymentMethodForm>
{
    public PaymentMethodFormValidator()
    {
        RuleFor(static x => x.Code).NotEmpty().WithMessage("コードを入力してください。").MaximumLength(20);
        RuleFor(static x => x.Name).NotEmpty().WithMessage("名称を入力してください。").MaximumLength(50);
        RuleFor(static x => x.ShortName).MaximumLength(10).WithMessage("ボタン名は 10 文字までです。");
    }
}
