namespace Pos.Server.Host.Models.Forms;

using FluentValidation;

public sealed class InventoryTransferFormValidator : FormValidator<InventoryTransferForm>
{
    public InventoryTransferFormValidator()
    {
        RuleFor(static x => x.FromStoreId).NotNull().WithMessage("出荷店を選択してください。");
        RuleFor(static x => x.ToStoreId).NotNull().WithMessage("入荷店を選択してください。");
        RuleFor(static x => x.ToStoreId).NotEqual(static x => x.FromStoreId).WithMessage("出荷店と入荷店を別の店舗にしてください。").When(static x => x.FromStoreId is not null);
        RuleFor(static x => x.Note).MaximumLength(Length.Note);
        RuleFor(static x => x.Lines).NotEmpty().WithMessage("明細を追加してください。");
        RuleForEach(static x => x.Lines).ChildRules(static line =>
        {
            line.RuleFor(static x => x.Product).NotNull().WithMessage("明細の商品を選択してください。");
            line.RuleFor(static x => x.Quantity).GreaterThan(0m).WithMessage("明細の数量は 0 より大きくしてください。");
        });
    }
}
