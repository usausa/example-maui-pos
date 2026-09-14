namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;

using Pos.Server.Accessors;
using Pos.Server.Host.Components.Dialogs;
using Pos.Server.Host.Infrastructure.Components;
using Pos.Server.Host.Mappers;
using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Entity;

// S-56 支払方法
public sealed partial class PaymentMethodsPage
{
    private List<PaymentMethodEntity> items = [];

    private bool includeDeleted;

    [Inject]
    public required PaymentMethodAccessor PaymentMethodAccessor { get; set; }

    protected override Task OnInitializedAsync() => LoadAsync();

    private Task LoadAsync() =>
        LoadAsync(async () =>
        {
            items = await PaymentMethodAccessor.QueryListAsync(null, includeDeleted, CancellationToken);
        });

    private async Task AddAsync()
    {
        var form = await ShowEditDialogAsync<PaymentMethodEditDialog, PaymentMethodForm>("支払方法追加", new PaymentMethodForm());
        if (form is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var now = UtcNow;
            var entity = FormMapper.ToPaymentMethodEntity(form);
            entity.Id = Guid.CreateVersion7();
            entity.CreatedAt = now;
            entity.UpdatedAt = now;
            entity.Version = 1;
            if (await IsSecondPointsMethodAsync(entity.Id, form))
            {
                return;
            }

            await PaymentMethodAccessor.InsertAsync(entity, CancellationToken);
            Snackbar.AddSuccess("追加しました。");
        }, LoadAsync);
    }

    private async Task EditAsync(PaymentMethodEntity entity)
    {
        var form = await ShowEditDialogAsync<PaymentMethodEditDialog, PaymentMethodForm>("支払方法編集", FormMapper.ToPaymentMethodForm(entity));
        if (form is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            if (await IsSecondPointsMethodAsync(form.Id, form))
            {
                return;
            }

            var rows = await PaymentMethodAccessor.UpdateAsync(form.Id, form.Code, form.Name, form.ShortName, form.Kind, form.AllowsChange, form.RequiresReference, form.IsActive, form.SortOrder, UtcNow, form.Version, CancellationToken);
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

    private async Task DeleteAsync(PaymentMethodEntity entity)
    {
        if (!await ConfirmDeleteAsync(entity.Name))
        {
            return;
        }

        await RunAsync(async () =>
        {
            if (await PaymentMethodAccessor.DeleteAsync(entity.Id, UtcNow, CancellationToken) > 0)
            {
                Snackbar.AddSuccess("削除しました。");
            }
            else
            {
                NotifyNotFound();
            }
        }, LoadAsync);
    }

    // Kind = Points かつ有効な行はちょうど 1 件 (api-design §3.9)
    private async Task<bool> IsSecondPointsMethodAsync(Guid id, PaymentMethodForm form)
    {
        if ((form.Kind == PaymentKind.Points) && form.IsActive && (await PaymentMethodAccessor.CountActivePointsAsync(id, CancellationToken) > 0))
        {
            Snackbar.AddWarning("ポイントの支払方法は 1 件だけ有効にできます。");
            return true;
        }

        return false;
    }
}
