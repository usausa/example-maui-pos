namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using Pos.Server.Accessors;
using Pos.Server.Host.Components.Dialogs;
using Pos.Server.Host.Infrastructure.Api;
using Pos.Server.Host.Infrastructure.Components;
using Pos.Server.Host.Mappers;
using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Entity;

// S-53 部門 (2 階層のツリー)
public sealed partial class CategoriesPage
{
    private List<CategoryEntity> items = [];

    private List<TreeItemData<CategoryEntity>> tree = [];

    private Dictionary<Guid, long> productCounts = [];

    [Inject]
    public required CategoryAccessor CategoryAccessor { get; set; }

    protected override Task OnInitializedAsync() => LoadAsync();

    private Task LoadAsync() =>
        LoadAsync(async () =>
        {
            items = await CategoryAccessor.QueryListAsync(null, false, "SortOrder, Code", ApiHelper.MaxPageSize, 0, CancellationToken);
            productCounts = [];
            foreach (var item in items)
            {
                productCounts[item.Id] = await CategoryAccessor.CountProductsAsync(item.Id, CancellationToken);
            }

            tree = items
                .Where(static x => x.ParentId is null)
                .Select(parent => new TreeItemData<CategoryEntity>
                {
                    Value = parent,
                    Text = parent.Name,
                    Expanded = true,
                    Children = items.Where(x => x.ParentId == parent.Id).Select(static child => new TreeItemData<CategoryEntity> { Value = child, Text = child.Name }).ToList()
                })
                .ToList();
        });

    private long ProductCount(Guid id) => productCounts.GetValueOrDefault(id);

    private IReadOnlyList<CategoryEntity> Parents => items.Where(static x => x.ParentId is null).ToList();

    private async Task AddAsync(Guid? parentId)
    {
        var form = await ShowEditDialogAsync<CategoryEditDialog, CategoryForm>("部門追加", new CategoryForm { ParentId = parentId }, Styles.SmallDialog, p => p.Add(nameof(CategoryEditDialog.Parents), Parents));
        if (form is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var now = UtcNow;
            var entity = FormMapper.ToCategoryEntity(form);
            entity.Id = Guid.CreateVersion7();
            entity.CreatedAt = now;
            entity.UpdatedAt = now;
            entity.Version = 1;
            await CategoryAccessor.InsertAsync(entity, CancellationToken);
            Snackbar.AddSuccess("追加しました。");
        }, LoadAsync);
    }

    private async Task EditAsync(CategoryEntity entity)
    {
        var form = await ShowEditDialogAsync<CategoryEditDialog, CategoryForm>("部門編集", FormMapper.ToCategoryForm(entity), Styles.SmallDialog, p => p.Add(nameof(CategoryEditDialog.Parents), Parents));
        if (form is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var rows = await CategoryAccessor.UpdateAsync(form.Id, form.Code, form.Name, form.ParentId, form.SortOrder, UtcNow, form.Version, CancellationToken);
            if (rows > 0)
            {
                Snackbar.AddSuccess("更新しました。");
            }
            else
            {
                NotifyVersionMismatch();
            }
        }, LoadAsync);
    }

    // 商品または子部門がある部門は削除できない (api-design §3.5)
    private async Task DeleteAsync(CategoryEntity entity)
    {
        if (!await ConfirmDeleteAsync(entity.Name))
        {
            return;
        }

        await RunAsync(async () =>
        {
            if ((await CategoryAccessor.CountProductsAsync(entity.Id, CancellationToken) > 0) ||
                (await CategoryAccessor.CountChildrenAsync(entity.Id, CancellationToken) > 0))
            {
                Snackbar.AddWarning("商品または子部門がある部門は削除できません。");
                return;
            }

            if (await CategoryAccessor.DeleteAsync(entity.Id, UtcNow, CancellationToken) > 0)
            {
                Snackbar.AddSuccess("削除しました。");
            }
            else
            {
                NotifyNotFound();
            }
        }, LoadAsync);
    }
}
