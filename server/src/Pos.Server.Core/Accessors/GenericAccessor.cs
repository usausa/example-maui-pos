namespace Pos.Server.Accessors;

// テーブルに紐付かない処理: PRAGMA、後から増えた列、SQL ファイルの実行 (スキーマ・初期データ)
[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class GenericAccessor
{
    // WAL は DB ファイルに永続化され、以後の全接続に適用される
    [Execute]
    public partial ValueTask<int> ExecutePragmaAsync(DbConnection con, CancellationToken cancellationToken);

    // 後から増えた列を既存 DB に足す (足したときは true。呼び出し側で初期値を入れる)
    public static ValueTask<bool> EnsurePaymentMethodShortNameAsync(DbConnection con, CancellationToken cancellationToken) =>
        SchemaHelper.EnsureColumnAsync(con, "PaymentMethods", "ShortName", "TEXT", cancellationToken);

    // ShortName 列を後から足した DB に、初期データと同じボタン名を入れる (更新なので端末は差分同期で受け取る)
    [Execute]
    public partial ValueTask<int> BackfillPaymentMethodShortNamesAsync(DateTime now, CancellationToken cancellationToken);

    // スキーマ (Host の Assets/Data/Schema.sql。CREATE TABLE IF NOT EXISTS の複数文) をそのまま実行する
    [DirectSql]
    [Execute]
    public partial ValueTask<int> ExecuteSchemaAsync(DbConnection con, string sql, CancellationToken cancellationToken);

    // SQL ファイルの内容 (複数文) をそのまま実行する。@now を実行時刻に束縛する
    [DirectSql]
    [Execute]
    public partial ValueTask<int> ExecuteScriptAsync(DbTransaction tx, string sql, DateTime now, CancellationToken cancellationToken);
}
