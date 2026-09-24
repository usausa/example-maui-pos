namespace Pos.Server.Host.Infrastructure.Csv;

using CsvHelper;
using CsvHelper.Configuration;

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

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static CsvImportResult<T> Read<T>(ReadOnlySpan<byte> data)
    {
        var text = Decode(data);
        var missing = new List<string>();
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            PrepareHeaderForMatch = static args => args.Header.Trim(),
            HeaderValidated = args => missing.AddRange(args.InvalidHeaders.Select(static x => x.Names[0])),
            MissingFieldFound = null,
            BadDataFound = null
        };

        using var reader = new StringReader(text);
        using var csv = new CsvReader(reader, config);
        if (!csv.Read())
        {
            csv.ValidateHeader<T>();
            return new CsvImportResult<T>(missing, []);
        }

        csv.ReadHeader();
        csv.ValidateHeader<T>();
        if (missing.Count > 0)
        {
            return new CsvImportResult<T>(missing, []);
        }

        var rows = new List<CsvImportRow<T>>();
        while (csv.Read())
        {
            rows.Add(new CsvImportRow<T>(csv.Parser.Row, csv.GetRecord<T>()));
        }

        return new CsvImportResult<T>(missing, rows);
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
