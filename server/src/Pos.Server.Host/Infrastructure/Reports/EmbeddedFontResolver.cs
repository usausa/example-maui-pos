namespace Pos.Server.Host.Infrastructure.Reports;

using System.Collections.Frozen;

using OysterReport;

public sealed class EmbeddedFontResolver : IReportFontResolver
{
    private const string EmbeddedFontName = "IPAexGothic";

    private static readonly FrozenSet<string> MappedFontNames = new[]
    {
        "ＭＳ Ｐゴシック",
        "MS Pゴシック",
        "ＭＳ ゴシック",
        "メイリオ",
        "Meiryo",
        "游ゴシック",
        "Yu Gothic",
        "游ゴシック Medium",
        "Yu Gothic Medium",
        "ＭＳ Ｐ明朝",
        "MS P明朝",
        "ＭＳ 明朝",
        "HGP明朝E",
        "HGPMinchoE",
        "HGS明朝E",
        "HGSMinchoE"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private readonly ReadOnlyMemory<byte> fontData;

    public EmbeddedFontResolver(string path)
    {
        fontData = File.ReadAllBytes(path);
    }

    public FontResolveInfo? ResolveTypeface(string familyName, bool bold, bool italic)
    {
        if (!MappedFontNames.Contains(familyName))
        {
            return null;
        }

        return new FontResolveInfo(EmbeddedFontName)
        {
            MustSimulateBold = bold,
            MustSimulateItalic = italic
        };
    }

    public ReadOnlyMemory<byte>? GetFont(string faceName) =>
        String.Equals(faceName, EmbeddedFontName, StringComparison.Ordinal)
            ? fontData
            : null;
}
