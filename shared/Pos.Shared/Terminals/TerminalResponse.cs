namespace Pos.Shared.Terminals;

using Pos.Shared.Common;

public sealed class TerminalResponse
{
    public Guid Id { get; set; }

    public Guid StoreId { get; set; }

    // 店舗内の端末番号。レシート番号の一部
    public int TerminalNo { get; set; }

    public string Name { get; set; } = default!;

    // サーバが把握している最終レシート連番 (取引登録時に更新)
    public int LastReceiptSeq { get; set; }

    public DateTime? LastSeenAt { get; set; }

    public string? AppVersion { get; set; }

    public bool IsActive { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int Version { get; set; }
}

public sealed class TerminalListResponse : ListResponse<TerminalResponse>;
