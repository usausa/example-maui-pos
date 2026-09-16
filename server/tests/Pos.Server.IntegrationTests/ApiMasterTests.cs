namespace Pos.Server;

using System.Text.Json;

using Pos.Contract.Categories;
using Pos.Contract.Customers;
using Pos.Contract.Products;
using Pos.Contract.Settings;
using Pos.Contract.Sync;
using Pos.Server.Host.Endpoints;

// マスタ・設定・同期・顧客の API
public sealed class ApiMasterTests : IClassFixture<TestApplicationFactory>
{
    private readonly TestApplicationFactory factory;

    private readonly JsonSerializerOptions options;

    public ApiMasterTests(TestApplicationFactory factory)
    {
        this.factory = factory;
        options = factory.JsonOptions();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // 全件同期 → serverTime 以降の差分は空
    [Fact]
    public async Task SyncMastersReturnsAllThenDelta()
    {
        var client = factory.CreateClient();

        var all = await client.GetJsonAsync<SyncMastersResponse>($"{ApiRoutes.Sync}/masters", options);
        Assert.NotNull(all.Settings);
        Assert.Equal(2, all.Stores.Count);
        Assert.Equal(3, all.Terminals.Count);
        Assert.Equal(4, all.Staff.Count);
        // 全件同期は削除済みも返す。同じ DB を使う CategoryCrudAndConflicts が登録して論理削除した部門は除いて数える
        Assert.Equal(13, all.Categories.Count(x => !x.IsDeleted));
        Assert.Equal(3, all.TaxRates.Count);
        Assert.Equal(33, all.Products.Count);
        Assert.Equal(3, all.Discounts.Count);
        Assert.Equal(6, all.PaymentMethods.Count);
        Assert.Equal(5, all.AdjustmentReasons.Count);
        Assert.False(all.ProductsTruncated);

        var delta = await client.GetJsonAsync<SyncMastersResponse>($"{ApiRoutes.Sync}/masters?since={all.ServerTime:yyyy-MM-ddTHH:mm:ss.fffZ}", options);
        Assert.Null(delta.Settings);
        Assert.Empty(delta.Products);
        Assert.Empty(delta.Stores);
        Assert.True(delta.ServerTime >= all.ServerTime);
    }

    // 登録 → コード重複 409 → 更新 (楽観ロック) → 使用中の削除 422 → 削除 204 → 404
    [Fact]
    public async Task CategoryCrudAndConflicts()
    {
        var client = factory.CreateClient();
        var code = $"T{Guid.NewGuid():N}"[..10];

        using var createResponse = await client.PostJsonAsync(ApiRoutes.Categories, new CategoryCreateRequest { Code = code, Name = "テスト部門", SortOrder = 99 }, options);
        var created = await createResponse.ReadAsAsync<CategoryResponseItem>(HttpStatusCode.Created, options);
        Assert.Equal($"{ApiRoutes.Categories}/{created.Id}", createResponse.Headers.Location?.ToString());
        Assert.Equal(1, created.Version);

        using var duplicateResponse = await client.PostJsonAsync(ApiRoutes.Categories, new CategoryCreateRequest { Code = code, Name = "重複", SortOrder = 1 }, options);
        await duplicateResponse.ReadProblemAsync(HttpStatusCode.Conflict, "DUPLICATE_CODE", options);

        using var updateResponse = await client.PutJsonAsync($"{ApiRoutes.Categories}/{created.Id}", new CategoryUpdateRequest { Code = code, Name = "更新後", SortOrder = 99, Version = 1 }, options);
        var updated = await updateResponse.ReadAsAsync<CategoryResponseItem>(HttpStatusCode.OK, options);
        Assert.Equal("更新後", updated.Name);
        Assert.Equal(2, updated.Version);

        using var staleResponse = await client.PutJsonAsync($"{ApiRoutes.Categories}/{created.Id}", new CategoryUpdateRequest { Code = code, Name = "古い版", SortOrder = 99, Version = 1 }, options);
        await staleResponse.ReadProblemAsync(HttpStatusCode.Conflict, "VERSION_MISMATCH", options);

        using var inUseResponse = await client.DeleteUrlAsync($"{ApiRoutes.Categories}/00000000-0000-0000-0004-000000000015");
        await inUseResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "IN_USE", options);

        using var deleteResponse = await client.DeleteUrlAsync($"{ApiRoutes.Categories}/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // 論理削除後も id 指定では isDeleted: true で取得でき、更新はできない
        Assert.True((await client.GetJsonAsync<CategoryResponseItem>($"{ApiRoutes.Categories}/{created.Id}", options)).IsDeleted);
        using var updateDeletedResponse = await client.PutJsonAsync($"{ApiRoutes.Categories}/{created.Id}", new CategoryUpdateRequest { Code = code, Name = "削除済み", SortOrder = 99, Version = 3 }, options);
        await updateDeletedResponse.ReadProblemAsync(HttpStatusCode.NotFound, "NOT_FOUND", options);
        using var getResponse = await client.GetAsync(new Uri($"{ApiRoutes.Categories}/{Guid.NewGuid()}", UriKind.Relative), Token);
        await getResponse.ReadProblemAsync(HttpStatusCode.NotFound, "NOT_FOUND", options);

        var list = await client.GetJsonAsync<CategoryResponse>($"{ApiRoutes.Categories}?includeDeleted=true&size=100", options);
        Assert.Contains(list.Items, x => (x.Id == created.Id) && x.IsDeleted);
        var active = await client.GetJsonAsync<CategoryResponse>($"{ApiRoutes.Categories}?size=100", options);
        Assert.DoesNotContain(active.Items, x => x.Id == created.Id);
    }

    // 入力検証は 400 + VALIDATION_ERROR + errors
    [Fact]
    public async Task ValidationErrorReturnsProblem()
    {
        var client = factory.CreateClient();

        using var response = await client.PostJsonAsync(ApiRoutes.Categories, new CategoryCreateRequest { Code = string.Empty, Name = string.Empty, SortOrder = 1 }, options);
        var problem = await response.ReadProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_ERROR", options);

        Assert.NotNull(problem.Errors);
        Assert.NotEmpty(problem.Errors);
    }

    [Fact]
    public async Task ProductLookupAndList()
    {
        var client = factory.CreateClient();

        var camera = await client.GetJsonAsync<ProductResponseItem>($"{ApiRoutes.Products}/lookup?barcode=4901234567894", options);
        Assert.Equal("CAM-X100", camera.Code);
        Assert.Equal(TestData.CameraProductId, camera.Id);

        var sdCard = await client.GetJsonAsync<ProductResponseItem>($"{ApiRoutes.Products}/lookup?code=SD-64", options);
        Assert.Equal(TestData.SdCardProductId, sdCard.Id);

        using var missing = await client.GetAsync(new Uri($"{ApiRoutes.Products}/lookup?barcode=0000000000000", UriKind.Relative), Token);
        await missing.ReadProblemAsync(HttpStatusCode.NotFound, "NOT_FOUND", options);

        var page = await client.GetJsonAsync<ProductResponse>($"{ApiRoutes.Products}?keyword=カメラ&page=0&size=2", options);
        Assert.Equal(2, page.Items.Count);
        Assert.True(page.Total > 2);
        Assert.All(page.Items, x => Assert.Contains("カメラ", x.Name, StringComparison.Ordinal));
    }

    [Fact]
    public async Task SettingsUpdateUsesVersion()
    {
        var client = factory.CreateClient();

        var settings = await client.GetJsonAsync<SettingsResponse>(ApiRoutes.Settings, options);
        Assert.Equal("うさぎ電機", settings.CompanyName);
        Assert.Equal(TaxRounding.Floor, settings.TaxRounding);

        using var updateResponse = await client.PutJsonAsync(ApiRoutes.Settings, new SettingsUpdateRequest { CompanyName = settings.CompanyName, Currency = "JPY", TaxRounding = TaxRounding.Floor, PointBasis = PointBasis.TaxIncluded, BusinessDayStartTime = "06:00", Version = settings.Version }, options);
        var updated = await updateResponse.ReadAsAsync<SettingsResponse>(HttpStatusCode.OK, options);
        Assert.Equal("06:00", updated.BusinessDayStartTime);
        Assert.Equal(settings.Version + 1, updated.Version);

        using var staleResponse = await client.PutJsonAsync(ApiRoutes.Settings, new SettingsUpdateRequest { CompanyName = settings.CompanyName, Currency = "JPY", TaxRounding = TaxRounding.Floor, PointBasis = PointBasis.TaxIncluded, BusinessDayStartTime = "05:00", Version = settings.Version }, options);
        await staleResponse.ReadProblemAsync(HttpStatusCode.Conflict, "VERSION_MISMATCH", options);
    }

    // 会員照会 → 手動調整 → 履歴
    [Fact]
    public async Task CustomerPointsAdjust()
    {
        var client = factory.CreateClient();

        var customer = await client.GetJsonAsync<CustomerResponseItem>($"{ApiRoutes.Customers}/lookup?code=M0003", options);
        Assert.Equal(500, customer.PointBalance);

        using var adjustResponse = await client.PostJsonAsync($"{ApiRoutes.Customers}/{customer.Id}/points/adjust", new CustomerPointAdjustRequest { Points = 100, Reason = "キャンペーン", StaffId = TestData.ManagerStaffId }, options);
        var adjusted = await adjustResponse.ReadAsAsync<CustomerPointHistoryResponseItem>(HttpStatusCode.OK, options);
        Assert.Equal(PointHistoryType.Adjust, adjusted.Type);
        Assert.Equal(600, adjusted.BalanceAfter);
        Assert.Equal(600, (await client.GetJsonAsync<CustomerResponseItem>($"{ApiRoutes.Customers}/{customer.Id}", options)).PointBalance);

        var history = await client.GetJsonAsync<CustomerPointHistoryResponse>($"{ApiRoutes.Customers}/{customer.Id}/points/history", options);
        Assert.Equal(2, history.Total);
        Assert.Equal(600, history.Items[0].BalanceAfter);
        Assert.All(history.Items, x => Assert.Equal(PointHistoryType.Adjust, x.Type));

        using var missing = await client.GetAsync(new Uri($"{ApiRoutes.Customers}/lookup?code=X9999", UriKind.Relative), Token);
        await missing.ReadProblemAsync(HttpStatusCode.NotFound, "NOT_FOUND", options);
    }
}
