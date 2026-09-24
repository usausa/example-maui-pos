namespace Pos.Server.Models.Parameters;

// 受注一覧の絞り込み (keyword は受注番号・宛名・電話の部分一致、from / to は受注日)
public sealed class OrderQueryParameter : PagedParameter<OrderSort>
{
    public Guid? StoreId { get; init; }

    public OrderStatus? Status { get; init; }

    // 未完了 (入荷待ち・引き渡し待ち) だけ
    public bool OpenOnly { get; init; }

    public OrderType? Type { get; init; }

    public Guid? CustomerId { get; init; }

    public string? Keyword { get; init; }

    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }
}
