namespace Pos.Server.Host.Models.Forms;

using FluentValidation;

public sealed class PointAdjustFormValidator : FormValidator<PointAdjustForm>
{
    public PointAdjustFormValidator()
    {
        RuleFor(static x => x.Points).NotEqual(0).WithMessage("増減を入力してください。");
        RuleFor(static x => x.Reason).NotEmpty().WithMessage("理由を入力してください。").MaximumLength(200);
        RuleFor(static x => x.StaffId).NotNull().WithMessage("担当を選択してください。");
    }
}
