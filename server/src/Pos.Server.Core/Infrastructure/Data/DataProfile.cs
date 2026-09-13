namespace Pos.Server.Infrastructure.Data;

// Accessor 共通の型変換 ([ExecuteConfig(typeof(DataProfile))] で参照する)
[AccessorProfile]
[TypeHandler(typeof(EnumTextConverter<TransactionType>))]
[TypeHandler(typeof(EnumTextConverter<TransactionStatus>))]
[TypeHandler(typeof(EnumTextConverter<ProductKind>))]
[TypeHandler(typeof(EnumTextConverter<PaymentKind>))]
[TypeHandler(typeof(EnumTextConverter<DiscountType>))]
[TypeHandler(typeof(EnumTextConverter<DiscountScope>))]
[TypeHandler(typeof(EnumTextConverter<TaxKind>))]
[TypeHandler(typeof(EnumTextConverter<StaffRole>))]
[TypeHandler(typeof(EnumTextConverter<ShiftStatus>))]
[TypeHandler(typeof(EnumTextConverter<CashEventType>))]
[TypeHandler(typeof(EnumTextConverter<InventoryChangeType>))]
[TypeHandler(typeof(EnumTextConverter<PointHistoryType>))]
[TypeHandler(typeof(EnumTextConverter<TaxRounding>))]
[TypeHandler(typeof(EnumTextConverter<PointBasis>))]
[TypeHandler(typeof(DateOnlyTextConverter))]
[TypeHandler(typeof(DateTimeTextConverter))]
public static class DataProfile;
