namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

using MudBlazor;

using Pos.Server.Host.Components.Dialogs;
using Pos.Server.Host.Helpers;
using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Entity;
using Pos.Server.Models.Parameters;
using Pos.Server.Services;

// 顧客一覧
public sealed partial class CustomersPage
{
    private MudDataGrid<CustomerEntity> Grid { get; set; } = default!;

    private string? keyword;
    private bool includeDeleted;

    [Inject]
    public required CustomerService CustomerService { get; set; }

    [Inject]
    public required NavigationManager Navigation { get; set; }

    private async Task<GridData<CustomerEntity>> LoadServerData(GridState<CustomerEntity> state, CancellationToken cancellationToken)
    {
        var sort = state.SortDefinitions.FirstOrDefault();
        var parameter = new CustomerQueryParameter
        {
            Keyword = keyword,
            IncludeDeleted = includeDeleted,
            Sort = EnumHelper.Parse(sort?.SortBy, CustomerSort.Code),
            Desc = sort?.Descending ?? false,
            Page = state.Page,
            Size = state.PageSize
        };
        var result = await CustomerService.QueryPageAsync(parameter, cancellationToken);
        return new GridData<CustomerEntity> { TotalItems = result.Total, Items = result.Items };
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

        await RunAsync(async () => NotifyResult(await CustomerService.InsertAsync(CustomerForm.ToEntity(form), CancellationToken), "追加しました。", duplicate: "会員番号が重複しています。"), SearchAsync);
    }
}
