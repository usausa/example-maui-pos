namespace Pos.Server.Host.Components.Dialogs;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using Pos.Server.Accessors;
using Pos.Server.Host.Infrastructure.Api;
using Pos.Server.Host.Infrastructure.Components;
using Pos.Server.Host.Mappers;
using Pos.Server.Models.Entity;
using Pos.Shared.Shifts;

// S-31 シフト詳細 (精算レポートと同じ内容 + 入出金・金種)
public sealed partial class ShiftDetailDialog
{
    private ShiftSummaryResponse? summary;

    private List<CashEventEntity> cashEvents = [];

    [Parameter]
    public Guid Id { get; set; }

    [Parameter]
    public required NameLookup Names { get; set; }

    [CascadingParameter]
    public required IMudDialogInstance MudDialog { get; set; }

    [Inject]
    public required ShiftAccessor ShiftAccessor { get; set; }

    protected override async Task OnInitializedAsync()
    {
        var entity = await ShiftAccessor.QueryAsync(Id, CancellationToken.None);
        if (entity is null)
        {
            return;
        }

        summary = await ShiftMapper.ToSummaryResponseAsync(ShiftAccessor, entity, CancellationToken.None);
        cashEvents = await ShiftAccessor.QueryCashEventListAsync(Id, ApiHelper.MaxPageSize, 0, CancellationToken.None);
    }
}
