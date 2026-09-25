namespace Pos.Server.Host.Models.Forms;

using FluentValidation;

public sealed class AccountPasswordFormValidator : FormValidator<AccountPasswordForm>
{
    public AccountPasswordFormValidator()
    {
        RuleFor(static x => x.Password)
            .NotEmpty().WithMessage("パスワードを入力してください。")
            .MinimumLength(Length.PasswordMin).WithMessage($"パスワードは {Length.PasswordMin} 文字以上にしてください。")
            .MaximumLength(Length.Password);
        RuleFor(static x => x.PasswordConfirm).Equal(static x => x.Password).WithMessage("パスワードが一致しません。");
    }
}
