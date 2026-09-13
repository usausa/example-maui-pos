namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

using MudBlazor;

using Pos.Server.Accessors;
using Pos.Server.Host.Components.Dialogs;
using Pos.Server.Host.Infrastructure.Api;
using Pos.Server.Host.Infrastructure.Components;
using Pos.Server.Host.Mappers;
using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Entity;

// S-60 顧客一覧
public sealed partial class CustomersPage
{
    private static readonly string[] SortColumns = ["Code", "Name", "Kana", "PointBalance", "CreatedAt", "UpdatedAt"];

    private MudDataGrid<CustomerEntity> Grid { get; set; } = default!;

    private string? keyword;

    private bool includeDeleted;

    [Inject]
    public required CustomerAccessor CustomerAccessor { get; set; }

    [Inject]
    public required NavigationManager Navigation { get; set; }

    private async Task<GridData<CustomerEntity>> LoadServerData(GridState<CustomerEntity> state, CancellationToken cancellationToken)
    {
        var sort = state.SortDefinitions.FirstOrDefault();
        var order = SqlHelper.NormalizeSort(SortColumns, "Code", sort?.SortBy, sort?.Descending ?? false);
        var pattern = ApiHelper.ToLikePattern(Dialect, keyword);
        var total = await CustomerAccessor.CountAsync(pattern, null, null, null, includeDeleted, cancellationToken);
        var items = await CustomerAccessor.QueryListAsync(pattern, null, null, null, includeDeleted, order, state.PageSize, state.Page * state.PageSize, cancellationToken);
        return new GridData<CustomerEntity> { TotalItems = (int)total, Items = items };
    }

    private Task SearchAsync() => Grid.ReloadServerData();

    private Task OnSearchKeyDown(KeyboardEventArgs args) =>
        args.Key == "Enter" ? SearchAsync() : Task.CompletedTask;

    private void OnRowClick(DataGridRowClickEventArgs<CustomerEntity> args) =>
        Navigation.NavigateTo($"customers/{args.Item.Id}");

    private async Task AddAsync()
    {
        var form = await ShowEditDialogAsync<CustomerEditDialog, CustomerForm>("顧客追加", new CustomerForm());
        if (form is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var now = UtcNow;
            var entity = FormMapper.ToCustomerEntity(form);
            entity.Id = Guid.CreateVersion7();
            entity.CreatedAt = now;
            entity.UpdatedAt = now;
            entity.Version = 1;
            await CustomerAccessor.InsertAsync(entity, CancellationToken);
            Snackbar.AddSuccess("追加しました。");
        }, SearchAsync);
    }
}
