namespace Pos.Server.Host.Infrastructure.Csv;

using CsvHelper;

// LineNo はファイルの行番号 (見出しが 1 行目)
public sealed record CsvImportRow<T>(int LineNo, T Record);

// 見出しに足りない列があれば MissingHeaders に入れ、行は読まない
public sealed record CsvImportResult<T>(IReadOnlyList<string> MissingHeaders, IReadOnlyList<CsvImportRow<T>> Rows);

// CSV の取込。UTF-8 (BOM の有無を問わない) と Shift_JIS (Excel の既定の CSV) を判別し、見出しの名前で列を読む。
// 行の値は T の文字列のプロパティで受け、変換と検証は呼び出し側が行う
public static class CsvImport
{
    // 取り込むファイルの上限
    public const int MaxBytes = 5 * 1024 * 1024;

    // 区切りはカンマに決める (先頭の行から推し量らせない)
    private static readonly CsvReaderOptions Options = new() { Separator = ',' };

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static CsvImportResult<T> Read<T>(ReadOnlySpan<byte> data)
    {
        using var reader = new StringReader(Decode(data));
        using var csv = CsvReader.Create(reader, Options);
        if (!csv.TryValidateHeader<T>(out var missing))
        {
            return new CsvImportResult<T>(missing.Select(HeaderName).ToList(), []);
        }

        var rows = new List<CsvImportRow<T>>();
        while (csv.Read())
        {
            rows.Add(new CsvImportRow<T>((int)csv.RowNumber + 1, csv.GetRecord<T>()));
        }

        return new CsvImportResult<T>([], rows);
    }

    // 足りない列は「型.メンバー ('見出し')」の形で返るので、見出しの名前にする
    private static string HeaderName(string column)
    {
        var start = column.IndexOf("('", StringComparison.Ordinal);
        return (start >= 0) && column.EndsWith("')", StringComparison.Ordinal) ? column[(start + 2)..^2] : column;
    }

    // BOM があれば UTF-8、UTF-8 として読めなければ Shift_JIS
    private static string Decode(ReadOnlySpan<byte> data)
    {
        var preamble = Encoding.UTF8.Preamble;
        if (data.StartsWith(preamble))
        {
            return Encoding.UTF8.GetString(data[preamble.Length..]);
        }

        try
        {
            return StrictUtf8.GetString(data);
        }
        catch (DecoderFallbackException)
        {
            return CodePagesEncodingProvider.Instance.GetEncoding(932)!.GetString(data);
        }
    }
}
