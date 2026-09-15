namespace Pos.Server.Host.Components.Dialogs;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using Pos.Server.Models.Entity;

using QRCoder;

// 端末の設定 QR。template-maui の SettingParser 互換 (行単位の Key=Value)
public sealed partial class TerminalQrDialog
{
    private string qrText = string.Empty;

    private string qrImage = string.Empty;

    [Parameter]
    public required TerminalEntity Terminal { get; set; }

    [Parameter]
    public required string StoreName { get; set; }

    [CascadingParameter]
    public required IMudDialogInstance MudDialog { get; set; }

    [Inject]
    public required NavigationManager Navigation { get; set; }

    protected override void OnInitialized()
    {
        // 接続先はサーバ自身の URL
        qrText = $"ApiEndPoint={Navigation.BaseUri}\nStoreId={Terminal.StoreId}\nTerminalId={Terminal.Id}\n";

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(qrText, QRCodeGenerator.ECCLevel.M);
        using var qrCode = new PngByteQRCode(data);
        qrImage = "data:image/png;base64," + Convert.ToBase64String(qrCode.GetGraphic(8));
    }
}
