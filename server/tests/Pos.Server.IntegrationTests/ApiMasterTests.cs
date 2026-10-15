namespace Pos.Server;

using System.Text;
using System.Text.Json;

using Pos.Contract.Categories;
using Pos.Contract.Customers;
using Pos.Contract.Products;
using Pos.Contract.Settings;
using Pos.Contract.Staff;
using Pos.Contract.Sync;
using Pos.Contract.Terminals;
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
        // Arrange
        var client = await factory.CreateAdminClientAsync();

        // Act / Assert: 全件
        var all = await client.GetJsonAsync<SyncMastersResponse>($"{ApiRoutes.Sync}/masters", options);
        Assert.NotNull(all.Settings);
        Assert.Equal(2, all.Stores.Count);
        Assert.Equal(3, all.Terminals.Count);
        // 全件同期は削除済みも返す。同じ DB の他のテストが登録して論理削除した部門と担当は除いて数える
        Assert.Equal(4, all.Staff.Count(x => !x.IsDeleted));
        Assert.Equal(13, all.Categories.Count(x => !x.IsDeleted));
        Assert.Equal(3, all.TaxRates.Count);
        Assert.Equal(33, all.Products.Count);
        Assert.Equal(3, all.Discounts.Count);
        Assert.Equal(7, all.PaymentMethods.Count);
        Assert.Equal(5, all.AdjustmentReasons.Count);
        Assert.False(all.ProductsTruncated);

        // Act / Assert: serverTime 以降の差分は空
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
        // Arrange
        var client = await factory.CreateAdminClientAsync();
        var code = $"T{Guid.NewGuid():N}"[..10];

        // Act / Assert: 登録
        using var createResponse = await client.PostJsonAsync(ApiRoutes.Categories, new CategoryCreateRequest { Code = code, Name = "テスト部門", SortOrder = 99 }, options);
        var created = await createResponse.ReadAsAsync<CategoryListResponseItem>(HttpStatusCode.Created, options);
        Assert.Equal($"{ApiRoutes.Categories}/{created.Id}", createResponse.Headers.Location?.ToString());
        Assert.Equal(1, created.Version);

        // Act / Assert: コードの重複は 409
        using var duplicateResponse = await client.PostJsonAsync(ApiRoutes.Categories, new CategoryCreateRequest { Code = code, Name = "重複", SortOrder = 1 }, options);
        await duplicateResponse.ReadProblemAsync(HttpStatusCode.Conflict, "DUPLICATE_CODE", options);

        // Act / Assert: 更新は版が上がり、古い版での更新は 409
        using var updateResponse = await client.PutJsonAsync($"{ApiRoutes.Categories}/{created.Id}", new CategoryUpdateRequest { Code = code, Name = "更新後", SortOrder = 99, Version = 1 }, options);
        var updated = await updateResponse.ReadAsAsync<CategoryListResponseItem>(HttpStatusCode.OK, options);
        Assert.Equal("更新後", updated.Name);
        Assert.Equal(2, updated.Version);
        using var staleResponse = await client.PutJsonAsync($"{ApiRoutes.Categories}/{created.Id}", new CategoryUpdateRequest { Code = code, Name = "古い版", SortOrder = 99, Version = 1 }, options);
        await staleResponse.ReadProblemAsync(HttpStatusCode.Conflict, "VERSION_MISMATCH", options);

        // Act / Assert: 商品が使っている部門の削除は 422
        using var inUseResponse = await client.DeleteUrlAsync($"{ApiRoutes.Categories}/{TestData.CameraCategoryId}");
        await inUseResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "IN_USE", options);

        // Act / Assert: 削除は 204
        using var deleteResponse = await client.DeleteUrlAsync($"{ApiRoutes.Categories}/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Act / Assert: 論理削除後も id 指定では isDeleted: true で取得でき、更新はできない (ない id は 404)
        Assert.True((await client.GetJsonAsync<CategoryListResponseItem>($"{ApiRoutes.Categories}/{created.Id}", options)).IsDeleted);
        using var updateDeletedResponse = await client.PutJsonAsync($"{ApiRoutes.Categories}/{created.Id}", new CategoryUpdateRequest { Code = code, Name = "削除済み", SortOrder = 99, Version = 3 }, options);
        await updateDeletedResponse.ReadProblemAsync(HttpStatusCode.NotFound, "NOT_FOUND", options);
        using var getResponse = await client.GetAsync(new Uri($"{ApiRoutes.Categories}/{Guid.NewGuid()}", UriKind.Relative), Token);
        await getResponse.ReadProblemAsync(HttpStatusCode.NotFound, "NOT_FOUND", options);

        // Act / Assert: 一覧は削除済みを含めたときだけ載る
        var list = await client.GetJsonAsync<CategoryListResponse>($"{ApiRoutes.Categories}?includeDeleted=true&size=100", options);
        Assert.Contains(list.Items, x => (x.Id == created.Id) && x.IsDeleted);
        var active = await client.GetJsonAsync<CategoryListResponse>($"{ApiRoutes.Categories}?size=100", options);
        Assert.DoesNotContain(active.Items, x => x.Id == created.Id);
    }

    // 参照先 (親部門、店舗、部門、税率) が見つからない書き込みは、重複ではなく 422 (VALIDATION_ERROR)。削除済みも見つからないものとする
    [Fact]
    public async Task WriteWithMissingReferenceReturnsUnprocessable()
    {
        // Arrange
        var client = await factory.CreateAdminClientAsync();
        var missingId = Guid.NewGuid();
        using var deletedResponse = await client.PostJsonAsync(ApiRoutes.Categories, new CategoryCreateRequest { Code = $"T{Guid.NewGuid():N}"[..10], Name = "削除する部門", SortOrder = 99 }, options);
        var deleted = await deletedResponse.ReadAsAsync<CategoryListResponseItem>(HttpStatusCode.Created, options);
        using var deleteResponse = await client.DeleteUrlAsync($"{ApiRoutes.Categories}/{deleted.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Act / Assert: 部門の親部門 (ない部門と削除済みの部門)
        using var categoryResponse = await client.PostJsonAsync(ApiRoutes.Categories, new CategoryCreateRequest { Code = $"T{Guid.NewGuid():N}"[..10], Name = "親部門なし", ParentId = missingId, SortOrder = 99 }, options);
        await categoryResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "VALIDATION_ERROR", options);
        using var deletedParentResponse = await client.PostJsonAsync(ApiRoutes.Categories, new CategoryCreateRequest { Code = $"T{Guid.NewGuid():N}"[..10], Name = "削除済みの親部門", ParentId = deleted.Id, SortOrder = 99 }, options);
        await deletedParentResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "VALIDATION_ERROR", options);

        // Act / Assert: 担当と端末の店舗 (端末は更新も)
        using var staffResponse = await client.PostJsonAsync(ApiRoutes.Staff, new StaffCreateRequest { Code = $"T{Guid.NewGuid():N}"[..10], Name = "店舗なし", Role = StaffRole.Cashier, StoreId = missingId }, options);
        await staffResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "VALIDATION_ERROR", options);
        using var terminalResponse = await client.PostJsonAsync(ApiRoutes.Terminals, new TerminalCreateRequest { StoreId = missingId, TerminalNo = 99, Name = "店舗なし" }, options);
        await terminalResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "VALIDATION_ERROR", options);
        using var terminalUpdateResponse = await client.PutJsonAsync($"{ApiRoutes.Terminals}/{TestData.MainTerminal2Id}", new TerminalUpdateRequest { StoreId = missingId, TerminalNo = 2, Name = "店舗なし", IsActive = true, Version = 1 }, options);
        await terminalUpdateResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "VALIDATION_ERROR", options);

        // Act / Assert: 商品の部門と税率
        using var productResponse = await client.PostJsonAsync(ApiRoutes.Products, new ProductCreateRequest { Code = $"T{Guid.NewGuid():N}"[..10], Name = "部門なし", CategoryId = missingId, Kind = ProductKind.Goods, Price = 100m, TaxRateId = TestData.StandardTaxRateId }, options);
        await productResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "VALIDATION_ERROR", options);
        using var taxRateResponse = await client.PostJsonAsync(ApiRoutes.Products, new ProductCreateRequest { Code = $"T{Guid.NewGuid():N}"[..10], Name = "税率なし", CategoryId = TestData.CameraCategoryId, Kind = ProductKind.Goods, Price = 100m, TaxRateId = missingId }, options);
        await taxRateResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "VALIDATION_ERROR", options);
    }

    // 入力検証は 400 + VALIDATION_ERROR + errors
    [Fact]
    public async Task ValidationErrorReturnsProblem()
    {
        // Arrange
        var client = await factory.CreateAdminClientAsync();

        // Act
        using var response = await client.PostJsonAsync(ApiRoutes.Categories, new CategoryCreateRequest { Code = string.Empty, Name = string.Empty, SortOrder = 1 }, options);

        // Assert
        var problem = await response.ReadProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_ERROR", options);
        Assert.NotNull(problem.Errors);
        Assert.NotEmpty(problem.Errors);
    }

    // 壊れた JSON の本文も 400 + VALIDATION_ERROR (開発環境では読み取りの失敗が例外になっても 500 にしない)
    [Fact]
    public async Task MalformedJsonReturnsValidationProblem()
    {
        // Arrange
        var client = await factory.CreateAdminClientAsync();
        using var content = new StringContent("{", Encoding.UTF8, "application/json");

        // Act
        using var response = await client.PostAsync(new Uri(ApiRoutes.Categories, UriKind.Relative), content, Token);

        // Assert
        await response.ReadProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_ERROR", options);
    }

    // 商品はバーコードとコードで引け (ないものは 404)、キーワードで一覧をページで取れる
    [Fact]
    public async Task ProductLookupAndList()
    {
        // Arrange
        var client = await factory.CreateAdminClientAsync();

        // Act / Assert: バーコードとコードで引ける
        var camera = await client.GetJsonAsync<ProductListResponseItem>($"{ApiRoutes.Products}/lookup?barcode=4901234567894", options);
        Assert.Equal("CAM-X100", camera.Code);
        Assert.Equal(TestData.CameraProductId, camera.Id);

        var sdCard = await client.GetJsonAsync<ProductListResponseItem>($"{ApiRoutes.Products}/lookup?code=SD-64", options);
        Assert.Equal(TestData.SdCardProductId, sdCard.Id);

        // Act / Assert: ないものは 404
        using var missing = await client.GetAsync(new Uri($"{ApiRoutes.Products}/lookup?barcode=0000000000000", UriKind.Relative), Token);
        await missing.ReadProblemAsync(HttpStatusCode.NotFound, "NOT_FOUND", options);

        // Act / Assert: キーワードで一覧をページで取れる
        var page = await client.GetJsonAsync<ProductListResponse>($"{ApiRoutes.Products}?keyword=カメラ&page=0&size=2", options);
        Assert.Equal(2, page.Items.Count);
        Assert.True(page.Total > 2);
        Assert.All(page.Items, x => Assert.Contains("カメラ", x.Name, StringComparison.Ordinal));
    }

    // lookup はキーがない・空なら 400 + VALIDATION_ERROR (商品は barcode か code、会員は code)。入力検証の title は日本語
    [Theory]
    [InlineData(ApiRoutes.Products + "/lookup")]
    [InlineData(ApiRoutes.Products + "/lookup?barcode=&code=")]
    [InlineData(ApiRoutes.Customers + "/lookup")]
    [InlineData(ApiRoutes.Customers + "/lookup?code=")]
    public async Task LookupWithoutKeyReturnsBadRequest(string path)
    {
        // Arrange
        var client = await factory.CreateAdminClientAsync();

        // Act
        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), Token);

        // Assert
        var problem = await response.ReadProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_ERROR", options);
        Assert.Equal("入力に誤りがあります", problem.Title);
    }

    // 会社設定の更新は版で守り、古い版での更新は 409
    [Fact]
    public async Task SettingsUpdateUsesVersion()
    {
        // Arrange
        var client = await factory.CreateAdminClientAsync();

        // Act / Assert: 初期データの設定
        var settings = await client.GetJsonAsync<SettingsResponse>(ApiRoutes.Settings, options);
        Assert.Equal("うさぎ電機", settings.CompanyName);
        Assert.Equal(TaxRounding.Floor, settings.TaxRounding);

        // Act / Assert: 今の版で更新すると版が進む
        using var updateResponse = await client.PutJsonAsync(ApiRoutes.Settings, new SettingsUpdateRequest { CompanyName = settings.CompanyName, Currency = "JPY", TaxRounding = TaxRounding.Floor, PointBasis = PointBasis.TaxIncluded, BusinessDayStartTime = "06:00", Version = settings.Version }, options);
        var updated = await updateResponse.ReadAsAsync<SettingsResponse>(HttpStatusCode.OK, options);
        Assert.Equal("06:00", updated.BusinessDayStartTime);
        Assert.Equal(settings.Version + 1, updated.Version);

        // Act / Assert: 古い版の更新は 409
        using var staleResponse = await client.PutJsonAsync(ApiRoutes.Settings, new SettingsUpdateRequest { CompanyName = settings.CompanyName, Currency = "JPY", TaxRounding = TaxRounding.Floor, PointBasis = PointBasis.TaxIncluded, BusinessDayStartTime = "05:00", Version = settings.Version }, options);
        await staleResponse.ReadProblemAsync(HttpStatusCode.Conflict, "VERSION_MISMATCH", options);
    }

    // 会員照会 → 手動調整 → 履歴。担当が見つからない・削除済みの調整は 422 (管理の入力)
    [Fact]
    public async Task CustomerPointsAdjust()
    {
        // Arrange
        var client = await factory.CreateAdminClientAsync();
        using var staffResponse = await client.PostJsonAsync(ApiRoutes.Staff, new StaffCreateRequest { Code = $"T{Guid.NewGuid():N}"[..10], Name = "削除済み", Role = StaffRole.Manager, StoreId = TestData.MainStoreId }, options);
        var deletedStaff = await staffResponse.ReadAsAsync<StaffListResponseItem>(HttpStatusCode.Created, options);
        using var deleteStaffResponse = await client.DeleteUrlAsync($"{ApiRoutes.Staff}/{deletedStaff.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteStaffResponse.StatusCode);

        // Act / Assert: 会員照会
        var customer = await client.GetJsonAsync<CustomerListResponseItem>($"{ApiRoutes.Customers}/lookup?code=M0003", options);
        Assert.Equal(500, customer.PointBalance);

        // Act / Assert: 手動調整
        using var adjustResponse = await client.PostJsonAsync($"{ApiRoutes.Customers}/{customer.Id}/points/adjust", new CustomerPointAdjustRequest { Points = 100, Reason = "キャンペーン", StaffId = TestData.ManagerStaffId }, options);
        var adjusted = await adjustResponse.ReadAsAsync<CustomerPointHistoryResponseItem>(HttpStatusCode.OK, options);
        Assert.Equal(PointHistoryType.Adjust, adjusted.Type);
        Assert.Equal(600, adjusted.BalanceAfter);
        Assert.Equal(600, (await client.GetJsonAsync<CustomerListResponseItem>($"{ApiRoutes.Customers}/{customer.Id}", options)).PointBalance);

        // Act / Assert: 担当が見つからない・削除済みの調整は 422 で、残高を変えない
        using var noStaffResponse = await client.PostJsonAsync($"{ApiRoutes.Customers}/{customer.Id}/points/adjust", new CustomerPointAdjustRequest { Points = 100, Reason = "担当なし" }, options);
        await noStaffResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "VALIDATION_ERROR", options);
        using var deletedStaffResponse = await client.PostJsonAsync($"{ApiRoutes.Customers}/{customer.Id}/points/adjust", new CustomerPointAdjustRequest { Points = 100, Reason = "削除済みの担当", StaffId = deletedStaff.Id }, options);
        await deletedStaffResponse.ReadProblemAsync(HttpStatusCode.UnprocessableEntity, "VALIDATION_ERROR", options);

        // Act / Assert: 履歴
        var history = await client.GetJsonAsync<CustomerPointHistoryResponse>($"{ApiRoutes.Customers}/{customer.Id}/points/history", options);
        Assert.Equal(2, history.Total);
        Assert.Equal(600, history.Items[0].BalanceAfter);
        Assert.All(history.Items, x => Assert.Equal(PointHistoryType.Adjust, x.Type));

        // Act / Assert: ない会員の照会は 404
        using var missing = await client.GetAsync(new Uri($"{ApiRoutes.Customers}/lookup?code=X9999", UriKind.Relative), Token);
        await missing.ReadProblemAsync(HttpStatusCode.NotFound, "NOT_FOUND", options);
    }
}
