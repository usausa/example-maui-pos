namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;

using Pos.Server.Host.Components.Dialogs;
using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Entity;
using Pos.Server.Models.Parameters;
using Pos.Server.Models.Views;
using Pos.Server.Services;

// 顧客詳細 (ポイント履歴・購入履歴・受注・ポイント調整)
public sealed partial class CustomerDetailPage
{
    private CustomerEntity? customer;
    private IReadOnlyList<PointHistoryEntity> histories = [];
    private IReadOnlyList<TransactionEntity> transactions = [];
    private IReadOnlyList<OrderDetailView> orders = [];
    private Dictionary<Guid, string> staffNames = [];

    [Parameter]
    public Guid Id { get; set; }

    [Inject]
    public required CustomerService CustomerService { get; set; }

    [Inject]
    public required TransactionService TransactionService { get; set; }

    [Inject]
    public required OrderService OrderService { get; set; }

    [Inject]
    public required StaffService StaffService { get; set; }

    [Inject]
    public required NavigationManager Navigation { get; set; }

    protected override Task OnParametersSetAsync() => LoadAsync();

    private Task LoadAsync() =>
        LoadAsync(async () =>
        {
            customer = await CustomerService.QueryAsync(Id, CancellationToken);
            if (customer is null)
            {
                return;
            }

            staffNames = (await StaffService.QueryAllAsync(true, CancellationToken)).ToDictionary(static x => x.Id, static x => x.Name);
            histories = (await CustomerService.QueryPointHistoryPageAsync(Id, 0, ListLimit, CancellationToken))?.Items ?? [];
            transactions = (await TransactionService.QueryPageAsync(new TransactionQueryParameter { CustomerId = Id, Desc = true, Size = ListLimit }, CancellationToken)).Items;
            orders = (await OrderService.QueryPageAsync(new OrderQueryParameter { CustomerId = Id, Desc = true, Size = ListLimit }, CancellationToken)).Items;
        });

    private string StaffName(Guid? staffId) => staffId is null ? string.Empty : staffNames.GetValueOrDefault(staffId.Value, "-");

    private async Task EditAsync()
    {
        var form = await ShowEditDialogAsync<CustomerEditDialog, CustomerForm>("顧客編集", CustomerForm.ToForm(customer!));
        if (form is null)
        {
            return;
        }

        await RunAsync(async () => NotifyResult(await CustomerService.UpdateAsync(CustomerForm.ToEntity(form), CancellationToken), "更新しました。", duplicate: "会員番号が重複しています。"), LoadAsync);
    }

    private async Task DeleteAsync()
    {
        if (!await ConfirmDeleteAsync(customer!.Name))
        {
            return;
        }

        await RunAsync(async () =>
        {
            var status = await CustomerService.DeleteAsync(Id, CancellationToken);
            NotifyResult(status, "削除しました。");
            if (status == DataWriteStatus.Success)
            {
                Navigation.NavigateTo("customers");
            }
        });
    }

    // 手動調整 (Adjust 履歴を作り、残高を加減算する)
    private async Task AdjustPointsAsync()
    {
        var form = await ShowEditDialogAsync<PointAdjustDialog, PointAdjustForm>("ポイント調整", new PointAdjustForm(), Styles.SmallDialog);
        if (form is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var history = await CustomerService.AdjustPointsAsync(Id, form.Points, form.Reason, form.StaffId, CancellationToken);
            NotifyResult(history is null ? DataWriteStatus.NotFound : DataWriteStatus.Success, "ポイントを調整しました。");
        }, LoadAsync);
    }
}
