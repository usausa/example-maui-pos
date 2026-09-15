namespace Pos.Server.Host.Models.Forms;

using FluentValidation;

public sealed class DiscountFormValidator : FormValidator<DiscountForm>
{
    public DiscountFormValidator()
    {
        RuleFor(static x => x.Code).NotEmpty().WithMessage("コードを入力してください。").MaximumLength(Length.Code);
        RuleFor(static x => x.Name).NotEmpty().WithMessage("名称を入力してください。").MaximumLength(Length.DiscountName);
        RuleFor(static x => x.Value).GreaterThanOrEqualTo(0m).WithMessage("値は 0 以上で入力してください。");
        RuleFor(static x => x.Value).LessThanOrEqualTo(1m).WithMessage("率は 0〜1 (0.05 = 5%) で入力してください。").When(static x => x.Type == DiscountType.Percent);
    }
}
