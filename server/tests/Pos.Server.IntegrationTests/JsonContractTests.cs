namespace Pos.Server;

using System.Text;
using System.Text.Json;

using Pos.Contract.Transactions;
using Pos.Server.Host.Endpoints;

// JSON の約束: camelCase、null は省略、列挙型は文字列、日時は yyyy-MM-ddTHH:mm:ss.fffZ、日付は yyyy-MM-dd
public sealed class JsonContractTests : IClassFixture<TestApplicationFactory>
{
    private readonly TestApplicationFactory factory;

    private readonly JsonSerializerOptions options;

    public JsonContractTests(TestApplicationFactory factory)
    {
        this.factory = factory;
        options = factory.JsonOptions();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // 項目名は camelCase、日時は UTC のミリ秒付き (yyyy-MM-ddTHH:mm:ss.fffZ) で書く
    [Fact]
    public void SerializeUsesContractFormats()
    {
        // Arrange
        var request = new TransactionVoidRequest
        {
            StaffId = new Guid("00000000-0000-0000-0000-000000000001"),
            Reason = "レジ誤操作",
            VoidedAt = new DateTime(2026, 9, 11, 3, 15, 0, DateTimeKind.Utc)
        };

        // Act
        var json = JsonSerializer.Serialize(request, options);

        // Assert
        Assert.Equal("{\"staffId\":\"00000000-0000-0000-0000-000000000001\",\"reason\":\"レジ誤操作\",\"voidedAt\":\"2026-09-11T03:15:00.000Z\"}", json);
    }

    // null の項目だけを省き (Guid.Empty は書く)、小数は桁を保ち、日付は yyyy-MM-dd、列挙型は文字列で書く
    [Fact]
    public void SerializeOmitsNullAndWritesEnumAsString()
    {
        // Arrange
        var response = new TransactionCalculateResponse
        {
            Lines = [],
            Discounts = [],
            TaxSummaries = [new TransactionCalculateResponseTaxSummary { TaxRateId = Guid.Empty, Rate = 0.10m, TaxIncluded = true, TaxableAmount = 80100m, TaxAmount = 7281m }],
            Total = 80100m
        };
        var delivery = new TransactionListResponseDelivery { RecipientName = "山田 太郎", Address = "東京都", RequestedDate = new DateOnly(2026, 9, 14) };

        // Act
        var json = JsonSerializer.Serialize(response, options);
        var deliveryJson = JsonSerializer.Serialize(delivery, options);
        var typeJson = JsonSerializer.Serialize(new TransactionVoidRequest { Reason = "x" }, options);

        // Assert
        Assert.Contains("\"taxableAmount\":80100", json, StringComparison.Ordinal);
        Assert.Contains("\"rate\":0.10", json, StringComparison.Ordinal);
        Assert.DoesNotContain("phone", deliveryJson, StringComparison.Ordinal);
        Assert.Contains("\"requestedDate\":\"2026-09-14\"", deliveryJson, StringComparison.Ordinal);
        Assert.Contains("\"staffId\":\"00000000-0000-0000-0000-000000000000\"", typeJson, StringComparison.Ordinal);
        Assert.Equal("\"Sale\"", JsonSerializer.Serialize(TransactionType.Sale, options));
        Assert.Equal("\"TaxIncluded\"", JsonSerializer.Serialize(PointBasis.TaxIncluded, options));
    }

    // オフセット付きの日時は UTC に直して読む
    [Fact]
    public void DeserializeRoundTripsDateTimeAsUtc()
    {
        // Arrange
        const string json = "{\"staffId\":\"00000000-0000-0000-0000-000000000001\",\"reason\":\"x\",\"voidedAt\":\"2026-09-11T12:15:00.000+09:00\"}";

        // Act
        var request = JsonSerializer.Deserialize<TransactionVoidRequest>(json, options);

        // Assert
        Assert.NotNull(request);
        Assert.Equal(DateTimeKind.Utc, request.VoidedAt.Kind);
        Assert.Equal(new DateTime(2026, 9, 11, 3, 15, 0, DateTimeKind.Utc), request.VoidedAt);
    }

    // 列挙型は文字列から読む
    [Fact]
    public void DeserializeEnumFromString()
    {
        // Arrange
        const string json = "{\"type\":\"Return\",\"lines\":[],\"discounts\":[],\"payments\":[]}";

        // Act
        var request = JsonSerializer.Deserialize<TransactionCalculateRequest>(json, options);

        // Assert
        Assert.NotNull(request);
        Assert.Equal(TransactionType.Return, request.Type);
        Assert.Empty(request.Lines);
    }

    // 読めない本文 (壊れた JSON・文字列の数値・重複したキー・知らない項目) は 400 (VALIDATION_ERROR)。title は日本語
    [Theory]
    [InlineData("{")]
    [InlineData("""{"code":"JSON-1","name":"JSON","sortOrder":"1"}""")]
    [InlineData("""{"code":"JSON-1","code":"JSON-2","name":"JSON"}""")]
    [InlineData("""{"code":"JSON-1","name":"JSON","ownerId":"other"}""")]
    public async Task CreateWithUnreadableJsonReturnsValidationError(string body)
    {
        // Arrange
        var client = await factory.CreateAdminClientAsync();
        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        // Act
        using var response = await client.PostAsync(new Uri(ApiRoutes.Categories, UriKind.Relative), content, Token);

        // Assert
        var problem = await response.ReadProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_ERROR", options);
        Assert.Equal("入力に誤りがあります", problem.Title);
    }
}
