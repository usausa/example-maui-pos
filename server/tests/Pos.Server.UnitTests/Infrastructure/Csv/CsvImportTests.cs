namespace Pos.Server.Infrastructure.Csv;

using System.Text;

using Pos.Server.Host.Infrastructure.Csv;
using Pos.Server.Host.Models.Import;

// CSV の取込: 文字コードの判別、見出しの過不足、行番号 (見出しが 1 行目、改行を含む値は 1 行と数える)
public sealed class CsvImportTests
{
    private const string Header = "コード,JAN,商品名,かな,メーカー,型番,部門コード,部門,種別,価格,内税,税率コード,原価,還元率,シリアル要,在庫管理,売価変更可,単位,販売可";

    // BOM 付き UTF-8 の値 (改行を含む) を読み、行番号はレコードで数える
    [Fact]
    public void ReadsUtf8WithBomAndCountsRecords()
    {
        // Arrange
        var text = $"{Header}\r\nA001,,\"カメラ\r\n本体\",,,,C01,部門,Goods,1000,True,T10,,0,False,True,False,,True\r\nA002,,レンズ,,,,C01,,Goods,2000,True,T10,,0,False,True,False,,True\r\n";
        var data = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(text)).ToArray();

        // Act
        var result = CsvImport.Read<ProductImportRow>(data);

        // Assert
        Assert.Empty(result.MissingHeaders);
        Assert.Equal([2, 3], result.Rows.Select(static x => x.LineNo));
        Assert.Equal("カメラ\r\n本体", result.Rows[0].Record.Name);
        Assert.Equal("2000", result.Rows[1].Record.Price);
    }

    // Excel の既定の Shift_JIS も読む (UTF-8 として読めなければ Shift_JIS)
    [Fact]
    public void ReadsShiftJis()
    {
        // Arrange
        var data = CodePagesEncodingProvider.Instance.GetEncoding(932)!.GetBytes($"{Header}\r\nA001,,デジタルカメラ,,,,C01,,Goods,1000,True,T10,,0,False,True,False,,True\r\n");

        // Act
        var result = CsvImport.Read<ProductImportRow>(data);

        // Assert
        Assert.Equal("デジタルカメラ", result.Rows.Single().Record.Name);
    }

    // 足りない列を見出しの名前で返し、行は読まない。参照用の「部門」はなくてよい
    [Fact]
    public void ReportsMissingHeaders()
    {
        // Arrange
        var data = "コード,商品名,部門コード,種別\r\nA001,カメラ,C01,Goods\r\n"u8.ToArray();

        // Act
        var result = CsvImport.Read<ProductImportRow>(data);

        // Assert
        Assert.Contains("価格", result.MissingHeaders);
        Assert.DoesNotContain("部門", result.MissingHeaders);
        Assert.DoesNotContain("コード", result.MissingHeaders);
        Assert.Empty(result.Rows);
    }
}
