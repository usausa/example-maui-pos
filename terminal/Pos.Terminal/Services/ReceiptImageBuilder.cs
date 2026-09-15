namespace Pos.Terminal.Services;

// レシート文字列 (等幅 32 桁) を画像にする。端末フォントが等幅でなくても桁位置で描くので崩れない
public static class ReceiptImageBuilder
{
    private const int Columns = 32;

    private const float FontSize = 26f;

    private const float ColumnWidth = 15f;

    private const float LineHeight = 34f;

    private const float Padding = 24f;

    public static byte[] Build(string text)
    {
        var lines = text.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n');
        var width = (int)((Columns * ColumnWidth) + (Padding * 2));
        var height = (int)((lines.Length * LineHeight) + (Padding * 2));

        using var typeface = ResolveTypeface();
        using var font = new SKFont(typeface, FontSize);
        using var paint = new SKPaint();
        paint.Color = SKColors.Black;
        paint.IsAntialias = true;

        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        var baseline = Padding + FontSize;
        foreach (var line in lines)
        {
            var column = 0;
            foreach (var c in line)
            {
                var span = c > 0xFF ? 2 : 1;
                var slot = span * ColumnWidth;
                var glyph = c.ToString();
                var glyphWidth = font.MeasureText(glyph);
                var x = Padding + (column * ColumnWidth) + Math.Max(0, (slot - glyphWidth) / 2);
                canvas.DrawText(glyph, x, baseline, SKTextAlign.Left, font, paint);
                column += span;
            }

            baseline += LineHeight;
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    // 日本語を描ける書体 (端末の既定 CJK フォント)
    private static SKTypeface ResolveTypeface() =>
        SKFontManager.Default.MatchCharacter('円') ?? SKTypeface.Default;
}
