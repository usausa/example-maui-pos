namespace Pos.Server.Infrastructure.Imaging;

// 画像の形式をデータの先頭のバイトで判定する (ファイル名や要求の Content-Type を信用しない)
public static class ImageContentType
{
    public const string Jpeg = "image/jpeg";

    public const string Png = "image/png";

    public static string? Detect(ReadOnlySpan<byte> data)
    {
        if (data.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
        {
            return Jpeg;
        }

        if (data.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return Png;
        }

        return null;
    }
}
