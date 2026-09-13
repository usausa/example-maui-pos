namespace Pos.Terminal.Converters;

using QRCoder;

// 文字列 → QR 画像 (電子レシートのレシート番号)
public sealed class QrImageSourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string text || String.IsNullOrEmpty(text))
        {
            return null;
        }

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M, true);
        using var png = new PngByteQRCode(data);
        var bytes = png.GetGraphic(12);
        return ImageSource.FromStream(() => new MemoryStream(bytes));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
