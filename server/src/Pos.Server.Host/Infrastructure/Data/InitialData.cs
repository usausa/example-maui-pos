namespace Pos.Server.Host.Infrastructure.Data;

using Pos.Server.Accessors;
using Pos.Server.Models.Entity;

using Smart.Data;

// 起動時の初期データ (architecture §8)。会社設定がなければ (= 空の DB) 投入する
public static class InitialData
{
    // 固定 ID (テストと端末セットアップの QR で参照する)
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

    private static Guid CategoryId(int number) => Id(4, number);

    private static Guid ProductId(int number) => Id(6, number);

    public static async ValueTask SeedAsync(IServiceProvider services, DateTime now, CancellationToken cancellationToken)
    {
        var settingsAccessor = services.GetRequiredService<SettingsAccessor>();
        if (await settingsAccessor.QueryAsync(cancellationToken) is not null)
        {
            return;
        }

        await settingsAccessor.InsertAsync(new SettingsEntity { Id = 1, CompanyName = "うさぎ電機", Currency = "JPY", TaxRounding = TaxRounding.Floor, PointBasis = PointBasis.TaxIncluded, BusinessDayStartTime = "05:00", UpdatedAt = now, Version = 1 }, cancellationToken);

        await SeedStoresAsync(services, now, cancellationToken);
        await SeedTerminalsAsync(services, now, cancellationToken);
        await SeedStaffAsync(services, now, cancellationToken);
        await SeedTaxRatesAsync(services, now, cancellationToken);
        await SeedPaymentMethodsAsync(services, now, cancellationToken);
        await SeedCategoriesAsync(services, now, cancellationToken);
        await SeedProductsAsync(services, now, cancellationToken);
        await SeedDiscountsAsync(services, now, cancellationToken);
        await SeedAdjustmentReasonsAsync(services, now, cancellationToken);
        await SeedCustomersAsync(services, now, cancellationToken);
        await SeedInventoryAsync(services, now, cancellationToken);
    }

    private static async ValueTask SeedStoresAsync(IServiceProvider services, DateTime now, CancellationToken cancellationToken)
    {
        var accessor = services.GetRequiredService<StoreAccessor>();
        await accessor.InsertAsync(new StoreEntity { Id = MainStoreId, Code = "S001", Name = "本店", PostalCode = "100-0001", Address = "東京都千代田区千代田 1-1", Phone = "03-0000-0001", RegistrationNo = "T1234567890123", ReceiptHeader = "うさぎ電機 本店", ReceiptFooter = "またのご来店をお待ちしております", TimeZone = "Asia/Tokyo", IsActive = true, CreatedAt = now, UpdatedAt = now, Version = 1 }, cancellationToken);
        await accessor.InsertAsync(new StoreEntity { Id = BranchStoreId, Code = "S002", Name = "支店", PostalCode = "530-0001", Address = "大阪府大阪市北区梅田 1-1", Phone = "06-0000-0002", RegistrationNo = "T1234567890123", ReceiptHeader = "うさぎ電機 支店", ReceiptFooter = "またのご来店をお待ちしております", TimeZone = "Asia/Tokyo", IsActive = true, CreatedAt = now, UpdatedAt = now, Version = 1 }, cancellationToken);
    }

    private static async ValueTask SeedTerminalsAsync(IServiceProvider services, DateTime now, CancellationToken cancellationToken)
    {
        var accessor = services.GetRequiredService<TerminalAccessor>();
        await accessor.InsertAsync(new TerminalEntity { Id = MainTerminal1Id, StoreId = MainStoreId, TerminalNo = 1, Name = "本店 レジ 1", IsActive = true, CreatedAt = now, UpdatedAt = now, Version = 1 }, cancellationToken);
        await accessor.InsertAsync(new TerminalEntity { Id = MainTerminal2Id, StoreId = MainStoreId, TerminalNo = 2, Name = "本店 レジ 2", IsActive = true, CreatedAt = now, UpdatedAt = now, Version = 1 }, cancellationToken);
        await accessor.InsertAsync(new TerminalEntity { Id = BranchTerminal1Id, StoreId = BranchStoreId, TerminalNo = 1, Name = "支店 レジ 1", IsActive = true, CreatedAt = now, UpdatedAt = now, Version = 1 }, cancellationToken);
    }

    private static async ValueTask SeedStaffAsync(IServiceProvider services, DateTime now, CancellationToken cancellationToken)
    {
        var accessor = services.GetRequiredService<StaffAccessor>();
        await accessor.InsertAsync(new StaffEntity { Id = AdminStaffId, Code = "A001", Name = "本部 管理者", Role = StaffRole.Admin, StoreId = null, IsActive = true, CreatedAt = now, UpdatedAt = now, Version = 1 }, cancellationToken);
        await accessor.InsertAsync(new StaffEntity { Id = ManagerStaffId, Code = "M001", Name = "山田 店長", Role = StaffRole.Manager, StoreId = MainStoreId, IsActive = true, CreatedAt = now, UpdatedAt = now, Version = 1 }, cancellationToken);
        await accessor.InsertAsync(new StaffEntity { Id = MainCashierStaffId, Code = "C001", Name = "佐藤 花子", Role = StaffRole.Cashier, StoreId = MainStoreId, IsActive = true, CreatedAt = now, UpdatedAt = now, Version = 1 }, cancellationToken);
        await accessor.InsertAsync(new StaffEntity { Id = BranchCashierStaffId, Code = "C002", Name = "鈴木 一郎", Role = StaffRole.Cashier, StoreId = BranchStoreId, IsActive = true, CreatedAt = now, UpdatedAt = now, Version = 1 }, cancellationToken);
    }

    private static async ValueTask SeedTaxRatesAsync(IServiceProvider services, DateTime now, CancellationToken cancellationToken)
    {
        var accessor = services.GetRequiredService<TaxRateAccessor>();
        await accessor.InsertAsync(new TaxRateEntity { Id = StandardTaxRateId, Code = "STD", Name = "標準税率 10%", Rate = 0.10m, Kind = TaxKind.Standard, IsDefault = true, SortOrder = 1, CreatedAt = now, UpdatedAt = now, Version = 1 }, cancellationToken);
        await accessor.InsertAsync(new TaxRateEntity { Id = ReducedTaxRateId, Code = "RED", Name = "軽減税率 8%", Rate = 0.08m, Kind = TaxKind.Reduced, IsDefault = false, SortOrder = 2, CreatedAt = now, UpdatedAt = now, Version = 1 }, cancellationToken);
        await accessor.InsertAsync(new TaxRateEntity { Id = ExemptTaxRateId, Code = "EXEMPT", Name = "非課税", Rate = 0m, Kind = TaxKind.Exempt, IsDefault = false, SortOrder = 3, CreatedAt = now, UpdatedAt = now, Version = 1 }, cancellationToken);
    }

    private static async ValueTask SeedPaymentMethodsAsync(IServiceProvider services, DateTime now, CancellationToken cancellationToken)
    {
        var accessor = services.GetRequiredService<PaymentMethodAccessor>();
        await accessor.InsertAsync(new PaymentMethodEntity { Id = CashPaymentMethodId, Code = "CASH", Name = "現金", Kind = PaymentKind.Cash, AllowsChange = true, RequiresReference = false, IsActive = true, SortOrder = 1, CreatedAt = now, UpdatedAt = now, Version = 1 }, cancellationToken);
        await accessor.InsertAsync(new PaymentMethodEntity { Id = CardPaymentMethodId, Code = "CARD", Name = "クレジットカード", Kind = PaymentKind.Card, AllowsChange = false, RequiresReference = true, IsActive = true, SortOrder = 2, CreatedAt = now, UpdatedAt = now, Version = 1 }, cancellationToken);
        await accessor.InsertAsync(new PaymentMethodEntity { Id = Id(8, 3), Code = "QR", Name = "QR 決済", Kind = PaymentKind.Qr, AllowsChange = false, RequiresReference = false, IsActive = true, SortOrder = 3, CreatedAt = now, UpdatedAt = now, Version = 1 }, cancellationToken);
        await accessor.InsertAsync(new PaymentMethodEntity { Id = Id(8, 4), Code = "EMONEY", Name = "電子マネー", Kind = PaymentKind.EMoney, AllowsChange = false, RequiresReference = false, IsActive = true, SortOrder = 4, CreatedAt = now, UpdatedAt = now, Version = 1 }, cancellationToken);
        await accessor.InsertAsync(new PaymentMethodEntity { Id = Id(8, 5), Code = "VOUCHER", Name = "商品券", Kind = PaymentKind.Voucher, AllowsChange = false, RequiresReference = false, IsActive = true, SortOrder = 5, CreatedAt = now, UpdatedAt = now, Version = 1 }, cancellationToken);
        await accessor.InsertAsync(new PaymentMethodEntity { Id = PointsPaymentMethodId, Code = "POINT", Name = "ポイント", Kind = PaymentKind.Points, AllowsChange = false, RequiresReference = false, IsActive = true, SortOrder = 6, CreatedAt = now, UpdatedAt = now, Version = 1 }, cancellationToken);
    }

    private static async ValueTask SeedCategoriesAsync(IServiceProvider services, DateTime now, CancellationToken cancellationToken)
    {
        var accessor = services.GetRequiredService<CategoryAccessor>();
        (int Number, string Code, string Name, int? Parent)[] categories =
        [
            (1, "C01", "家電", null),
            (2, "C02", "カメラ", null),
            (3, "C03", "ホームセンター", null),
            (4, "C04", "サービス", null),
            (11, "C0101", "テレビ・レコーダー", 1),
            (12, "C0102", "冷蔵庫・洗濯機", 1),
            (13, "C0103", "生活家電", 1),
            (21, "C0201", "デジタルカメラ", 2),
            (22, "C0202", "レンズ", 2),
            (23, "C0203", "アクセサリ", 2),
            (31, "C0301", "工具", 3),
            (32, "C0302", "園芸", 3),
            (33, "C0303", "日用品", 3)
        ];
        var sortOrder = 0;
        foreach (var (number, code, name, parent) in categories)
        {
            await accessor.InsertAsync(new CategoryEntity { Id = CategoryId(number), Code = code, Name = name, ParentId = parent is null ? null : CategoryId(parent.Value), SortOrder = ++sortOrder, CreatedAt = now, UpdatedAt = now, Version = 1 }, cancellationToken);
        }
    }

    private static async ValueTask SeedProductsAsync(IServiceProvider services, DateTime now, CancellationToken cancellationToken)
    {
        var accessor = services.GetRequiredService<ProductAccessor>();
        // (番号, コード, JAN (null = 採番), 名称, かな, メーカー, 型番, 部門, 価格, 原価, 還元率, シリアル要, 単位)
        (int Number, string Code, string? Barcode, string Name, string Kana, string? Brand, string? ModelNo, int Category, decimal Price, decimal? Cost, decimal PointRate, bool Serial, string? Unit)[] goods =
        [
            (1, "TV-55K", null, "4K 液晶テレビ 55型", "4K エキショウテレビ 55ガタ", "UsaVision", "UV-55K", 11, 128000m, 96000m, 0.10m, true, "台"),
            (2, "TV-43K", null, "4K 液晶テレビ 43型", "4K エキショウテレビ 43ガタ", "UsaVision", "UV-43K", 11, 78000m, 58000m, 0.10m, true, "台"),
            (3, "BD-2T", null, "ブルーレイレコーダー 2TB", "ブルーレイレコーダー 2TB", "UsaVision", "UBD-2000", 11, 49800m, 37000m, 0.10m, true, "台"),
            (4, "RF-450", null, "冷蔵庫 450L", "レイゾウコ 450L", "UsaCool", "UR-450", 12, 158000m, 118000m, 0.05m, true, "台"),
            (5, "RF-150", null, "冷蔵庫 150L", "レイゾウコ 150L", "UsaCool", "UR-150", 12, 39800m, 29000m, 0.05m, true, "台"),
            (6, "WM-D10", null, "ドラム式洗濯機 10kg", "ドラムシキセンタクキ 10kg", "UsaCool", "UW-D10", 12, 198000m, 148000m, 0.05m, true, "台"),
            (7, "MW-26", null, "電子レンジ 26L", "デンシレンジ 26L", "UsaKitchen", "UM-26", 13, 24800m, 18000m, 0.05m, false, "台"),
            (8, "RC-55", null, "IH 炊飯器 5.5合", "IH スイハンキ 5.5ゴウ", "UsaKitchen", "URC-55", 13, 32000m, 24000m, 0.05m, false, "台"),
            (9, "VC-C1", null, "コードレス掃除機", "コードレスソウジキ", "UsaHome", "UVC-C1", 13, 54800m, 40000m, 0.10m, true, "台"),
            (10, "HD-12", null, "ヘアドライヤー", "ヘアドライヤー", "UsaHome", "UHD-12", 13, 12800m, 9000m, 0.01m, false, "台"),
            (11, "CAM-X100", "4901234567894", "デジタルカメラ X-100", "デジタルカメラ X-100", "UsaCam", "X-100", 21, 80000m, 60000m, 0.10m, true, "台"),
            (12, "CAM-C20", null, "コンパクトデジタルカメラ C-20", "コンパクトデジタルカメラ C-20", "UsaCam", "C-20", 21, 42000m, 31000m, 0.10m, true, "台"),
            (13, "CAM-R5", null, "一眼レフカメラ R-5 ボディ", "イチガンレフカメラ R-5 ボディ", "UsaCam", "R-5", 21, 150000m, 112000m, 0.10m, true, "台"),
            (14, "LENS-2470", null, "標準ズームレンズ 24-70mm", "ヒョウジュンズームレンズ 24-70mm", "UsaCam", "UL-2470", 22, 98000m, 72000m, 0.05m, true, "本"),
            (15, "LENS-50", null, "単焦点レンズ 50mm", "タンショウテンレンズ 50mm", "UsaCam", "UL-50", 22, 32000m, 23000m, 0.05m, true, "本"),
            (16, "LENS-70300", null, "望遠ズームレンズ 70-300mm", "ボウエンズームレンズ 70-300mm", "UsaCam", "UL-70300", 22, 68000m, 50000m, 0.05m, true, "本"),
            (17, "SD-64", "4901234567900", "SD カード 64GB", "SD カード 64GB", "UsaMedia", "USD-64", 23, 2000m, 1200m, 0.01m, false, "枚"),
            (18, "BAG-01", null, "カメラバッグ", "カメラバッグ", "UsaCam", "UB-01", 23, 6800m, 4000m, 0.01m, false, "個"),
            (19, "TRIPOD-01", null, "三脚", "サンキャク", "UsaCam", "UT-01", 23, 9800m, 6000m, 0.01m, false, "本"),
            (20, "BAT-X", null, "予備バッテリー", "ヨビバッテリー", "UsaCam", "UBT-X", 23, 5400m, 3200m, 0.01m, false, "個"),
            (21, "DRILL-01", null, "電動ドリルドライバー", "デンドウドリルドライバー", "UsaTool", "UD-01", 31, 12800m, 8500m, 0.01m, false, "台"),
            (22, "DRIVER-SET", null, "ドライバーセット 10本組", "ドライバーセット 10ホングミ", "UsaTool", "UDS-10", 31, 1980m, 1200m, 0.01m, false, "組"),
            (23, "ROPE-6", null, "ロープ 6mm (切り売り)", "ロープ 6mm キリウリ", null, null, 31, 120m, 70m, 0.01m, false, "m"),
            (24, "SOIL-25", null, "培養土 25L", "バイヨウド 25L", "UsaGarden", null, 32, 698m, 400m, 0.01m, false, "袋"),
            (25, "POT-8", null, "植木鉢 8号", "ウエキバチ 8ゴウ", "UsaGarden", null, 32, 880m, 500m, 0.01m, false, "個"),
            (26, "SCISSORS-G", null, "園芸ばさみ", "エンゲイバサミ", "UsaGarden", "UGS-1", 32, 1280m, 800m, 0.01m, false, "本"),
            (27, "GLOVE-W", null, "作業手袋", "サギョウテブクロ", null, null, 33, 480m, 250m, 0.01m, false, "双"),
            (28, "DETERGENT", null, "洗濯用洗剤 1kg", "センタクヨウセンザイ 1kg", "UsaHome", null, 33, 398m, 250m, 0.01m, false, "個"),
            (29, "BAG-45", null, "ゴミ袋 45L 50枚", "ゴミブクロ 45L 50マイ", null, null, 33, 548m, 300m, 0.01m, false, "袋"),
            (30, "BATT-AA8", null, "単3 アルカリ電池 8本", "タン3 アルカリデンチ 8ホン", "UsaPower", "UAA-8", 33, 698m, 400m, 0.01m, false, "個")
        ];
        foreach (var (number, code, barcode, name, kana, brand, modelNo, category, price, cost, pointRate, serial, unit) in goods)
        {
            await accessor.InsertAsync(new ProductEntity
            {
                Id = ProductId(number),
                Code = code,
                Barcode = barcode ?? Ean13($"49012345{number:0000}"),
                Name = name,
                Kana = kana,
                Brand = brand,
                ModelNo = modelNo,
                CategoryId = CategoryId(category),
                Kind = ProductKind.Goods,
                Price = price,
                TaxIncluded = true,
                TaxRateId = StandardTaxRateId,
                Cost = cost,
                PointRate = pointRate,
                RequiresSerial = serial,
                TrackInventory = true,
                AllowsPriceOverride = false,
                Unit = unit,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
                Version = 1
            }, cancellationToken);
        }

        (int Number, string Code, string Name, string Kana, decimal Price)[] serviceProducts =
        [
            (31, "SVC-DELIVERY", "配送料", "ハイソウリョウ", 1100m),
            (32, "SVC-WARRANTY", "延長保証 (5 年)", "エンチョウホショウ 5ネン", 5000m),
            (33, "SVC-INSTALL", "設置工事", "セッチコウジ", 8000m)
        ];
        foreach (var (number, code, name, kana, price) in serviceProducts)
        {
            await accessor.InsertAsync(new ProductEntity
            {
                Id = ProductId(number),
                Code = code,
                Barcode = null,
                Name = name,
                Kana = kana,
                CategoryId = CategoryId(4),
                Kind = ProductKind.Service,
                Price = price,
                TaxIncluded = true,
                TaxRateId = StandardTaxRateId,
                Cost = null,
                PointRate = 0m,
                RequiresSerial = false,
                TrackInventory = false,
                AllowsPriceOverride = true,
                Unit = "件",
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
                Version = 1
            }, cancellationToken);
        }
    }

    private static async ValueTask SeedDiscountsAsync(IServiceProvider services, DateTime now, CancellationToken cancellationToken)
    {
        var accessor = services.GetRequiredService<DiscountAccessor>();
        await accessor.InsertAsync(new DiscountEntity { Id = StaffDiscountId, Code = "STAFF", Name = "社員割引 10%", Type = DiscountType.Percent, Value = 0.10m, Scope = DiscountScope.Transaction, RequiresApproval = false, IsActive = true, SortOrder = 1, CreatedAt = now, UpdatedAt = now, Version = 1 }, cancellationToken);
        await accessor.InsertAsync(new DiscountEntity { Id = DisplayDiscountId, Code = "DISPLAY", Name = "展示品 5%", Type = DiscountType.Percent, Value = 0.05m, Scope = DiscountScope.Line, RequiresApproval = true, IsActive = true, SortOrder = 2, CreatedAt = now, UpdatedAt = now, Version = 1 }, cancellationToken);
        await accessor.InsertAsync(new DiscountEntity { Id = RoundingDiscountId, Code = "ROUND", Name = "端数値引", Type = DiscountType.Amount, Value = 100m, Scope = DiscountScope.Line, RequiresApproval = false, IsActive = true, SortOrder = 3, CreatedAt = now, UpdatedAt = now, Version = 1 }, cancellationToken);
    }

    private static async ValueTask SeedAdjustmentReasonsAsync(IServiceProvider services, DateTime now, CancellationToken cancellationToken)
    {
        var accessor = services.GetRequiredService<AdjustmentReasonAccessor>();
        (string Code, string Name)[] reasons = [("DAMAGE", "破損"), ("DISPOSE", "廃棄"), ("THEFT", "万引き"), ("INTERNAL", "自家消費"), ("STOCKTAKE", "棚卸差異")];
        for (var i = 0; i < reasons.Length; i++)
        {
            await accessor.InsertAsync(new AdjustmentReasonEntity { Id = Id(9, i + 1), Code = reasons[i].Code, Name = reasons[i].Name, SortOrder = i + 1, IsActive = true, CreatedAt = now, UpdatedAt = now, Version = 1 }, cancellationToken);
        }
    }

    private static async ValueTask SeedCustomersAsync(IServiceProvider services, DateTime now, CancellationToken cancellationToken)
    {
        var accessor = services.GetRequiredService<CustomerAccessor>();
        (int Number, string Code, string Name, string Kana, string Phone, string PostalCode, string Address, DateOnly? BirthDate, int Points)[] customers =
        [
            (1, "M0001", "山田 太郎", "ヤマダ タロウ", "090-0000-0001", "100-0001", "東京都千代田区千代田 1-1-1", new DateOnly(1980, 4, 1), 6000),
            (2, "M0002", "佐藤 花子", "サトウ ハナコ", "090-0000-0002", "150-0001", "東京都渋谷区神宮前 2-2-2", new DateOnly(1992, 8, 15), 0),
            (3, "M0003", "鈴木 一郎", "スズキ イチロウ", "090-0000-0003", "530-0001", "大阪府大阪市北区梅田 3-3-3", null, 500),
            (4, "M0004", "高橋 美咲", "タカハシ ミサキ", "090-0000-0004", "460-0001", "愛知県名古屋市中区三の丸 4-4-4", new DateOnly(1975, 12, 24), 12000),
            (5, "M0005", "田中 健", "タナカ ケン", "090-0000-0005", "810-0001", "福岡県福岡市中央区天神 5-5-5", new DateOnly(2000, 1, 10), 30000)
        ];
        foreach (var (number, code, name, kana, phone, postalCode, address, birthDate, points) in customers)
        {
            await accessor.InsertAsync(new CustomerEntity { Id = Id(10, number), Code = code, Name = name, Kana = kana, Phone = phone, Email = $"member{number}@example.com", PostalCode = postalCode, Address = address, BirthDate = birthDate, PointBalance = points, CreatedAt = now, UpdatedAt = now, Version = 1 }, cancellationToken);
        }

        // 残高は履歴の集計なので、初期残高も Adjust 履歴として残す
        var provider = services.GetRequiredService<IDbProvider>();
        await provider.UsingTxAsync(async (_, tx) =>
        {
            foreach (var (number, _, _, _, _, _, _, _, points) in customers.Where(static x => x.Points > 0))
            {
                await accessor.InsertPointHistoryAsync(tx, new PointHistoryEntity { Id = Id(11, number), CustomerId = Id(10, number), Type = PointHistoryType.Adjust, Points = points, BalanceAfter = points, Reason = "初期データ", StaffId = AdminStaffId, OccurredAt = now, CreatedAt = now }, cancellationToken);
            }

            await tx.CommitAsync(cancellationToken);
        }, cancellationToken);
    }

    private static ValueTask SeedInventoryAsync(IServiceProvider services, DateTime now, CancellationToken cancellationToken)
    {
        var accessor = services.GetRequiredService<InventoryAccessor>();
        var provider = services.GetRequiredService<IDbProvider>();
        return provider.UsingTxAsync(async (_, tx) =>
        {
            // 0 / 少量 / 多量を混ぜる (他店在庫の表示確認用)
            for (var number = 1; number <= 30; number++)
            {
                await accessor.AddQuantityAsync(tx, MainStoreId, ProductId(number), (number % 3) switch { 0 => 0m, 1 => 5m, _ => 30m }, now, cancellationToken);
                await accessor.AddQuantityAsync(tx, BranchStoreId, ProductId(number), ((number + 1) % 3) switch { 0 => 0m, 1 => 3m, _ => 12m }, now, cancellationToken);
            }

            await tx.CommitAsync(cancellationToken);
        }, cancellationToken);
    }

    // JAN (EAN-13) のチェックデジットを付ける
    private static string Ean13(string body)
    {
        var sum = 0;
        for (var i = 0; i < 12; i++)
        {
            var digit = body[i] - '0';
            sum += (i % 2 == 0) ? digit : digit * 3;
        }

        return body + ((10 - (sum % 10)) % 10).ToString(CultureInfo.InvariantCulture);
    }
}
