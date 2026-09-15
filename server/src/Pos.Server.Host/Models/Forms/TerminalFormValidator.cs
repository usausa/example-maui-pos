namespace Pos.Server.Host.Models.Forms;

using FluentValidation;

public sealed class TerminalFormValidator : FormValidator<TerminalForm>
{
    public TerminalFormValidator()
    {
        RuleFor(static x => x.StoreId).NotNull().WithMessage("店舗を選択してください。");
        RuleFor(static x => x.TerminalNo).InclusiveBetween(1, 99).WithMessage("端末番号は 1〜99 で入力してください。");
        RuleFor(static x => x.Name).NotEmpty().WithMessage("名称を入力してください。").MaximumLength(Length.TerminalName);
    }
}
