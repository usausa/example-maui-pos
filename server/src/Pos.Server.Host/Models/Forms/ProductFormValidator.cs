namespace Pos.Server.Host.Models.Forms;

using FluentValidation;

public sealed class ProductFormValidator : FormValidator<ProductForm>
{
    public ProductFormValidator()
    {
        RuleFor(static x => x.Code).NotEmpty().WithMessage("コードを入力してください。").MaximumLength(20);
        RuleFor(static x => x.Barcode).MaximumLength(20);
        RuleFor(static x => x.Name).NotEmpty().WithMessage("商品名を入力してください。").MaximumLength(100);
        RuleFor(static x => x.Kana).MaximumLength(100);
        RuleFor(static x => x.Brand).MaximumLength(50);
        RuleFor(static x => x.ModelNo).MaximumLength(50);
        RuleFor(static x => x.CategoryId).NotNull().WithMessage("部門を選択してください。");
        RuleFor(static x => x.Price).GreaterThanOrEqualTo(0m).WithMessage("価格は 0 以上で入力してください。");
        RuleFor(static x => x.TaxRateId).NotNull().WithMessage("税率を選択してください。");
        RuleFor(static x => x.Cost).GreaterThanOrEqualTo(0m).WithMessage("原価は 0 以上で入力してください。").When(static x => x.Cost is not null);
        RuleFor(static x => x.PointRate).InclusiveBetween(0m, 1m).WithMessage("還元率は 0〜1 (0.01 = 1%) で入力してください。");
        RuleFor(static x => x.Unit).MaximumLength(10);
    }
}
