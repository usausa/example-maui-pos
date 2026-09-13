namespace Pos.Server.Host.Infrastructure.Api;

using CsvHelper;

// CSV ダウンロード (BOM 付き UTF-8。Excel でそのまま開ける)
public static class CsvExport
{
    public static PushStreamHttpResult Stream<T>(IEnumerable<T> rows, string fileName) =>
        TypedResults.Stream(
            async stream =>
            {
                await using var writer = new StreamWriter(stream, new UTF8Encoding(true));
                await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
                await csv.WriteRecordsAsync(rows);
            },
            "text/csv",
            fileName);
}
