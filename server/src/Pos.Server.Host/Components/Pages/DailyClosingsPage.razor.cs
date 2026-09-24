namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using Pos.Server.Host.Application.Lookup;
using Pos.Server.Host.Application.State;
using Pos.Server.Host.Components.Dialogs;
using Pos.Server.Host.Helpers;
using Pos.Server.Models.Parameters;
using Pos.Server.Models.Views;
using Pos.Server.Services;

// 日次締め (店舗 × 営業日の一覧から締め・解除する)
public sealed partial class DailyClosingsPage
{
    private MudDataGrid<DailyClosingDayView> Grid { get; set; } = default!;

    private NameLookup names = new();
    private DateRange? period;
    private Guid? storeId;
    private DailyClosingStatus? status;

    [Inject]
    public required DailyClosingService DailyClosingService { get; set; }

    [Inject]
    public required ReportService ReportService { get; set; }

    [Inject]
    public required StoreService StoreService { get; set; }

    [Inject]
    public required TerminalService TerminalService { get; set; }

    [Inject]
    public required StaffService StaffService { get; set; }

    [Inject]
    public required StoreFilterState StoreFilter { get; set; }

    // ダッシュボードの未締めから開いたとき
    [SupplyParameterFromQuery(Name = "unclosed")]
    public bool Unclosed { get; set; }

    protected override Task OnInitializedAsync()
    {
        storeId = StoreFilter.StoreId;
        // 既定は直近 30 日。未締めから開いたときは期間で絞らない (古い未締めも漏らさない)
        if (Unclosed)
        {
            status = DailyClosingStatus.Open;
        }
        else
        {
            var (start, end) = ReportService.ResolvePeriod(null, null);
            period = new DateRange(start.ToDateTime(TimeOnly.MinValue), end.ToDateTime(TimeOnly.MinValue));
        }

        return LoadAsync(async () =>
        {
            names = await NameLookup.LoadAsync(StoreService, TerminalService, StaffService, null, CancellationToken);
        });
    }

    //--------------------------------------------------------------------------------
    // Grid
    //--------------------------------------------------------------------------------

    // 締め済みは締めた時点の日計、未締めは取引からの集計
    private async Task<GridData<DailyClosingDayView>> LoadServerData(GridState<DailyClosingDayView> state, CancellationToken cancellationToken)
    {
        var sort = state.SortDefinitions.FirstOrDefault();
        var parameter = new DailyClosingQueryParameter
        {
            StoreId = storeId,
            Status = status,
            From = ToDateOnly(period?.Start),
            To = ToDateOnly(period?.End),
            Sort = EnumHelper.Parse(sort?.SortBy, DailyClosingSort.BusinessDate),
            Desc = sort?.Descending ?? true,
            Page = state.Page,
            Size = state.PageSize
        };
        var result = await DailyClosingService.QueryDayPageAsync(parameter, cancellationToken);
        return new GridData<DailyClosingDayView> { TotalItems = result.Total, Items = result.Items };
    }

    private static DateOnly? ToDateOnly(DateTime? value) => value is null ? null : DateOnly.FromDateTime(value.Value);

    private Task SearchAsync() => Grid.ReloadServerData();

    private Task OnStoreChanged(Guid? value)
    {
        storeId = value;
        StoreFilter.StoreId = value;
        return SearchAsync();
    }

    //--------------------------------------------------------------------------------
    // Close / Reopen
    //--------------------------------------------------------------------------------

    // 内容を確認するダイアログで [締める] / [締めを解除] を選んだら実行する
    private async Task OnRowClick(DataGridRowClickEventArgs<DailyClosingDayView> args)
    {
        var reference = await DialogService.ShowAsync<DailyClosingDialog>(
            string.Empty,
            new DialogParameters
            {
                { nameof(DailyClosingDialog.StoreId), args.Item.StoreId },
                { nameof(DailyClosingDialog.BusinessDate), args.Item.BusinessDate },
                { nameof(DailyClosingDialog.Names), names }
            },
            Styles.LargeDialog);
        var result = await reference.Result;
        if (result is not { Canceled: false, Data: DailyClosingDayView day })
        {
            return;
        }

        if (day.Status == DailyClosingStatus.Open)
        {
            await CloseAsync(day);
        }
        else
        {
            await ReopenAsync(day);
        }
    }

    private Task CloseAsync(DailyClosingDayView day) =>
        RunAsync(async () =>
        {
            // 認証の導入時: 締めた人 (closedBy) にログイン中のアカウント名を渡す
            var result = await DailyClosingService.CloseAsync(day.StoreId, day.BusinessDate, null, CancellationToken);
            switch (result.Status)
            {
                case DailyClosingResultStatus.Success:
                    Snackbar.AddSuccess($"{names.Store(day.StoreId)} {day.BusinessDate.ToDateText()} を締めました。");
                    break;
                case DailyClosingResultStatus.NoShift:
                    Snackbar.AddWarning("この営業日のシフトがありません。");
                    break;
                case DailyClosingResultStatus.ShiftStillOpen:
                    Snackbar.AddWarning("未精算のシフトがあります。精算してから締めてください。");
                    break;
                default:
                    Snackbar.AddWarning("既に締め済みです。");
                    break;
            }
        }, SearchAsync);

    // 認証の導入時: 締め解除は Administrator に限る (ダイアログのボタンも Administrator だけに出す)
    private async Task ReopenAsync(DailyClosingDayView day)
    {
        if (!await DialogService.ShowConfirm("締め解除", $"{names.Store(day.StoreId)} {day.BusinessDate.ToDateText()} の締めを解除しますか？ 解除すると、この営業日の取引を取消できるようになります。"))
        {
            return;
        }

        await RunAsync(async () =>
        {
            if (await DailyClosingService.ReopenAsync(day.Id!.Value, CancellationToken))
            {
                Snackbar.AddSuccess("締めを解除しました。");
            }
            else
            {
                Snackbar.AddError("対象が存在しません。");
            }
        }, SearchAsync);
    }
}
