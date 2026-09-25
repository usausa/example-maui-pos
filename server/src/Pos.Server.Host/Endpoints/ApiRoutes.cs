namespace Pos.Server.Host.Endpoints;

public static class ApiRoutes
{
    public const string Prefix = "/api/v1";

    public const string Settings = Prefix + "/settings";

    public const string Stores = Prefix + "/stores";

    public const string Terminals = Prefix + "/terminals";

    public const string Staff = Prefix + "/staff";

    public const string Categories = Prefix + "/categories";

    public const string TaxRates = Prefix + "/tax-rates";

    public const string Products = Prefix + "/products";

    public const string Discounts = Prefix + "/discounts";

    public const string PaymentMethods = Prefix + "/payment-methods";

    public const string Sync = Prefix + "/sync";

    public const string Customers = Prefix + "/customers";

    public const string Transactions = Prefix + "/transactions";

    public const string Shifts = Prefix + "/shifts";

    public const string DailyClosings = Prefix + "/daily-closings";

    public const string Orders = Prefix + "/orders";

    public const string Inventory = Prefix + "/inventory";

    public const string AdjustmentReasons = Inventory + "/adjustment-reasons";

    public const string Suppliers = Inventory + "/suppliers";

    public const string InventoryReceipts = Inventory + "/receipts";

    public const string InventoryTransfers = Inventory + "/transfers";

    public const string PurchaseOrders = Inventory + "/purchase-orders";

    public const string Reports = Prefix + "/reports";

    // 商品画像 (ImageUrl はこれに ?v={内容のハッシュ} を付けたもの)
    public static string ProductImage(Guid id) => $"{Products}/{id}/image";
}
