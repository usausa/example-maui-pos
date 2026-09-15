namespace Pos.Server.Host.Components.Dialogs;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using Pos.Server.Host.Application.Lookup;
using Pos.Server.Models.Entity;
using Pos.Server.Models.Views;
using Pos.Server.Services;

// シフト詳細 (精算レポートと同じ内容 + 入出金・金種)
public sealed partial class ShiftDetailDialog
{
    private const int CashEventLimit = 1000;

    private ShiftSummaryView? summary;
    private IReadOnlyList<CashEventEntity> cashEvents = [];

    [Parameter]
    public Guid Id { get; set; }

    [Parameter]
    public required NameLookup Names { get; set; }

    [CascadingParameter]
    public required IMudDialogInstance MudDialog { get; set; }

    [Inject]
    public required ShiftService ShiftService { get; set; }

    protected override async Task OnInitializedAsync()
    {
        summary = await ShiftService.QuerySummaryAsync(Id, CancellationToken.None);
        if (summary is null)
        {
            return;
        }

        cashEvents = (await ShiftService.QueryCashEventPageAsync(Id, 0, CashEventLimit, CancellationToken.None))?.Items ?? [];
    }
}
