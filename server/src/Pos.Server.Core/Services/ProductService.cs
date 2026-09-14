namespace Pos.Server.Services;

using Pos.Server.Accessors;
using Pos.Server.Models;
using Pos.Server.Models.Entity;
using Pos.Server.Models.Parameters;
using Pos.Server.Models.Views;

public sealed class ProductService
{
    private static readonly string[] SortColumns = ["Code", "Name", "Price", "UpdatedAt"];
    private const string DefaultSort = "Code";

    private readonly IDialect dialect;
    private readonly ProductAccessor productAccessor;
    private readonly TimeProvider timeProvider;

    public ProductService(
        IDialect dialect,
        ProductAccessor productAccessor,
        TimeProvider timeProvider)
    {
        this.dialect = dialect;
        this.productAccessor = productAccessor;
        this.timeProvider = timeProvider;
    }

    // 差分同期 (UpdatedSince 指定時) は updatedAt, id 順で固定
    public async ValueTask<PagedResult<ProductEntity>> QueryPageAsync(ProductQueryParameter parameter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        var keyword = ServiceHelper.ToLikePattern(dialect, parameter.Keyword);
        var total = await productAccessor.CountAsync(parameter.CategoryId, keyword, parameter.IsActive, parameter.UpdatedSince, parameter.IncludeDeleted, cancellationToken);
        var order = parameter.UpdatedSince is null ? SqlHelper.NormalizeSort(SortColumns, DefaultSort, parameter.Sort, parameter.Desc) : SqlHelper.SyncSort;
        var items = await productAccessor.QueryListAsync(parameter.CategoryId, keyword, parameter.IsActive, parameter.UpdatedSince, parameter.IncludeDeleted, order, parameter.Size, parameter.Page * parameter.Size, cancellationToken);
        return new PagedResult<ProductEntity>((int)total, parameter.Page, parameter.Size, items);
    }

    // 全件 (コード順)
    public ValueTask<List<ProductEntity>> QueryAllAsync(bool includeDeleted, CancellationToken cancellationToken) =>
        productAccessor.QueryAllAsync(includeDeleted, cancellationToken);

    // CSV 出力 (削除済みを除く全件、コード順)
    public ValueTask<List<ProductExportItem>> QueryExportListAsync(CancellationToken cancellationToken) =>
        productAccessor.QueryExportListAsync(cancellationToken);

    public ValueTask<ProductEntity?> QueryAsync(Guid id, CancellationToken cancellationToken) =>
        productAccessor.QueryAsync(id, cancellationToken);

    public ValueTask<ProductEntity?> QueryByBarcodeAsync(string barcode, CancellationToken cancellationToken) =>
        productAccessor.QueryByBarcodeAsync(barcode, cancellationToken);

    public ValueTask<ProductEntity?> QueryByCodeAsync(string code, CancellationToken cancellationToken) =>
        productAccessor.QueryByCodeAsync(code, cancellationToken);

    public ValueTask<DataWriteStatus> InsertAsync(ProductEntity entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        entity.Id = Guid.CreateVersion7();
        entity.CreatedAt = now;
        entity.UpdatedAt = now;
        entity.Version = 1;
        return ServiceHelper.InsertAsync(dialect, () => productAccessor.InsertAsync(entity, cancellationToken));
    }

    public ValueTask<DataWriteStatus> UpdateAsync(ProductEntity entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);

        entity.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        return ServiceHelper.UpdateAsync(
            dialect,
            () => productAccessor.UpdateAsync(
                entity.Id,
                entity.Code,
                entity.Barcode,
                entity.Name,
                entity.Kana,
                entity.Brand,
                entity.ModelNo,
                entity.CategoryId,
                entity.Kind,
                entity.Price,
                entity.TaxIncluded,
                entity.TaxRateId,
                entity.Cost,
                entity.PointRate,
                entity.RequiresSerial,
                entity.TrackInventory,
                entity.AllowsPriceOverride,
                entity.Unit,
                entity.IsActive,
                entity.UpdatedAt,
                entity.Version,
                cancellationToken),
            async () => await productAccessor.QueryAsync(entity.Id, cancellationToken) is { IsDeleted: false });
    }

    public async ValueTask<DataWriteStatus> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        await productAccessor.DeleteAsync(id, timeProvider.GetUtcNow().UtcDateTime, cancellationToken) > 0 ? DataWriteStatus.Success : DataWriteStatus.NotFound;
}
