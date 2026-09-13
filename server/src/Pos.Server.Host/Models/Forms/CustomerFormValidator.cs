namespace Pos.Server.Host.Models.Forms;

using FluentValidation;

public sealed class CustomerFormValidator : FormValidator<CustomerForm>
{
    public CustomerFormValidator()
    {
        RuleFor(static x => x.Code).NotEmpty().WithMessage("会員番号を入力してください。").MaximumLength(20);
        RuleFor(static x => x.Name).NotEmpty().WithMessage("名前を入力してください。").MaximumLength(100);
        RuleFor(static x => x.Kana).MaximumLength(100);
        RuleFor(static x => x.Phone).MaximumLength(20);
        RuleFor(static x => x.Email).MaximumLength(100).EmailAddress().WithMessage("メールアドレスの形式が正しくありません。").When(static x => !String.IsNullOrEmpty(x.Email));
        RuleFor(static x => x.PostalCode).MaximumLength(10);
        RuleFor(static x => x.Address).MaximumLength(200);
        RuleFor(static x => x.Note).MaximumLength(500);
    }
}
