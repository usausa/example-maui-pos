namespace Pos.Server;

using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

// API テスト用の JSON 入出力 (アプリと同じ JsonSerializerOptions を使う)
internal static class ApiTestExtensions
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static JsonSerializerOptions JsonOptions(this TestApplicationFactory factory) =>
        factory.Services.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;

    public static async Task<T> GetJsonAsync<T>(this HttpClient client, string url, JsonSerializerOptions options)
    {
        using var response = await client.GetAsync(new Uri(url, UriKind.Relative), Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>(options, Token))!;
    }

    public static Task<HttpResponseMessage> PostJsonAsync<TRequest>(this HttpClient client, string url, TRequest request, JsonSerializerOptions options) =>
        client.PostAsJsonAsync(new Uri(url, UriKind.Relative), request, options, Token);

    public static Task<HttpResponseMessage> PutJsonAsync<TRequest>(this HttpClient client, string url, TRequest request, JsonSerializerOptions options) =>
        client.PutAsJsonAsync(new Uri(url, UriKind.Relative), request, options, Token);

    public static Task<HttpResponseMessage> DeleteUrlAsync(this HttpClient client, string url) =>
        client.DeleteAsync(new Uri(url, UriKind.Relative), Token);

    public static async Task<T> ReadAsAsync<T>(this HttpResponseMessage response, HttpStatusCode expected, JsonSerializerOptions options)
    {
        var body = await response.Content.ReadAsStringAsync(Token);
        Assert.True(expected == response.StatusCode, $"Expected {expected} but was {response.StatusCode}: {body}");
        return JsonSerializer.Deserialize<T>(body, options)!;
    }

    // Problem Details (errorCode 付き)
    public static async Task<ProblemResponse> ReadProblemAsync(this HttpResponseMessage response, HttpStatusCode expected, string errorCode, JsonSerializerOptions options)
    {
        var problem = await response.ReadAsAsync<ProblemResponse>(expected, options);
        Assert.Equal(errorCode, problem.ErrorCode);
        Assert.NotNull(problem.TraceId);
        return problem;
    }
}
