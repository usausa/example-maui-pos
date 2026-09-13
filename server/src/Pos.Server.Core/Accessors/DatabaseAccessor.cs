namespace Pos.Server.Accessors;

[DataAccessor]
public sealed partial class DatabaseAccessor
{
    // WAL は DB ファイルに永続化され、以後の全接続に適用される
    [Execute]
    public partial ValueTask<int> ExecutePragmaAsync(DbConnection con, CancellationToken cancellationToken);
}
