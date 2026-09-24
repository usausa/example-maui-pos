namespace Pos.Contract.Products;

// 商品 CSV の取込の結果 (dryRun は反映せずに同じ検証だけを行う)
public sealed class ProductImportResponse
{
    public bool DryRun { get; set; }

    public int InsertCount { get; set; }

    public int UpdateCount { get; set; }

    public int UnchangedCount { get; set; }

    public int ErrorCount { get; set; }

    public IReadOnlyList<ProductImportResponseItem> Items { get; set; } = default!;
}

public sealed class ProductImportResponseItem
{
    // ファイルの行番号 (見出しが 1 行目)
    public int LineNo { get; set; }

    public string? Code { get; set; }

    public string? Name { get; set; }

    public ImportAction Action { get; set; }

    public IReadOnlyList<string> Errors { get; set; } = [];
}
