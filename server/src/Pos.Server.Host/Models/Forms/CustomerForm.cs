namespace Pos.Server.Host.Models.Forms;

public sealed class CustomerForm
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
}
