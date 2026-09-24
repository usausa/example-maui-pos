namespace Pos.Server.Host.Components.Dialogs;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using Pos.Server.Host.Application.Lookup;
using Pos.Server.Models.Views;
using Pos.Server.Services;

// 日次締めの内容 (未締めは取引からの集計、締め済みは締めた内容)。[締める] / [締めを解除] はその日の行を返し、実行はページが行う
public sealed partial class DailyClosingDialog
{
    private DailyClosingSummaryView? summary;

    [Parameter]
    public Guid StoreId { get; set; }

    [Parameter]
    public DateOnly BusinessDate { get; set; }

    [Parameter]
    public required NameLookup Names { get; set; }

    [CascadingParameter]
    public required IMudDialogInstance MudDialog { get; set; }

    [Inject]
    public required DailyClosingService DailyClosingService { get; set; }

    // シフトがあり、すべて精算済み
    private bool CanClose => summary is { Day: { Status: DailyClosingStatus.Open, ShiftCount: > 0, OpenShiftCount: 0 } };

    protected override async Task OnInitializedAsync()
    {
        summary = await DailyClosingService.QuerySummaryAsync(StoreId, BusinessDate, CancellationToken.None);
    }
}
