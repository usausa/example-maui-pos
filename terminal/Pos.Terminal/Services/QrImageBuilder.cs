namespace Pos.Terminal.Services;

using QRCoder;

// 文字列を QR の画像にする (電子レシートのレシート番号)
public static class QrImageBuilder
{
    private const int PixelsPerModule = 12;

    public static SKImage Build(string text)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M, true);
        using var png = new PngByteQRCode(data);
        return SKImage.FromEncodedData(png.GetGraphic(PixelsPerModule));
    }
}
