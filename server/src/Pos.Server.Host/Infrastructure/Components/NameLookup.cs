namespace Pos.Server.Host.Infrastructure.Components;

using Pos.Server.Accessors;
using Pos.Server.Host.Infrastructure.Api;
using Pos.Server.Models.Entity;

// 一覧・詳細で ID を名称にするための辞書 (店舗・端末・スタッフ・支払方法。削除済みも含む)
public sealed class NameLookup
{
    public Dictionary<Guid, StoreEntity> Stores { get; private init; } = [];

    public Dictionary<Guid, TerminalEntity> Terminals { get; private init; } = [];

    public Dictionary<Guid, StaffEntity> Staff { get; private init; } = [];

    public Dictionary<Guid, PaymentMethodEntity> PaymentMethods { get; private init; } = [];

    public static async ValueTask<NameLookup> LoadAsync(
        StoreAccessor storeAccessor,
        TerminalAccessor terminalAccessor,
        StaffAccessor staffAccessor,
        PaymentMethodAccessor? paymentMethodAccessor,
        CancellationToken cancellationToken) =>
        new()
        {
            Stores = (await storeAccessor.QueryListAsync(null, true, "Code", ApiHelper.MaxPageSize, 0, cancellationToken)).ToDictionary(static x => x.Id),
            Terminals = (await terminalAccessor.QueryListAsync(null, null, true, "StoreId, TerminalNo", ApiHelper.MaxPageSize, 0, cancellationToken)).ToDictionary(static x => x.Id),
            Staff = (await staffAccessor.QueryListAsync(null, null, true, "Code", ApiHelper.MaxPageSize, 0, cancellationToken)).ToDictionary(static x => x.Id),
            PaymentMethods = paymentMethodAccessor is null ? [] : (await paymentMethodAccessor.QueryListAsync(null, true, cancellationToken)).ToDictionary(static x => x.Id)
        };

    public string Store(Guid id) => Stores.TryGetValue(id, out var x) ? x.Name : "-";

    public string Terminal(Guid id) => Terminals.TryGetValue(id, out var x) ? x.Name : "-";

    public string StaffName(Guid? id) => (id is not null) && Staff.TryGetValue(id.Value, out var x) ? x.Name : "-";

    public string PaymentMethod(Guid id) => PaymentMethods.TryGetValue(id, out var x) ? x.Name : "-";

    // 店舗に属する端末 (フィルタ用)
    public IEnumerable<TerminalEntity> TerminalsOf(Guid? storeId) =>
        Terminals.Values.Where(x => !x.IsDeleted && ((storeId is null) || (x.StoreId == storeId))).OrderBy(static x => x.StoreId).ThenBy(static x => x.TerminalNo);
}
