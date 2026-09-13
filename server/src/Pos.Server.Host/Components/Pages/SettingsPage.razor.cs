namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using Pos.Server.Accessors;
using Pos.Server.Host.Infrastructure.Components;
using Pos.Server.Host.Mappers;
using Pos.Server.Host.Models.Forms;

// S-80 会社設定
public sealed partial class SettingsPage
{
    private static readonly SettingsFormValidator Validator = new();

    private MudForm Form { get; set; } = default!;

    private SettingsForm settings = new();

    [Inject]
    public required SettingsAccessor SettingsAccessor { get; set; }

    protected override Task OnInitializedAsync() => LoadAsync();

    private Task LoadAsync() =>
        LoadAsync(async () =>
        {
            var entity = await SettingsAccessor.QueryAsync(CancellationToken);
            settings = entity is null ? new SettingsForm() : FormMapper.ToSettingsForm(entity);
        });

    private async Task SaveAsync()
    {
        await Form.ValidateAsync();
        if (!Form.IsValid)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var rows = await SettingsAccessor.UpdateAsync(settings.CompanyName, settings.Currency, settings.TaxRounding, settings.PointBasis, settings.BusinessDayStartTime, UtcNow, settings.Version, CancellationToken);
            if (rows > 0)
            {
                Snackbar.AddSuccess("保存しました。");
            }
            else
            {
                NotifyVersionMismatch();
            }
        }, LoadAsync);
    }
}
