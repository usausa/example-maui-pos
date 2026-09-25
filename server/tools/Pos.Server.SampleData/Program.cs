namespace Pos.Server.SampleData;

// レポート確認用のサンプル取引を API 経由で作る。使い方は SampleDataOptions を参照
internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        SampleDataOptions options;
        try
        {
            options = SampleDataOptions.Parse(args);
        }
        catch (ArgumentException ex)
        {
            await Console.Error.WriteLineAsync(ex.Message).ConfigureAwait(false);
            await Console.Error.WriteLineAsync(SampleDataOptions.Usage).ConfigureAwait(false);
            return 2;
        }

        if (options.ShowHelp)
        {
            await Console.Out.WriteLineAsync(SampleDataOptions.Usage).ConfigureAwait(false);
            return 0;
        }

        using var client = new ApiClient(options.BaseAddress);
        var generator = new SampleGenerator(client, options, Console.Out);
        try
        {
            await client.LoginAsync(options.User, options.Password).ConfigureAwait(false);
            await generator.RunAsync().ConfigureAwait(false);
            return 0;
        }
        catch (ApiException ex)
        {
            await Console.Error.WriteLineAsync($"API エラー: {ex.Message}").ConfigureAwait(false);
            return 1;
        }
        catch (HttpRequestException ex)
        {
            await Console.Error.WriteLineAsync($"接続できません: {options.BaseAddress} ({ex.Message})").ConfigureAwait(false);
            return 1;
        }
    }
}
