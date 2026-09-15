namespace Pos.Server.Host.Helpers;

// クエリ文字列や並び順ラベルの列挙値。大文字小文字は区別せず、数値や未定義の値は受け付けない
public static class EnumHelper
{
    // 省略 (null / 空) は既定値
    public static bool TryParse<TEnum>(string? value, TEnum defaultValue, out TEnum result)
        where TEnum : struct, Enum
    {
        if (String.IsNullOrEmpty(value))
        {
            result = defaultValue;
            return true;
        }

        result = default;
        return Char.IsLetter(value[0]) && Enum.TryParse(value, true, out result) && Enum.IsDefined(result);
    }

    // 不正な値は既定値
    public static TEnum Parse<TEnum>(string? value, TEnum defaultValue)
        where TEnum : struct, Enum =>
        TryParse(value, defaultValue, out var result) ? result : defaultValue;
}
