namespace Pos.Server.Host.Mappers;

using Pos.Server.Models.Entity;
using Pos.Shared.Categories;
using Pos.Shared.Customers;
using Pos.Shared.Discounts;
using Pos.Shared.Inventory;
using Pos.Shared.PaymentMethods;
using Pos.Shared.Products;
using Pos.Shared.Settings;
using Pos.Shared.Staff;
using Pos.Shared.Stores;
using Pos.Shared.TaxRates;
using Pos.Shared.Terminals;

using Smart.Mapper;

// Entity ↔ Request / Response (マスタ・顧客)。サーバ付与項目 (Id / CreatedAt / UpdatedAt / Version) は呼び出し側で設定する
public static partial class MasterMapper
{
    [Mapper]
    public static partial SettingsResponse ToSettingsResponse(SettingsEntity entity);

    [Mapper]
    public static partial StoreResponse ToStoreResponse(StoreEntity entity);

    [Mapper]
    public static partial StoreEntity ToStoreEntity(StoreCreateRequest request);

    [Mapper]
    public static partial TerminalResponse ToTerminalResponse(TerminalEntity entity);

    [Mapper]
    public static partial TerminalEntity ToTerminalEntity(TerminalCreateRequest request);

    [Mapper]
    public static partial StaffResponse ToStaffResponse(StaffEntity entity);

    [Mapper]
    public static partial StaffEntity ToStaffEntity(StaffCreateRequest request);

    [Mapper]
    public static partial CategoryResponse ToCategoryResponse(CategoryEntity entity);

    [Mapper]
    public static partial CategoryEntity ToCategoryEntity(CategoryCreateRequest request);

    [Mapper]
    public static partial TaxRateResponse ToTaxRateResponse(TaxRateEntity entity);

    [Mapper]
    public static partial TaxRateEntity ToTaxRateEntity(TaxRateCreateRequest request);

    [Mapper]
    public static partial ProductResponse ToProductResponse(ProductEntity entity);

    [Mapper]
    public static partial ProductEntity ToProductEntity(ProductCreateRequest request);

    [Mapper]
    public static partial DiscountResponse ToDiscountResponse(DiscountEntity entity);

    [Mapper]
    public static partial DiscountEntity ToDiscountEntity(DiscountCreateRequest request);

    [Mapper]
    public static partial PaymentMethodResponse ToPaymentMethodResponse(PaymentMethodEntity entity);

    [Mapper]
    public static partial PaymentMethodEntity ToPaymentMethodEntity(PaymentMethodCreateRequest request);

    [Mapper]
    public static partial AdjustmentReasonResponse ToAdjustmentReasonResponse(AdjustmentReasonEntity entity);

    [Mapper]
    public static partial AdjustmentReasonEntity ToAdjustmentReasonEntity(AdjustmentReasonCreateRequest request);

    [Mapper]
    public static partial CustomerResponse ToCustomerResponse(CustomerEntity entity);

    [Mapper]
    public static partial CustomerEntity ToCustomerEntity(CustomerCreateRequest request);

    [Mapper]
    public static partial PointHistoryResponse ToPointHistoryResponse(PointHistoryEntity entity);
}
