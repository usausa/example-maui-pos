namespace Pos.Server.Host.Models.Forms;

using FluentValidation;

public sealed class TaxRateFormValidator : FormValidator<TaxRateForm>
{
    public TaxRateFormValidator()
    {
        RuleFor(static x => x.Code).NotEmpty().WithMessage("コードを入力してください。").MaximumLength(Length.TaxRateCode);
        RuleFor(static x => x.Name).NotEmpty().WithMessage("名称を入力してください。").MaximumLength(Length.TaxRateName);
        RuleFor(static x => x.Rate).InclusiveBetween(0m, 1m).WithMessage("税率は 0〜1 (0.10 = 10%) で入力してください。");
    }
}
