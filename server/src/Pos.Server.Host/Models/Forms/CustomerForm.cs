namespace Pos.Server.Host.Models.Forms;

using Pos.Server.Models.Entity;

using Smart.Mapper;

public sealed partial class CustomerForm
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Kana { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? PostalCode { get; set; }

    public string? Address { get; set; }

    // MudDatePicker は DateTime? を扱う
    public DateTime? BirthDate { get; set; }

    public string? Note { get; set; }

    public int Version { get; set; }

    // Entity ↔ フォーム (サーバ付与項目はサービスが設定する)
    [Mapper]
    [MapUsing(nameof(BirthDate), nameof(ToBirthDate))]
    public static partial CustomerForm ToForm(CustomerEntity entity);

    [Mapper]
    [MapUsing(nameof(CustomerEntity.BirthDate), nameof(ToBirthDateOnly))]
    public static partial CustomerEntity ToEntity(CustomerForm form);

    private static DateTime? ToBirthDate(CustomerEntity entity) => entity.BirthDate?.ToDateTime(TimeOnly.MinValue);

    private static DateOnly? ToBirthDateOnly(CustomerForm form) => form.BirthDate is null ? null : DateOnly.FromDateTime(form.BirthDate.Value);
}
