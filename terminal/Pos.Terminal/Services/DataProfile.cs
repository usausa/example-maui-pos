namespace Pos.Terminal.Services;

using Pos.Terminal.Helpers.Data;
using Pos.Terminal.Models.Entity;

using Smart.Data.Accessor.Attributes;

// DataAccessor 共通の型変換 ([ExecuteConfig(typeof(DataProfile))])。Pos.Contract の Response をそのままエンティティに使う
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
[TypeHandler(typeof(EnumTextConverter<OrderDepositType>))]
[TypeHandler(typeof(EnumTextConverter<InventoryChangeType>))]
[TypeHandler(typeof(EnumTextConverter<TaxRounding>))]
[TypeHandler(typeof(EnumTextConverter<PointBasis>))]
[TypeHandler(typeof(EnumTextConverter<OutboxKind>))]
[TypeHandler(typeof(EnumTextConverter<OutboxStatus>))]
[TypeHandler(typeof(DateOnlyTextConverter))]
[TypeHandler(typeof(DateTimeTicksConverter))]
public static class DataProfile;
