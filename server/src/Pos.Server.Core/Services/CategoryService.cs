namespace Pos.Server.Services;

using Pos.Server.Accessors;
using Pos.Server.Models;
using Pos.Server.Models.Entity;
using Pos.Server.Models.Parameters;

// 部門 (2 階層)
public sealed class CategoryService
{
    private readonly TimeProvider timeProvider;
    private readonly IDialect dialect;
    private readonly MasterAccessor masterAccessor;

    public CategoryService(
        TimeProvider timeProvider,
        IDialect dialect,
        MasterAccessor masterAccessor)
    {
        this.timeProvider = timeProvider;
        this.dialect = dialect;
        this.masterAccessor = masterAccessor;
    }

    public async ValueTask<PagedResult<CategoryEntity>> QueryPageAsync(CategoryQueryParameter parameter, CancellationToken cancellationToken)
    {
        var total = await masterAccessor.CountCategoriesAsync(parameter.UpdatedSince, parameter.IncludeDeleted, cancellationToken);
        var items = await masterAccessor.QueryCategoryListAsync(parameter.UpdatedSince, parameter.IncludeDeleted, parameter.Sort, parameter.Desc, parameter.Size, parameter.Page * parameter.Size, cancellationToken);
        return new PagedResult<CategoryEntity>((int)total, parameter.Page, parameter.Size, items);
    }

    // 全件を大分類の並び順に、その中分類を続けた順で返す (セレクタ・ツリー用)。親のない中分類は末尾
    public async ValueTask<List<CategoryEntity>> QueryAllAsync(bool includeDeleted, CancellationToken cancellationToken)
    {
        var categories = await masterAccessor.QueryCategoryAllAsync(includeDeleted, cancellationToken);
        var result = new List<CategoryEntity>(categories.Count);
        foreach (var parent in categories.Where(static x => x.ParentId is null))
        {
            result.Add(parent);
            result.AddRange(categories.Where(x => x.ParentId == parent.Id));
        }

        result.AddRange(categories.Where(x => !result.Contains(x)));
        return result;
    }

    public ValueTask<CategoryEntity?> QueryAsync(Guid id, CancellationToken cancellationToken) =>
        masterAccessor.QueryCategoryAsync(id, cancellationToken);

    // 部門ごとの所属商品数
    public async ValueTask<Dictionary<Guid, long>> QueryProductCountsAsync(CancellationToken cancellationToken) =>
        (await masterAccessor.QueryCategoryProductSummaryAsync(cancellationToken)).ToDictionary(static x => x.CategoryId, static x => x.Count);

    // 親部門がなければ Invalid
    public async ValueTask<DataWriteStatus> InsertAsync(CategoryEntity entity, CancellationToken cancellationToken)
    {
        if (!await ExistsParentAsync(entity.ParentId, cancellationToken))
        {
            return DataWriteStatus.Invalid;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        entity.Id = Guid.CreateVersion7();
        entity.CreatedAt = now;
        entity.UpdatedAt = now;
        entity.Version = 1;
        return await ServiceHelper.InsertAsync(dialect, () => masterAccessor.InsertCategoryAsync(entity, cancellationToken));
    }

    // 親部門に自分自身か、ない部門は指定できない (Invalid)
    public async ValueTask<DataWriteResult<CategoryEntity>> UpdateAsync(CategoryEntity entity, CancellationToken cancellationToken)
    {
        if ((entity.ParentId == entity.Id) || !await ExistsParentAsync(entity.ParentId, cancellationToken))
        {
            return new DataWriteResult<CategoryEntity>(DataWriteStatus.Invalid, null);
        }

        entity.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        return await ServiceHelper.UpdateAsync(
            dialect,
            () => masterAccessor.UpdateCategoryAsync(entity.Id, entity.Code, entity.Name, entity.ParentId, entity.SortOrder, entity.UpdatedAt, entity.Version, cancellationToken),
            async () => await masterAccessor.QueryCategoryAsync(entity.Id, cancellationToken) is { IsDeleted: false });
    }

    // 所属商品・子部門がある部門は削除できない
    public async ValueTask<DataWriteStatus> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if ((await masterAccessor.CountCategoryProductsAsync(id, cancellationToken) > 0) ||
            (await masterAccessor.CountCategoryChildrenAsync(id, cancellationToken) > 0))
        {
            return DataWriteStatus.InUse;
        }

        return await masterAccessor.DeleteCategoryAsync(id, timeProvider.GetUtcNow().UtcDateTime, cancellationToken) > 0 ? DataWriteStatus.Success : DataWriteStatus.NotFound;
    }

    // 参照先は書く前に確かめ、削除済みはないものとする (削除済みを指すと、削除の側の使用中の確かめが崩れる)
    private async ValueTask<bool> ExistsParentAsync(Guid? parentId, CancellationToken cancellationToken) =>
        (parentId is null) || (await masterAccessor.QueryCategoryAsync(parentId.Value, cancellationToken) is { IsDeleted: false });
}
