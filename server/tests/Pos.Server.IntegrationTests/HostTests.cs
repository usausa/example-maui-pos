namespace Pos.Server;

using System.Text.Json.Nodes;

using Microsoft.AspNetCore.Mvc.Testing;

using Pos.Server.Host.Endpoints;

public sealed class HostTests : IClassFixture<TestApplicationFactory>
{
    private readonly TestApplicationFactory factory;

    public HostTests(TestApplicationFactory factory)
    {
        this.factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // ヘルスチェックはログインなしで 200
    [Fact]
    public async Task HealthReturnsOk()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative), Token);

        // Assert
        response.EnsureSuccessStatusCode();
    }

    // ログインしていなければログイン画面へ
    [Fact]
    public async Task RootRedirectsToLoginWhenAnonymous()
    {
        // Arrange
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // Act
        using var response = await client.GetAsync(new Uri("/", UriKind.Relative), Token);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("http://localhost/login", response.Headers.Location?.ToString(), StringComparison.Ordinal);
    }

    // ログインしていれば管理画面の HTML を返す (プリレンダリングしないので、中身は対話型で描く)
    [Fact]
    public async Task RootShowsHomePage()
    {
        // Arrange
        var client = await factory.CreateAdminClientAsync();

        // Act
        using var response = await client.GetAsync(new Uri("/", UriKind.Relative), Token);
        var content = await response.Content.ReadAsStringAsync(Token);

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Contains("<title>POS 管理</title>", content, StringComparison.Ordinal);
    }

    // ない画面は 404 のページを出す
    [Fact]
    public async Task UnknownPageReturnsNotFoundPage()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync(new Uri("/unknown", UriKind.Relative), Token);
        var content = await response.Content.ReadAsStringAsync(Token);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("ページが見つかりません", content, StringComparison.Ordinal);
    }

    // 開発環境の OpenAPI の文書に API が載る
    [Fact]
    public async Task OpenApiDocumentListsApi()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative), Token);
        var content = await response.Content.ReadAsStringAsync(Token);

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Contains($"{ApiRoutes.Transactions}/calculate", content, StringComparison.Ordinal);
        Assert.Contains(ApiRoutes.Shifts + "/{id}/summary/pdf", content, StringComparison.Ordinal);
        Assert.Contains($"{ApiRoutes.Reports}/sales/daily/pdf", content, StringComparison.Ordinal);
    }

    // OpenAPI の文書に操作名と応答の型 (失敗は Problem Details) が載り、整数の項目は integer になる
    [Fact]
    public async Task OpenApiDocumentDescribesResponses()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        var document = JsonNode.Parse(await client.GetStringAsync(new Uri("/openapi/v1.json", UriKind.Relative), Token))!;
        var operation = document["paths"]![ApiRoutes.Categories + "/{id}"]!["get"]!;

        // Assert
        Assert.Equal("CategoryGet", (string?)operation["operationId"]);
        Assert.Equal("#/components/schemas/CategoryListResponseItem", (string?)operation["responses"]!["200"]!["content"]!["application/json"]!["schema"]!["$ref"]);
        Assert.NotNull(operation["responses"]!["404"]!["content"]!["application/problem+json"]);
        Assert.Equal("integer", (string?)document["components"]!["schemas"]!["CategoryListResponseItem"]!["properties"]!["sortOrder"]!["type"]);
    }

    // ない API は HTML ではなく本文のない 404 を返す
    [Fact]
    public async Task UnknownApiReturnsPlainNotFound()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync(new Uri("/api/unknown", UriKind.Relative), Token);
        var content = await response.Content.ReadAsStringAsync(Token);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(content);
    }
}
