namespace Pos.Server;

using System.Net.Http.Headers;
using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using Pos.Contract.Terminals;
using Pos.Server.Host.Endpoints;
using Pos.Server.Services;

// 認証済みのクライアント: 管理画面のログイン (Cookie) と、端末のペアリング (Bearer)
internal static partial class AuthTestExtensions
{
    // 初期の管理者 (appsettings の Auth:InitialName / InitialPassword)
    public const string AdminName = "admin";
    public const string AdminPassword = "admin";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static Task<HttpClient> CreateAdminClientAsync(this TestApplicationFactory factory) =>
        factory.CreateLoginClientAsync(AdminName, AdminPassword);

    // ログイン画面のフォーム (偽造防止トークン付き) を送り、Cookie を持ったクライアントを返す
    public static async Task<HttpClient> CreateLoginClientAsync(this TestApplicationFactory factory, string name, string password)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        using var response = await client.PostLoginAsync(name, password);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
        return client;
    }

    public static async Task<HttpResponseMessage> PostLoginAsync(this HttpClient client, string name, string password)
    {
        var page = await client.GetStringAsync(new Uri("/login", UriKind.Relative), Token);
        var match = AntiforgeryPattern().Match(page);
        Assert.True(match.Success, "Antiforgery token not found.");

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(match.Groups[1].Value),
            ["name"] = name,
            ["password"] = password,
            ["returnUrl"] = string.Empty
        });
        return await client.PostAsync(new Uri("/auth/login", UriKind.Relative), content, Token);
    }

    // 端末をペアリングして、トークンを付けたクライアントを返す
    public static async Task<HttpClient> CreateTerminalClientAsync(this TestApplicationFactory factory, Guid terminalId)
    {
        var code = await factory.Services.GetRequiredService<TerminalTokenService>().IssuePairingCodeAsync(terminalId, Token);
        var client = factory.CreateClient();
        using var response = await client.PostJsonAsync(ApiRoutes.Terminals + "/pair", new TerminalPairRequest { PairingCode = code!.Code, DeviceName = "test" }, factory.JsonOptions());
        var paired = await response.ReadAsAsync<TerminalPairResponse>(HttpStatusCode.OK, factory.JsonOptions());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", paired.Token);
        return client;
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryPattern();
}
