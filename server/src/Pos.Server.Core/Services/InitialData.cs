namespace Pos.Server.Services;

// 初期データ (Host の Assets/Data/InitialData.sql) の固定 ID。テストと端末セットアップの QR で参照する
public static class InitialData
{
    public static readonly Guid MainStoreId = Id(1, 1);
    public static readonly Guid BranchStoreId = Id(1, 2);

    public static readonly Guid MainTerminal1Id = Id(2, 1);
    public static readonly Guid MainTerminal2Id = Id(2, 2);
    public static readonly Guid BranchTerminal1Id = Id(2, 3);

    public static readonly Guid AdminStaffId = Id(3, 1);
    public static readonly Guid ManagerStaffId = Id(3, 2);
    public static readonly Guid MainCashierStaffId = Id(3, 3);
    public static readonly Guid BranchCashierStaffId = Id(3, 4);

    public static readonly Guid StandardTaxRateId = Id(5, 1);
    public static readonly Guid ReducedTaxRateId = Id(5, 2);
    public static readonly Guid ExemptTaxRateId = Id(5, 3);

    public static readonly Guid CameraProductId = Id(6, 11);
    public static readonly Guid SdCardProductId = Id(6, 17);
    public static readonly Guid DeliveryProductId = Id(6, 31);

    public static readonly Guid StaffDiscountId = Id(7, 1);
    public static readonly Guid DisplayDiscountId = Id(7, 2);
    public static readonly Guid RoundingDiscountId = Id(7, 3);

    public static readonly Guid CashPaymentMethodId = Id(8, 1);
    public static readonly Guid CardPaymentMethodId = Id(8, 2);
    public static readonly Guid PointsPaymentMethodId = Id(8, 6);

    public static readonly Guid Customer1Id = Id(10, 1);

    private static Guid Id(int kind, int number) => new($"00000000-0000-0000-{kind:x4}-{number:x12}");
}
