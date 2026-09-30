namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

using QRCoder;

// 端末の設定 QR。印刷して置き、端末の初期設定で読む。QR は template-maui の SettingParser 互換 (行単位の Key=Value) で、
// 毎回変わるペアリングコードは入れない (コードは端末で入力する)
public sealed partial class TerminalSetupQrPage
{
    private string endPoint = string.Empty;

    private string qrImage = string.Empty;

    // 接続先が localhost で、端末から届かない
    private bool unreachable;

    [Inject]
    public required NavigationManager Navigation { get; set; }

    [Inject]
    public required IJSRuntime JSRuntime { get; set; }

    [Inject]
    public required TerminalSetting Setting { get; set; }

    protected override void OnInitialized()
    {
        // 接続先は端末から届くこのサーバの URL (設定がなければ管理画面を開いた URL)
        endPoint = String.IsNullOrEmpty(Setting.ApiEndPoint) ? Navigation.BaseUri : Setting.ApiEndPoint;
        unreachable = new Uri(endPoint).IsLoopback;

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode($"ApiEndPoint={endPoint}\n", QRCodeGenerator.ECCLevel.M);
        using var qrCode = new PngByteQRCode(data);
        qrImage = "data:image/png;base64," + Convert.ToBase64String(qrCode.GetGraphic(8));
    }

    private Task PrintAsync() => JSRuntime.InvokeVoidAsync("print").AsTask();
}
