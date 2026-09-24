namespace Pos.Server.Host.Models.Forms;

using FluentValidation;

// 届いた数は入力欄の下限 (0) で止める
public sealed class InventoryMovementFormValidator : FormValidator<InventoryMovementForm>
{
    public InventoryMovementFormValidator()
    {
        RuleFor(static x => x.StaffId).NotNull().WithMessage("担当を選択してください。");
    }
}
