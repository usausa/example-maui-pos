namespace Pos.Terminal.Models.Input;

public sealed class NumberInputParameter
{
    public string Title { get; }

    public string Value { get; }

    public int MaxLength { get; }

    // true: 番号入力 (先頭の 0 を残し、空も許す)
    public bool Digits { get; }

    public NumberInputParameter(string title, string value, int maxLength, bool digits = false)
    {
        Title = title;
        Value = value;
        MaxLength = maxLength;
        Digits = digits;
    }
}
