namespace Pos.Shared.PaymentMethods;

using Pos.Shared.Common;

public sealed class PaymentMethodResponse
{
    public Guid Id { get; set; }

    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;

    // 端末の支払ボタンに出す短い名前 (省略時は name)
    public string? ShortName { get; set; }

    public PaymentKind Kind { get; set; }

    // 釣銭あり (預り金 > 充当額 を許可)
    public bool AllowsChange { get; set; }

    // 伝票番号などの参照入力を求める
    public bool RequiresReference { get; set; }

    public bool IsActive { get; set; }

    public int SortOrder { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int Version { get; set; }
}

public sealed class PaymentMethodListResponse : ListResponse<PaymentMethodResponse>;
