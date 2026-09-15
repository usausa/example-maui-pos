namespace Pos.Server.Host.Models.Forms;

using FluentValidation;

public sealed class CategoryFormValidator : FormValidator<CategoryForm>
{
    public CategoryFormValidator()
    {
        RuleFor(static x => x.Code).NotEmpty().WithMessage("コードを入力してください。").MaximumLength(Length.Code);
        RuleFor(static x => x.Name).NotEmpty().WithMessage("名称を入力してください。").MaximumLength(Length.Name);
        RuleFor(static x => x.ParentId).Must(static (form, parentId) => parentId != form.Id).WithMessage("自分自身を親にはできません。");
    }
}
