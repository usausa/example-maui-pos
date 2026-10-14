namespace Pos.Domain;

using System.Reflection;

// Pos.Domain は UI / DB / HTTP / 通信データに依存しない
public sealed class DependencyTests
{
    private static readonly string[] ForbiddenPrefixes =
    [
        "Microsoft.AspNetCore",
        "Microsoft.Maui",
        "Microsoft.Data.Sqlite",
        "Smart.Data",
        "Pos.Contract",
        "Pos.Server",
        "Pos.Terminal"
    ];

    // Domain は UI・DB・HTTP・通信データ・サーバ・端末のアセンブリを参照しない
    [Fact]
    public void DomainDoesNotReferenceInfrastructure()
    {
        // Arrange
        var references = Assembly.Load("Pos.Domain")
            .GetReferencedAssemblies()
            .Select(static x => x.Name!)
            .ToList();

        // Act
        var forbidden = references
            .Where(static x => ForbiddenPrefixes.Any(prefix => x.StartsWith(prefix, StringComparison.Ordinal)))
            .ToList();

        // Assert
        Assert.Empty(forbidden);
    }
}
