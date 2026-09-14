namespace Pos.Server.SampleData;

using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using System.Text.Unicode;

// サーバと同じ JSON 契約 (camelCase / null 省略 / 列挙型は文字列 / 日時は UTC) で API を呼ぶ
internal sealed class ApiClient : IDisposable
{
    private const string Prefix = "api/v1/";

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly HttpClient client;

    public ApiClient(Uri baseAddress)
    {
        client = new HttpClient { BaseAddress = baseAddress, Timeout = TimeSpan.FromSeconds(30) };
    }

    public void Dispose()
    {
        client.Dispose();
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };
        options.Converters.Add(new JsonDateTimeConverter());
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    public async Task<T> GetAsync<T>(string path)
    {
        using var response = await client.GetAsync(new Uri(Prefix + path, UriKind.Relative)).ConfigureAwait(false);
        return await ReadAsync<T>(response).ConfigureAwait(false);
    }

    // 404 は null
    public async Task<T?> GetOrDefaultAsync<T>(string path)
        where T : class
    {
        using var response = await client.GetAsync(new Uri(Prefix + path, UriKind.Relative)).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        return await ReadAsync<T>(response).ConfigureAwait(false);
    }

    public async Task<T> PostAsync<T>(string path, object request)
    {
        using var content = JsonContent.Create(request, request.GetType(), options: JsonOptions);
        using var response = await client.PostAsync(new Uri(Prefix + path, UriKind.Relative), content).ConfigureAwait(false);
        return await ReadAsync<T>(response).ConfigureAwait(false);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions).ConfigureAwait(false);
            return result ?? throw new ApiException(response.StatusCode, null, "応答が空です");
        }

        ProblemResponse? problem = null;
        if (response.Content.Headers.ContentType?.MediaType?.Contains("json", StringComparison.OrdinalIgnoreCase) ?? false)
        {
            try
            {
                problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(JsonOptions).ConfigureAwait(false);
            }
            catch (JsonException)
            {
                problem = null;
            }
        }

        var message = problem is null
            ? $"{(int)response.StatusCode} {response.ReasonPhrase}"
            : $"{(int)response.StatusCode} {problem.ErrorCode}: {problem.Detail ?? problem.Title}";
        throw new ApiException(response.StatusCode, problem?.ErrorCode, message);
    }
}
