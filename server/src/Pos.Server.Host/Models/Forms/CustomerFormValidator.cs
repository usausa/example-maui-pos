namespace Pos.Server.Host.Models.Forms;

using FluentValidation;

public sealed class CustomerFormValidator : FormValidator<CustomerForm>
{
    public CustomerFormValidator()
    {
        RuleFor(static x => x.Code).NotEmpty().WithMessage("会員番号を入力してください。").MaximumLength(Length.Code);
        RuleFor(static x => x.Name).NotEmpty().WithMessage("名前を入力してください。").MaximumLength(Length.Name);
        RuleFor(static x => x.Kana).MaximumLength(Length.Kana);
        RuleFor(static x => x.Phone).MaximumLength(Length.Phone);
        RuleFor(static x => x.Email).MaximumLength(Length.Email).EmailAddress().WithMessage("メールアドレスの形式が正しくありません。").When(static x => !String.IsNullOrEmpty(x.Email));
        RuleFor(static x => x.PostalCode).MaximumLength(Length.PostalCode);
        RuleFor(static x => x.Address).MaximumLength(Length.Address);
        RuleFor(static x => x.Note).MaximumLength(Length.Note);
    }
}
