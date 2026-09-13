namespace Pos.Terminal.Modules;

using Pos.Shared.Customers;

// スキャン画面の用途 (T-11)
public enum ScanMode
{
    // 販売: 読むたびに明細追加して継続
    Product,
    // 1 件読んだら戻る
    ProductOnce,
    Customer,
    Receipt,
    Setup
}

#pragma warning disable CA1724
public static class Parameters
{
    private const string ScanModeKey = nameof(ScanModeKey);
    private const string ReturnToKey = nameof(ReturnToKey);
    private const string ScanResultKey = nameof(ScanResultKey);
    private const string TransactionIdKey = nameof(TransactionIdKey);
    private const string ShiftIdKey = nameof(ShiftIdKey);
    private const string CustomerKey = nameof(CustomerKey);
    private const string CustomerIdKey = nameof(CustomerIdKey);
    private const string CallerReturnToKey = nameof(CallerReturnToKey);
    private const string ProductIdKey = nameof(ProductIdKey);
    private const string ApplyToCartKey = nameof(ApplyToCartKey);
    private const string StateKey = nameof(StateKey);

    public static NavigationParameter Make() => new();

    // Scan

    // callerReturnTo: 呼び出し元がさらに戻る先 (会員選択 → スキャン → 会員選択 → 販売 のように引き継ぐ)
    public static NavigationParameter WithScan(this NavigationParameter parameter, ScanMode mode, ViewId returnTo, ViewId? callerReturnTo = null) =>
        parameter.SetValue(ScanModeKey, mode).SetValue(ReturnToKey, returnTo).WithCallerReturnTo(callerReturnTo);

    public static ScanMode GetScanMode(this INavigationParameter parameter) =>
        parameter.TryGetValue<ScanMode>(ScanModeKey, out var value) ? value : ScanMode.Product;

    public static ViewId GetReturnTo(this INavigationParameter parameter, ViewId defaultView) =>
        parameter.TryGetValue<ViewId>(ReturnToKey, out var value) ? value : defaultView;

    public static NavigationParameter WithScanResult(this NavigationParameter parameter, string value) =>
        parameter.SetValue(ScanResultKey, value);

    public static string? GetScanResult(this INavigationParameter parameter) =>
        parameter.TryGetValue<string>(ScanResultKey, out var value) ? value : null;

    // Transaction / Shift

    public static NavigationParameter WithTransactionId(this NavigationParameter parameter, Guid id) =>
        parameter.SetValue(TransactionIdKey, id);

    public static Guid? GetTransactionId(this INavigationParameter parameter) =>
        parameter.TryGetValue<Guid>(TransactionIdKey, out var value) ? value : null;

    public static NavigationParameter WithShiftId(this NavigationParameter parameter, Guid id) =>
        parameter.SetValue(ShiftIdKey, id);

    public static Guid? GetShiftId(this INavigationParameter parameter) =>
        parameter.TryGetValue<Guid>(ShiftIdKey, out var value) ? value : null;

    // Customer

    public static NavigationParameter WithCustomer(this NavigationParameter parameter, CustomerResponse? customer) =>
        customer is null ? parameter : parameter.SetValue(CustomerKey, customer);

    public static CustomerResponse? GetCustomer(this INavigationParameter parameter) =>
        parameter.TryGetValue<CustomerResponse>(CustomerKey, out var value) ? value : null;

    public static NavigationParameter WithCustomerId(this NavigationParameter parameter, Guid id) =>
        parameter.SetValue(CustomerIdKey, id);

    public static Guid? GetCustomerId(this INavigationParameter parameter) =>
        parameter.TryGetValue<Guid>(CustomerIdKey, out var value) ? value : null;

    // 戻り先 (会員選択などを複数の画面から使う)

    public static NavigationParameter WithReturnTo(this NavigationParameter parameter, ViewId returnTo) =>
        parameter.SetValue(ReturnToKey, returnTo);

    public static NavigationParameter WithCallerReturnTo(this NavigationParameter parameter, ViewId? returnTo) =>
        returnTo is null ? parameter : parameter.SetValue(CallerReturnToKey, returnTo.Value);

    public static ViewId? GetCallerReturnTo(this INavigationParameter parameter) =>
        parameter.TryGetValue<ViewId>(CallerReturnToKey, out var value) ? value : null;

    // 呼び出し元の状態 (スキャン画面がそのまま返す)

    public static NavigationParameter WithState(this NavigationParameter parameter, object? state) =>
        state is null ? parameter : parameter.SetValue(StateKey, state);

    public static T? GetState<T>(this INavigationParameter parameter)
        where T : class =>
        parameter.TryGetValue<T>(StateKey, out var value) ? value : null;

    // Product

    public static NavigationParameter WithProductId(this NavigationParameter parameter, Guid id) =>
        parameter.SetValue(ProductIdKey, id);

    public static Guid? GetProductId(this INavigationParameter parameter) =>
        parameter.TryGetValue<Guid>(ProductIdKey, out var value) ? value : null;

    // 会員登録後にカートへ紐付ける (会員選択からの新規登録)

    public static NavigationParameter WithApplyToCart(this NavigationParameter parameter) =>
        parameter.SetValue(ApplyToCartKey, true);

    public static bool GetApplyToCart(this INavigationParameter parameter) =>
        parameter.TryGetValue<bool>(ApplyToCartKey, out var value) && value;
}
#pragma warning restore CA1724
