namespace Pos.Server.Host.Models.Forms;

using FluentValidation;

public sealed class OrderFormValidator : FormValidator<OrderForm>
{
    public OrderFormValidator()
    {
        RuleFor(static x => x.StoreId).NotNull().WithMessage("店舗を選択してください。");
        RuleFor(static x => x.StaffId).NotNull().WithMessage("担当を選択してください。");
        RuleFor(static x => x.CustomerName).NotEmpty().WithMessage("会員か宛名を指定してください。").When(static x => x.Customer is null);
        RuleFor(static x => x.CustomerName).MaximumLength(Length.Name);
        RuleFor(static x => x.Phone).MaximumLength(Length.Phone);
        RuleFor(static x => x.Note).MaximumLength(Length.Note);
        RuleFor(static x => x.Lines).NotEmpty().WithMessage("明細を追加してください。");
        RuleForEach(static x => x.Lines).ChildRules(static line =>
        {
            line.RuleFor(static x => x.Product).NotNull().WithMessage("明細の商品を選択してください。");
            line.RuleFor(static x => x.Quantity).GreaterThan(0m).WithMessage("明細の数量は 0 より大きくしてください。");
            line.RuleFor(static x => x.UnitPrice).GreaterThanOrEqualTo(0m).WithMessage("明細の単価は 0 以上にしてください。");
            line.RuleFor(static x => x.Note).MaximumLength(Length.LineNote);
        });
    }
}
