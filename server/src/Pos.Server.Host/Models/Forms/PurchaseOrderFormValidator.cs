namespace Pos.Server.Host.Models.Forms;

using FluentValidation;

public sealed class PurchaseOrderFormValidator : FormValidator<PurchaseOrderForm>
{
    public PurchaseOrderFormValidator()
    {
        RuleFor(static x => x.StoreId).NotNull().WithMessage("店舗を選択してください。");
        RuleFor(static x => x.SupplierId).NotNull().WithMessage("仕入先を選択してください。");
        RuleFor(static x => x.Note).MaximumLength(Length.Note);
        RuleFor(static x => x.Lines).NotEmpty().WithMessage("明細を追加してください。");
        RuleForEach(static x => x.Lines).ChildRules(static line =>
        {
            line.RuleFor(static x => x.Product).NotNull().WithMessage("明細の商品を選択してください。");
            line.RuleFor(static x => x.Quantity).GreaterThan(0m).WithMessage("明細の数量は 0 より大きくしてください。");
            line.RuleFor(static x => x.Cost).GreaterThanOrEqualTo(0m).WithMessage("明細の仕入単価は 0 以上にしてください。");
        });
    }
}
