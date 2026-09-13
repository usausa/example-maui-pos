namespace Pos.Shared.Customers;

using Pos.Shared.Common;

public sealed class CustomerResponse
{
    public Guid Id { get; set; }

    // 会員番号 (会員証バーコード)
    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string? Kana { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? PostalCode { get; set; }

    public string? Address { get; set; }

    public DateOnly? BirthDate { get; set; }

    // サーバ計算。更新 API では変更できない
    public int PointBalance { get; set; }

    public string? Note { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int Version { get; set; }
}

public sealed class CustomerListResponse : ListResponse<CustomerResponse>
{
}
