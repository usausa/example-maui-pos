namespace Pos.Terminal.Helpers;

// 設定 QR (Key=Value 行) の読み取り (D-24)
public sealed class SettingParser
{
    private readonly Dictionary<string, string> values;

    public SettingParser(string data)
    {
        values = data
            .Split('\n')
            .Select(static x => (Index: x.IndexOf('=', StringComparison.Ordinal), Line: x))
            .Where(static x => x.Index > 0)
            .GroupBy(static x => x.Line[..x.Index].Trim(), static x => x.Line[(x.Index + 1)..].Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static g => g.Key, static g => g.Last(), StringComparer.OrdinalIgnoreCase);
    }

    public bool TryGetString(string key, out string value)
    {
        if (values.TryGetValue(key, out var str))
        {
            value = str;
            return true;
        }

        value = string.Empty;
        return false;
    }

    public string GetString(string key, string defaultValue = "") => values.GetValueOrDefault(key, defaultValue);
}
