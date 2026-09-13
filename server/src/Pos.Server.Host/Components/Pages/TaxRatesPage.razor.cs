namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;

using Pos.Server.Accessors;
using Pos.Server.Host.Components.Dialogs;
using Pos.Server.Host.Infrastructure.Components;
using Pos.Server.Host.Mappers;
using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Entity;

// S-54 税率
public sealed partial class TaxRatesPage
{
    private List<TaxRateEntity> items = [];

    private bool includeDeleted;

    [Inject]
    public required TaxRateAccessor TaxRateAccessor { get; set; }

    protected override Task OnInitializedAsync() => LoadAsync();

    private Task LoadAsync() =>
        LoadAsync(async () =>
        {
            items = await TaxRateAccessor.QueryListAsync(null, includeDeleted, CancellationToken);
        });

    private async Task AddAsync()
    {
        var form = await ShowEditDialogAsync<TaxRateEditDialog, TaxRateForm>("税率追加", new TaxRateForm());
        if (form is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var now = UtcNow;
            var entity = FormMapper.ToTaxRateEntity(form);
            entity.Id = Guid.CreateVersion7();
            entity.CreatedAt = now;
            entity.UpdatedAt = now;
            entity.Version = 1;
            await TaxRateAccessor.InsertAsync(entity, CancellationToken);
            if (entity.IsDefault)
            {
                await TaxRateAccessor.ClearDefaultAsync(entity.Id, now, CancellationToken);
            }

            Snackbar.AddSuccess("追加しました。");
        }, LoadAsync);
    }

    private async Task EditAsync(TaxRateEntity entity)
    {
        var form = await ShowEditDialogAsync<TaxRateEditDialog, TaxRateForm>("税率編集", FormMapper.ToTaxRateForm(entity));
        if (form is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var now = UtcNow;
            var rows = await TaxRateAccessor.UpdateAsync(form.Id, form.Code, form.Name, form.Rate, form.Kind, form.IsDefault, form.SortOrder, now, form.Version, CancellationToken);
            if (rows == 0)
            {
                NotifyVersionMismatch();
                return;
            }

            // 既定は 1 件だけ (api-design §3.6)
            if (form.IsDefault)
            {
                await TaxRateAccessor.ClearDefaultAsync(form.Id, now, CancellationToken);
            }

            Snackbar.AddSuccess("更新しました。");
        }, LoadAsync);
    }

    private async Task DeleteAsync(TaxRateEntity entity)
    {
        if (!await ConfirmDeleteAsync(entity.Name))
        {
            return;
        }

        await RunAsync(async () =>
        {
            if (await TaxRateAccessor.CountProductsAsync(entity.Id, CancellationToken) > 0)
            {
                Snackbar.AddWarning("使用中の商品がある税率は削除できません。");
                return;
            }

            if (await TaxRateAccessor.DeleteAsync(entity.Id, UtcNow, CancellationToken) > 0)
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
