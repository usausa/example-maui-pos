namespace Pos.Server.Host.Models.Forms;

using FluentValidation;

public sealed class InventoryChangeFormValidator : FormValidator<InventoryChangeForm>
{
    public InventoryChangeFormValidator()
    {
        RuleFor(static x => x.StoreId).NotNull().WithMessage("店舗を選択してください。");
        RuleFor(static x => x.Product).NotNull().WithMessage("商品を選択してください。");
        RuleFor(static x => x.Type).Must(static x => x.IsManual()).WithMessage("種別は棚卸か調整を選択してください。");
        RuleFor(static x => x.Quantity).GreaterThanOrEqualTo(0m).WithMessage("棚卸の数量は 0 以上で入力してください。").When(static x => x.Type == InventoryChangeType.PhysicalCount);
        RuleFor(static x => x.Quantity).NotEqual(0m).WithMessage("増減を入力してください。").When(static x => x.Type == InventoryChangeType.Adjustment);
        RuleFor(static x => x.Reason).MaximumLength(Length.Reason);
        RuleFor(static x => x.StaffId).NotNull().WithMessage("担当を選択してください。");
    }
}
