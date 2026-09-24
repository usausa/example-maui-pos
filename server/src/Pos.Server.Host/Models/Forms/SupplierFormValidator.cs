namespace Pos.Server.Host.Models.Forms;

using FluentValidation;

public sealed class SupplierFormValidator : FormValidator<SupplierForm>
{
    public SupplierFormValidator()
    {
        RuleFor(static x => x.Code).NotEmpty().WithMessage("コードを入力してください。").MaximumLength(Length.Code);
        RuleFor(static x => x.Name).NotEmpty().WithMessage("名称を入力してください。").MaximumLength(Length.SupplierName);
        RuleFor(static x => x.Phone).MaximumLength(Length.Phone);
        RuleFor(static x => x.Email).MaximumLength(Length.Email).EmailAddress().WithMessage("メールアドレスの形式が正しくありません。").When(static x => !String.IsNullOrEmpty(x.Email));
        RuleFor(static x => x.Note).MaximumLength(Length.Note);
    }
}
