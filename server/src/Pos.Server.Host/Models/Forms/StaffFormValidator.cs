namespace Pos.Server.Host.Models.Forms;

using FluentValidation;

public sealed class StaffFormValidator : FormValidator<StaffForm>
{
    public StaffFormValidator()
    {
        RuleFor(static x => x.Code).NotEmpty().WithMessage("コードを入力してください。").MaximumLength(Length.Code);
        RuleFor(static x => x.Name).NotEmpty().WithMessage("名前を入力してください。").MaximumLength(Length.StaffName);
    }
}
