namespace Pos.Server.Host.Infrastructure.Csv;

using CsvHelper;

// CSV ダウンロード (BOM 付き UTF-8。Excel でそのまま開ける)
// 前後に空白のある値も引用符で囲む (読む側が空白を落とさないように)
public static class CsvExport
{
    private static readonly CsvWriterOptions Options = new() { Quoting = CsvQuoting.WhenNeededOrWhitespace, WriteByteOrderMark = true };

    public static PushStreamHttpResult Stream<T>(IEnumerable<T> rows, string fileName) =>
        TypedResults.Stream(
            async stream =>
            {
                await using var csv = CsvWriter.Create(stream, Options);
                await csv.WriteRecordsAsync(rows);
            },
            "text/csv",
            fileName);
}
