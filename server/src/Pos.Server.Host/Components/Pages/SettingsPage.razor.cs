namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using Pos.Server.Host.Models.Forms;
using Pos.Server.Services;

// S-80 会社設定
public sealed partial class SettingsPage
{
    private static readonly SettingsFormValidator Validator = new();

    private MudForm Form { get; set; } = default!;

    private SettingsForm settings = new();

    [Inject]
    public required SettingsService SettingsService { get; set; }

    protected override Task OnInitializedAsync() => LoadAsync();

    private Task LoadAsync() =>
        LoadAsync(async () =>
        {
            var entity = await SettingsService.QueryAsync(CancellationToken);
            settings = entity is null ? new SettingsForm() : SettingsForm.ToForm(entity);
        });

    private async Task SaveAsync()
    {
        await Form.ValidateAsync();
        if (!Form.IsValid)
        {
            return;
        }

        await RunAsync(async () => NotifyResult(await SettingsService.UpdateAsync(SettingsForm.ToEntity(settings), CancellationToken), "保存しました。"), LoadAsync);
    }
}
