namespace Pos.Server.Accessors;

using Pos.Server.Infrastructure.Data;
using Pos.Server.Models.Entity;

[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class SettingsAccessor
{
    [Execute]
    public partial void Create();

    // 1 行だけ (Id = 1)
    [QueryFirst]
    public partial ValueTask<SettingsEntity?> QueryAsync(CancellationToken cancellationToken);

    [Execute]
    [Insert(typeof(SettingsEntity), Table = "Settings")]
    public partial ValueTask<int> InsertAsync(SettingsEntity entity, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> UpdateAsync(
        string companyName,
        string currency,
        TaxRounding taxRounding,
        PointBasis pointBasis,
        string businessDayStartTime,
        DateTime updatedAt,
        int version,
        CancellationToken cancellationToken);
}
