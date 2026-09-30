namespace Pos.Server.Host.Components.Dialogs;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using Pos.Server.Models.Entity;
using Pos.Server.Services;

using QRCoder;

// 端末の登録: 発行したペアリングコードと設定 QR。QR は template-maui の SettingParser 互換 (行単位の Key=Value)
public sealed partial class TerminalPairingDialog
{
    private string qrText = string.Empty;

    private string qrImage = string.Empty;

    // 接続先が localhost で、端末から届かない
    private bool unreachable;

    [Parameter]
    public required TerminalEntity Terminal { get; set; }

    [Parameter]
    public required string StoreName { get; set; }

    [Parameter]
    public required TerminalPairingCode Code { get; set; }

    [CascadingParameter]
    public required IMudDialogInstance MudDialog { get; set; }

    [Inject]
    public required NavigationManager Navigation { get; set; }

    [Inject]
    public required TerminalSetting Setting { get; set; }

    protected override void OnInitialized()
    {
        // 接続先は端末から届くこのサーバの URL (設定がなければ管理画面を開いた URL)
        var endPoint = String.IsNullOrEmpty(Setting.ApiEndPoint) ? Navigation.BaseUri : Setting.ApiEndPoint;
        unreachable = new Uri(endPoint).IsLoopback;
        qrText = $"ApiEndPoint={endPoint}\nPairingCode={Code.Code}\n";

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(qrText, QRCodeGenerator.ECCLevel.M);
        using var qrCode = new PngByteQRCode(data);
        qrImage = "data:image/png;base64," + Convert.ToBase64String(qrCode.GetGraphic(8));
    }
}
