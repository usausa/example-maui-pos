namespace Pos.Server;

using Microsoft.Extensions.DependencyInjection;

using Pos.Domain;
using Pos.Server.Accessors;
using Pos.Server.Host.Infrastructure.Data;
using Pos.Server.Models.Entity;

using Smart.Data;

// 起動時のスキーマ作成・初期データと、SQLite の型変換 (architecture §9-2 / §9-3)
public sealed class DatabaseTests : IClassFixture<TestApplicationFactory>
{
    private readonly TestApplicationFactory factory;

    public DatabaseTests(TestApplicationFactory factory)
    {
        this.factory = factory;
    }

    private T Resolve<T>()
        where T : notnull
        => factory.Services.GetRequiredService<T>();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // 初期データ (architecture §8) が入る
    [Fact]
    public async Task InitialDataIsSeeded()
    {
        var settings = await Resolve<SettingsAccessor>().QueryAsync(Token);
        Assert.NotNull(settings);
        Assert.Equal(TaxRounding.Floor, settings.TaxRounding);
        Assert.Equal(PointBasis.TaxIncluded, settings.PointBasis);

        Assert.NotNull(await Resolve<StoreAccessor>().QueryAsync(InitialData.MainStoreId, Token));
        Assert.NotNull(await Resolve<StoreAccessor>().QueryAsync(InitialData.BranchStoreId, Token));
        Assert.Equal(3, await Resolve<TerminalAccessor>().CountAsync(null, null, false, Token));
        Assert.Equal(4, await Resolve<StaffAccessor>().CountAsync(null, null, false, Token));
        Assert.Equal(13, await Resolve<CategoryAccessor>().CountAsync(null, false, Token));
        Assert.Equal(3, (await Resolve<TaxRateAccessor>().QueryListAsync(null, false, Token)).Count);
        Assert.Equal(6, (await Resolve<PaymentMethodAccessor>().QueryListAsync(null, false, Token)).Count);
        Assert.Equal(33, await Resolve<ProductAccessor>().CountAsync(null, null, null, null, false, Token));
        Assert.Equal(3, (await Resolve<DiscountAccessor>().QueryListAsync(null, false, Token)).Count);
        Assert.Equal(5, (await Resolve<AdjustmentReasonAccessor>().QueryListAsync(null, false, Token)).Count);
        Assert.Equal(5, await Resolve<CustomerAccessor>().CountAsync(null, null, null, null, false, Token));
        Assert.Equal(60, await Resolve<InventoryAccessor>().CountLevelsAsync(null, null, null, false, null, Token));

        // api-design §4.6 の商品 (JAN で引ける)
        var camera = await Resolve<ProductAccessor>().QueryByBarcodeAsync("4901234567894", Token);
        Assert.NotNull(camera);
        Assert.Equal(InitialData.CameraProductId, camera.Id);
        Assert.Equal(80000m, camera.Price);
        Assert.Equal(0.10m, camera.PointRate);
        Assert.Equal(ProductKind.Goods, camera.Kind);
    }

    // 列挙型は TEXT (列挙名)、Guid は TEXT (36 文字、Microsoft.Data.Sqlite の既定で大文字)、日付は yyyy-MM-dd で保存される
    [Fact]
    public async Task ColumnsAreStoredAsText()
    {
        var provider = Resolve<IDbProvider>();
        await using var con = provider.CreateConnection();
        await con.OpenAsync(Token);

        await using var command = con.CreateCommand();
        command.CommandText = "SELECT typeof(Id), Id, typeof(Kind), Kind, typeof(Price), typeof(PointRate), typeof(CreatedAt), CreatedAt FROM Products WHERE Code = 'CAM-X100'";
        await using var reader = await command.ExecuteReaderAsync(Token);
        Assert.True(await reader.ReadAsync(Token));
        Assert.Equal("text", reader.GetString(0));
        Assert.Equal(InitialData.CameraProductId.ToString("D").ToUpperInvariant(), reader.GetString(1));
        Assert.Equal("text", reader.GetString(2));
        Assert.Equal("Goods", reader.GetString(3));
        Assert.Equal("integer", reader.GetString(4));
        Assert.Equal("real", reader.GetString(5));
        Assert.Equal("text", reader.GetString(6));
        Assert.Matches(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{7}$", reader.GetString(7));

        await using var dateCommand = con.CreateCommand();
        dateCommand.CommandText = "SELECT BirthDate FROM Customers WHERE Code = 'M0001'";
        Assert.Equal("1980-04-01", await dateCommand.ExecuteScalarAsync(Token));
    }

    // decimal は NUMERIC で数値として保存され、SUM がそのまま使える (小数の数量を含む)
    [Fact]
    public async Task DecimalRoundTripsAndSums()
    {
        var accessor = Resolve<InventoryAccessor>();
        var provider = Resolve<IDbProvider>();
        var storeId = InitialData.BranchStoreId;
        var productId = InitialData.SdCardProductId;
        var now = DateTime.UtcNow;

        var before = (await accessor.QueryLevelsByProductAsync(productId, Token)).Single(x => x.StoreId == storeId).Quantity;
        await provider.UsingTxAsync(async (_, tx) =>
        {
            var after = await accessor.AddQuantityAsync(tx, storeId, productId, 1.5m, now, Token);
            Assert.Equal(before + 1.5m, after);
            after = await accessor.AddQuantityAsync(tx, storeId, productId, 2.25m, now, Token);
            Assert.Equal(before + 3.75m, after);
            await tx.CommitAsync(Token);
        }, Token);

        await using var con = provider.CreateConnection();
        await con.OpenAsync(Token);
        await using var command = con.CreateCommand();
        command.CommandText = "SELECT SUM(Quantity) FROM InventoryLevels WHERE ProductId = @productId";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@productId";
        parameter.Value = productId;
        command.Parameters.Add(parameter);
        var sum = Convert.ToDecimal(await command.ExecuteScalarAsync(Token), System.Globalization.CultureInfo.InvariantCulture);

        var levels = await accessor.QueryLevelsByProductAsync(productId, Token);
        Assert.Equal(levels.Sum(static x => x.Quantity), sum);
        Assert.Equal(before + 3.75m, levels.Single(x => x.StoreId == storeId).Quantity);
    }

    // 日時は UTC として読み戻される
    [Fact]
    public async Task DateTimeRoundTripsAsUtc()
    {
        var accessor = Resolve<StoreAccessor>();
        var store = await accessor.QueryAsync(InitialData.MainStoreId, Token);

        Assert.NotNull(store);
        Assert.Equal(DateTimeKind.Utc, store.CreatedAt.Kind);

        var entity = new StoreEntity
        {
            Id = Guid.NewGuid(),
            Code = $"T{Guid.NewGuid():N}"[..10],
            Name = "日時テスト",
            TimeZone = "Asia/Tokyo",
            IsActive = true,
            CreatedAt = new DateTime(2026, 9, 11, 3, 15, 0, DateTimeKind.Utc).AddTicks(1234567),
            UpdatedAt = new DateTime(2026, 9, 11, 3, 15, 0, DateTimeKind.Utc).AddTicks(1234567),
            Version = 1
        };
        await accessor.InsertAsync(entity, Token);
        var restored = await accessor.QueryAsync(entity.Id, Token);

        Assert.NotNull(restored);
        Assert.Equal(entity.CreatedAt, restored.CreatedAt);
        Assert.Equal(DateTimeKind.Utc, restored.CreatedAt.Kind);
    }
}
