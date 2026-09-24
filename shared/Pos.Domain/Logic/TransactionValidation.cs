namespace Pos.Domain.Logic;

// 各項目はサーバ (Problem Details) と端末 (要確認の表示) が読む。文言は RuleReason / WarningCode から各側で決める
// ReSharper disable NotAccessedPositionalProperty.Global
public sealed record RuleError(ErrorCode Code, RuleReason Reason, Guid? LineId = null);

public sealed record RuleWarning(WarningCode Code, Guid? LineId = null);

public sealed class TransactionValidation
{
    public required IReadOnlyList<RuleError> Errors { get; init; }

    public required IReadOnlyList<RuleWarning> Warnings { get; init; }

    // 再計算結果 (計算できない入力のときは null)
    public SalesResult? Expected { get; init; }

    public bool IsValid => Errors.Count == 0;
}

// 業務ルールの検証に必要な事実。呼び出し側 (エンドポイント・端末) が DB / キャッシュから集める
public sealed class ShiftFact
{
    public required Guid Id { get; init; }

    public required ShiftStatus Status { get; init; }

    public required Guid TerminalId { get; init; }
}

public sealed class ProductFact
{
    public required Guid Id { get; init; }

    public required bool AllowsPriceOverride { get; init; }

    public bool IsActive { get; init; } = true;
}

public sealed class OrderFact
{
    public required Guid Id { get; init; }

    public required Guid StoreId { get; init; }

    public required OrderStatus Status { get; init; }
}

public sealed class SaleContext
{
    public Guid StoreId { get; init; }

    public required Guid TerminalId { get; init; }

    // null = シフトが見つからない
    public ShiftFact? Shift { get; init; }

    public bool ReceiptNoInUse { get; init; }

    // 存在する商品だけを入れる (キー = 商品 ID)
    public required IReadOnlyDictionary<Guid, ProductFact> Products { get; init; }

    public bool HasCustomer { get; init; }

    // 顧客のポイント残高 (顧客なし / 不明なら null)
    public int? CustomerPointBalance { get; init; }

    // 店舗 × 営業日が締め済み (オフラインの端末から遅れて届いた取引。店頭で成立済みなので受理して警告)
    public bool DayClosed { get; init; }

    // 受注から会計したとき。Order は見つからなければ null
    public Guid? OrderId { get; init; }

    public OrderFact? Order { get; init; }
}

public sealed class OriginalTransactionFact
{
    public required Guid Id { get; init; }

    public required TransactionType Type { get; init; }

    public required TransactionStatus Status { get; init; }
}

public sealed class ReturnContext
{
    public required Guid TerminalId { get; init; }

    public ShiftFact? Shift { get; init; }

    public bool ReceiptNoInUse { get; init; }

    // null = 元取引が見つからない
    public OriginalTransactionFact? Original { get; init; }

    public bool HasCustomer { get; init; }

    // 店舗 × 営業日が締め済み (受理して警告)
    public bool DayClosed { get; init; }
}

public sealed class VoidContext
{
    // null = 取引が見つからない
    public TransactionFact? Transaction { get; init; }

    // null = シフトが見つからない
    public ShiftStatus? ShiftStatus { get; init; }

    // 取引の店舗 × 営業日が締め済み (締めた日計を変えないため取消できない。返品で対応する)
    public bool DayClosed { get; init; }
}

public sealed class TransactionFact
{
    public required Guid Id { get; init; }

    public required TransactionType Type { get; init; }

    public required TransactionStatus Status { get; init; }

    public bool HasReturns { get; init; }
}
