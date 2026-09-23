namespace Pos.Terminal.Converters;

// コード → アバターの背景色。同じコードはいつも同じ色になる (文字列の値から色を選ぶ)
public sealed class AvatarColorConverter : IValueConverter
{
    // Indigo / Teal / Purple / DeepOrange / LightBlue / LightGreen / Brown / BlueGray (いずれも 600)
    private static readonly Color[] Palette =
    [
        Color.FromArgb("#3949AB"),
        Color.FromArgb("#00897B"),
        Color.FromArgb("#8E24AA"),
        Color.FromArgb("#F4511E"),
        Color.FromArgb("#039BE5"),
        Color.FromArgb("#7CB342"),
        Color.FromArgb("#6D4C41"),
        Color.FromArgb("#546E7A")
    ];

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string { Length: > 0 } text)
        {
            return Palette[^1];
        }

        // string.GetHashCode は実行ごとに変わるので自前で計算する
        var hash = 0u;
        foreach (var c in text)
        {
            hash = unchecked((hash * 31) + c);
        }

        return Palette[hash % (uint)Palette.Length];
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
