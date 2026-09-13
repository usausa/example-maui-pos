namespace Pos.Terminal.State;

using Pos.Terminal.Models.Entity;

// 選択中スタッフ・開設中シフト・未送信件数 (タイトルバーに表示)
#pragma warning disable CA1724
public sealed partial class Session : ObservableObject
{
    [ObservableProperty]
    public partial SettingsResponse? CompanySettings { get; set; }

    [ObservableProperty(NotifyAlso = [nameof(HeaderText)])]
    public partial StoreResponse? Store { get; set; }

    [ObservableProperty(NotifyAlso = [nameof(HeaderText)])]
    public partial TerminalResponse? Terminal { get; set; }

    [ObservableProperty(NotifyAlso = [nameof(HeaderText)])]
    public partial StaffResponse? Staff { get; set; }

    [ObservableProperty(NotifyAlso = [nameof(IsShiftOpen)])]
    public partial LocalShiftEntity? CurrentShift { get; set; }

    // 未送信 (Pending) と要確認 (Failed) の件数
    [ObservableProperty]
    public partial int UnsentCount { get; set; }

    [ObservableProperty]
    public partial int FailedCount { get; set; }

    [ObservableProperty]
    public partial DateTime? LastSyncAt { get; set; }

    public bool IsShiftOpen => CurrentShift is { Status: ShiftStatus.Open };

    public TaxRounding TaxRounding => CompanySettings?.TaxRounding ?? TaxRounding.Floor;

    public PointBasis PointBasis => CompanySettings?.PointBasis ?? PointBasis.TaxIncluded;

    // 営業日: 営業日切替時刻 (既定 05:00) より前は前日
    public DateOnly BusinessDate
    {
        get
        {
            var now = DateTime.Now;
            var start = TimeOnly.TryParseExact(CompanySettings?.BusinessDayStartTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) ? time : new TimeOnly(5, 0);
            var date = DateOnly.FromDateTime(now);
            return TimeOnly.FromDateTime(now) < start ? date.AddDays(-1) : date;
        }
    }

    // タイトルバー右側: 店舗コード-端末番号 担当
    public string HeaderText =>
        Store is null || Terminal is null ? string.Empty : $"{Store.Code}-{Terminal.TerminalNo:00} {Staff?.Name ?? string.Empty}".Trim();
}
#pragma warning restore CA1724
