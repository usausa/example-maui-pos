namespace Pos.Server.Host.Mappers;

using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Entity;

using Smart.Mapper;

// Entity ↔ 管理画面のフォーム。サーバ付与項目 (Id / CreatedAt / UpdatedAt / Version) は呼び出し側で設定する
public static partial class FormMapper
{
    [Mapper]
    public static partial StoreForm ToStoreForm(StoreEntity entity);

    [Mapper]
    public static partial StoreEntity ToStoreEntity(StoreForm form);

    [Mapper]
    public static partial TerminalForm ToTerminalForm(TerminalEntity entity);

    [Mapper]
    [MapUsing(nameof(TerminalEntity.StoreId), nameof(ResolveStoreId))]
    public static partial TerminalEntity ToTerminalEntity(TerminalForm form);

    [Mapper]
    public static partial StaffForm ToStaffForm(StaffEntity entity);

    [Mapper]
    public static partial StaffEntity ToStaffEntity(StaffForm form);

    [Mapper]
    public static partial CategoryForm ToCategoryForm(CategoryEntity entity);

    [Mapper]
    public static partial CategoryEntity ToCategoryEntity(CategoryForm form);

    [Mapper]
    public static partial TaxRateForm ToTaxRateForm(TaxRateEntity entity);

    [Mapper]
    public static partial TaxRateEntity ToTaxRateEntity(TaxRateForm form);

    [Mapper]
    public static partial DiscountForm ToDiscountForm(DiscountEntity entity);

    [Mapper]
    public static partial DiscountEntity ToDiscountEntity(DiscountForm form);

    [Mapper]
    public static partial PaymentMethodForm ToPaymentMethodForm(PaymentMethodEntity entity);

    [Mapper]
    public static partial PaymentMethodEntity ToPaymentMethodEntity(PaymentMethodForm form);

    [Mapper]
    public static partial AdjustmentReasonForm ToAdjustmentReasonForm(AdjustmentReasonEntity entity);

    [Mapper]
    public static partial AdjustmentReasonEntity ToAdjustmentReasonEntity(AdjustmentReasonForm form);

    [Mapper]
    public static partial ProductForm ToProductForm(ProductEntity entity);

    [Mapper]
    [MapUsing(nameof(ProductEntity.CategoryId), nameof(ResolveCategoryId))]
    [MapUsing(nameof(ProductEntity.TaxRateId), nameof(ResolveTaxRateId))]
    public static partial ProductEntity ToProductEntity(ProductForm form);

    [Mapper]
    [MapUsing(nameof(CustomerForm.BirthDate), nameof(ToBirthDate))]
    public static partial CustomerForm ToCustomerForm(CustomerEntity entity);

    [Mapper]
    [MapUsing(nameof(CustomerEntity.BirthDate), nameof(ToBirthDateOnly))]
    public static partial CustomerEntity ToCustomerEntity(CustomerForm form);

    [Mapper]
    public static partial SettingsForm ToSettingsForm(SettingsEntity entity);

    private static Guid ResolveStoreId(TerminalForm form) => form.StoreId ?? Guid.Empty;

    private static Guid ResolveCategoryId(ProductForm form) => form.CategoryId ?? Guid.Empty;

    private static Guid ResolveTaxRateId(ProductForm form) => form.TaxRateId ?? Guid.Empty;

    private static DateTime? ToBirthDate(CustomerEntity entity) => entity.BirthDate?.ToDateTime(TimeOnly.MinValue);

    private static DateOnly? ToBirthDateOnly(CustomerForm form) => form.BirthDate is null ? null : DateOnly.FromDateTime(form.BirthDate.Value);
}
