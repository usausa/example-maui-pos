namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;

using Pos.Server.Accessors;
using Pos.Server.Host.Components.Dialogs;
using Pos.Server.Host.Infrastructure.Api;
using Pos.Server.Host.Infrastructure.Components;
using Pos.Server.Host.Mappers;
using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Entity;

using Smart.Data;

// S-61 顧客詳細 (ポイント履歴・購入履歴・S-62 ポイント調整)
public sealed partial class CustomerDetailPage
{
    private CustomerEntity? customer;

    private List<PointHistoryEntity> histories = [];

    private List<TransactionEntity> transactions = [];

    private Dictionary<Guid, string> staffNames = [];

    [Parameter]
    public Guid Id { get; set; }

    [Inject]
    public required CustomerAccessor CustomerAccessor { get; set; }

    [Inject]
    public required TransactionAccessor TransactionAccessor { get; set; }

    [Inject]
    public required StaffAccessor StaffAccessor { get; set; }

    [Inject]
    public required IDbProvider Provider { get; set; }

    [Inject]
    public required NavigationManager Navigation { get; set; }

    protected override Task OnParametersSetAsync() => LoadAsync();

    private Task LoadAsync() =>
        LoadAsync(async () =>
        {
            customer = await CustomerAccessor.QueryAsync(Id, CancellationToken);
            if (customer is null)
            {
                return;
            }

            staffNames = (await StaffAccessor.QueryListAsync(null, null, true, "Code", ApiHelper.MaxPageSize, 0, CancellationToken)).ToDictionary(static x => x.Id, static x => x.Name);
            histories = await CustomerAccessor.QueryPointHistoryListAsync(Id, ApiHelper.MaxPageSize, 0, CancellationToken);
            transactions = await TransactionAccessor.QueryListAsync(null, null, null, null, Id, null, null, null, null, "TransactedAt DESC", ApiHelper.MaxPageSize, 0, CancellationToken);
        });

    private string StaffName(Guid? staffId) => staffId is null ? string.Empty : staffNames.GetValueOrDefault(staffId.Value, "-");

    private async Task EditAsync()
    {
        var form = await ShowEditDialogAsync<CustomerEditDialog, CustomerForm>("顧客編集", FormMapper.ToCustomerForm(customer!));
        if (form is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var birthDate = form.BirthDate is null ? (DateOnly?)null : DateOnly.FromDateTime(form.BirthDate.Value);
            var rows = await CustomerAccessor.UpdateAsync(form.Id, form.Code, form.Name, form.Kana, form.Phone, form.Email, form.PostalCode, form.Address, birthDate, form.Note, UtcNow, form.Version, CancellationToken);
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

    private async Task DeleteAsync()
    {
        if (!await ConfirmDeleteAsync(customer!.Name))
        {
            return;
        }

        await RunAsync(async () =>
        {
            if (await CustomerAccessor.DeleteAsync(Id, UtcNow, CancellationToken) > 0)
            {
                Snackbar.AddSuccess("削除しました。");
                Navigation.NavigateTo("customers");
            }
            else
            {
                NotifyNotFound();
            }
        });
    }

    // 手動調整 (Adjust 履歴を作り、残高を加減算する)。API の POST /customers/{id}/points/adjust と同じ
    private async Task AdjustPointsAsync()
    {
        var form = await ShowEditDialogAsync<PointAdjustDialog, PointAdjustForm>("ポイント調整", new PointAdjustForm(), Styles.SmallDialog);
        if (form is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var now = UtcNow;
            await Provider.UsingTxAsync(async (_, tx) =>
            {
                var balance = await CustomerAccessor.AddPointsAsync(tx, Id, form.Points, now, CancellationToken);
                await CustomerAccessor.InsertPointHistoryAsync(tx, new PointHistoryEntity
                {
                    Id = Guid.CreateVersion7(),
                    CustomerId = Id,
                    Type = PointHistoryType.Adjust,
                    Points = form.Points,
                    BalanceAfter = balance,
                    Reason = form.Reason,
                    StaffId = form.StaffId,
                    OccurredAt = now,
                    CreatedAt = now
                }, CancellationToken);
                await tx.CommitAsync(CancellationToken);
            }, CancellationToken);
            Snackbar.AddSuccess("ポイントを調整しました。");
        }, LoadAsync);
    }
}
