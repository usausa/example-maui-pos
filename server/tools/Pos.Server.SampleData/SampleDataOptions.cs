namespace Pos.Server.SampleData;

internal sealed class SampleDataOptions
{
    public static string Usage { get; } = """
        使い方: dotnet run --project server/tools/Pos.Server.SampleData -- [オプション]

          --base <url>     サーバ (既定 http://localhost:8080/)
          --days <n>       今日から遡る日数 (既定 7)
          --per-day <n>    端末 1 台 1 日あたりの販売件数の目安 (既定 6)
          --seed <n>       乱数シード (既定 1。同じシードなら同じ内容)
          --help           この説明
        """;

    public Uri BaseAddress { get; private set; } = new("http://localhost:8080/");

    public int Days { get; private set; } = 7;

    public int PerDay { get; private set; } = 6;

    public int Seed { get; private set; } = 1;

    public bool ShowHelp { get; private set; }

    public static SampleDataOptions Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var options = new SampleDataOptions();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help":
                case "-h":
                    options.ShowHelp = true;
                    break;
                case "--base":
                    options.BaseAddress = new Uri(Next(args, ref i), UriKind.Absolute);
                    break;
                case "--days":
                    options.Days = Math.Max(1, ParseInt(Next(args, ref i)));
                    break;
                case "--per-day":
                    options.PerDay = Math.Max(1, ParseInt(Next(args, ref i)));
                    break;
                case "--seed":
                    options.Seed = ParseInt(Next(args, ref i));
                    break;
                default:
                    throw new ArgumentException($"不明なオプションです: {args[i]}");
            }
        }

        return options;
    }

    private static string Next(string[] args, ref int index)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"{args[index]} には値が必要です");
        }

        return args[++index];
    }

    private static int ParseInt(string value) =>
        Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : throw new ArgumentException($"数値ではありません: {value}");
}
