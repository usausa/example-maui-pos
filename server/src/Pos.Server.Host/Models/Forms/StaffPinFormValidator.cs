namespace Pos.Server.Host.Models.Forms;

using FluentValidation;

using Pos.Domain.Logic;

public sealed class StaffPinFormValidator : FormValidator<StaffPinForm>
{
    public StaffPinFormValidator()
    {
        RuleFor(static x => x.Pin).Must(PinHasher.IsValidFormat).WithMessage($"PIN は {Length.PinMinDigits}〜{Length.PinDigits} 桁の数字にしてください。");
        RuleFor(static x => x.PinConfirm).Equal(static x => x.Pin).WithMessage("PIN が一致しません。");
    }
}
