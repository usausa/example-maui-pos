namespace Pos.Server.Host.Models.Forms;

using FluentValidation;

public sealed class AdjustmentReasonFormValidator : FormValidator<AdjustmentReasonForm>
{
    public AdjustmentReasonFormValidator()
    {
        RuleFor(static x => x.Code).NotEmpty().WithMessage("コードを入力してください。").MaximumLength(Length.Code);
        RuleFor(static x => x.Name).NotEmpty().WithMessage("名称を入力してください。").MaximumLength(Length.AdjustmentReasonName);
    }
}
