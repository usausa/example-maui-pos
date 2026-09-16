namespace Pos.Terminal.Services;

using Pos.Terminal.Helpers.Data;

using Smart.Data;

// ローカル DB の初期化 (PRAGMA・スキーマ・後から増えた列の追加)
public sealed class DatabaseService
{
    private const string SchemaFile = "Schema.sql";

    private readonly IDbProvider provider;

    private readonly DataAccessor accessor;

    public DatabaseService(
        IDbProvider provider,
        DataAccessor accessor)
    {
        this.provider = provider;
        this.accessor = accessor;
    }

    public async ValueTask InitializeAsync()
    {
        var schema = await ReadSchemaAsync();
        await provider.UsingAsync(async con =>
        {
            await accessor.ExecutePragmaAsync(con);
            await accessor.ExecuteSchemaAsync(con, schema);

            // 後から増えた列 (マスタは次の同期で埋まる)
            await SchemaHelper.EnsureColumnAsync(con, "PaymentMethods", "ShortName", "TEXT");
        });
    }

    // アプリに同梱した SQL ファイル (MauiAsset)
    private static async ValueTask<string> ReadSchemaAsync()
    {
        await using var stream = await FileSystem.Current.OpenAppPackageFileAsync(SchemaFile);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}
