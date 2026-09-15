namespace Pos.Server.Accessors;

using Pos.Server.Models.Entity;
using Pos.Server.Models.Views;

[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class ProductAccessor
{
    [Execute]
    public partial void Create();

    // keyword は呼び出し側でエスケープ済みの LIKE パターン (%...%)
    [ExecuteScalar]
    public partial ValueTask<long> CountAsync(Guid? categoryId, string? keyword, bool? isActive, DateTime? updatedSince, bool includeDeleted, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<ProductEntity>> QueryListAsync(Guid? categoryId, string? keyword, bool? isActive, DateTime? updatedSince, bool includeDeleted, ProductSort sort, bool desc, int limit, int offset, CancellationToken cancellationToken);

    // 全件 (コード順)
    [Query]
    public partial ValueTask<List<ProductEntity>> QueryAllAsync(bool includeDeleted, CancellationToken cancellationToken);

    [QueryFirst]
    [SelectSingle(typeof(ProductEntity))]
    public partial ValueTask<ProductEntity?> QueryAsync(Guid id, CancellationToken cancellationToken);

    [QueryFirst]
    public partial ValueTask<ProductEntity?> QueryByBarcodeAsync(string barcode, CancellationToken cancellationToken);

    [QueryFirst]
    public partial ValueTask<ProductEntity?> QueryByCodeAsync(string code, CancellationToken cancellationToken);

    // CSV 出力 (削除済みを除く全件、コード順。部門・税率のコードと名称付き)
    [Query]
    public partial ValueTask<List<ProductExportView>> QueryExportListAsync(CancellationToken cancellationToken);

    // 取引検証用 (明細の商品をまとめて取得)
    [Query]
    public partial ValueTask<List<ProductEntity>> QueryByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken);

    [Execute]
    [Insert(typeof(ProductEntity))]
    public partial ValueTask<int> InsertAsync(ProductEntity entity, CancellationToken cancellationToken);

    // Version が一致する行だけ更新し、更新後の行を返す (null = 競合または削除済み)
    [QueryFirst]
    public partial ValueTask<ProductEntity?> UpdateAsync(
        Guid id,
        string code,
        string? barcode,
        string name,
        string? kana,
        string? brand,
        string? modelNo,
        Guid categoryId,
        ProductKind kind,
        decimal price,
        bool taxIncluded,
        Guid taxRateId,
        decimal? cost,
        decimal pointRate,
        bool requiresSerial,
        bool trackInventory,
        bool allowsPriceOverride,
        string? unit,
        bool isActive,
        DateTime updatedAt,
        int version,
        CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> DeleteAsync(Guid id, DateTime updatedAt, CancellationToken cancellationToken);
}
