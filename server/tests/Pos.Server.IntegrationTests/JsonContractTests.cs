namespace Pos.Server;

using System.Text.Json;

using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Pos.Contract.Transactions;

// JSON の約束: camelCase、null は省略、列挙型は文字列、日時は yyyy-MM-ddTHH:mm:ss.fffZ、日付は yyyy-MM-dd
public sealed class JsonContractTests : IClassFixture<TestApplicationFactory>
{
    private readonly TestApplicationFactory factory;

    public JsonContractTests(TestApplicationFactory factory)
    {
        this.factory = factory;
    }

    private JsonSerializerOptions Options => factory.Services.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;

    [Fact]
    public void SerializeUsesContractFormats()
    {
        var request = new TransactionVoidRequest
        {
            StaffId = new Guid("00000000-0000-0000-0000-000000000001"),
            Reason = "レジ誤操作",
            VoidedAt = new DateTime(2026, 9, 11, 3, 15, 0, DateTimeKind.Utc)
        };

        var json = JsonSerializer.Serialize(request, Options);

        Assert.Equal("{\"staffId\":\"00000000-0000-0000-0000-000000000001\",\"reason\":\"レジ誤操作\",\"voidedAt\":\"2026-09-11T03:15:00.000Z\"}", json);
    }

    [Fact]
    public void SerializeOmitsNullAndWritesEnumAsString()
    {
        var response = new TransactionCalculateResponse
        {
            Lines = [],
            Discounts = [],
            TaxSummaries = [new TransactionCalculateResponseTaxSummary { TaxRateId = Guid.Empty, Rate = 0.10m, TaxIncluded = true, TaxableAmount = 80100m, TaxAmount = 7281m }],
            Total = 80100m
        };
        var delivery = new TransactionResponseDelivery { RecipientName = "山田 太郎", Address = "東京都", RequestedDate = new DateOnly(2026, 9, 14) };

        var json = JsonSerializer.Serialize(response, Options);
        var deliveryJson = JsonSerializer.Serialize(delivery, Options);
        var typeJson = JsonSerializer.Serialize(new TransactionVoidRequest { Reason = "x" }, Options);

        Assert.Contains("\"taxableAmount\":80100", json, StringComparison.Ordinal);
        Assert.Contains("\"rate\":0.10", json, StringComparison.Ordinal);
        Assert.DoesNotContain("phone", deliveryJson, StringComparison.Ordinal);
        Assert.Contains("\"requestedDate\":\"2026-09-14\"", deliveryJson, StringComparison.Ordinal);
        Assert.Contains("\"staffId\":\"00000000-0000-0000-0000-000000000000\"", typeJson, StringComparison.Ordinal);
        Assert.Equal("\"Sale\"", JsonSerializer.Serialize(TransactionType.Sale, Options));
        Assert.Equal("\"TaxIncluded\"", JsonSerializer.Serialize(PointBasis.TaxIncluded, Options));
    }

    [Fact]
    public void DeserializeRoundTripsDateTimeAsUtc()
    {
        const string json = "{\"staffId\":\"00000000-0000-0000-0000-000000000001\",\"reason\":\"x\",\"voidedAt\":\"2026-09-11T12:15:00.000+09:00\"}";

        var request = JsonSerializer.Deserialize<TransactionVoidRequest>(json, Options);

        Assert.NotNull(request);
        Assert.Equal(DateTimeKind.Utc, request.VoidedAt.Kind);
        Assert.Equal(new DateTime(2026, 9, 11, 3, 15, 0, DateTimeKind.Utc), request.VoidedAt);
    }

    [Fact]
    public void DeserializeEnumFromString()
    {
        const string json = "{\"type\":\"Return\",\"lines\":[],\"discounts\":[],\"payments\":[]}";

        var request = JsonSerializer.Deserialize<TransactionCalculateRequest>(json, Options);

        Assert.NotNull(request);
        Assert.Equal(TransactionType.Return, request.Type);
        Assert.Empty(request.Lines);
    }
}
