namespace Pos.Server.Host.Application.Telemetry;

using System.Reflection;

// Meter と ActivitySource の名前 (アセンブリ名)
public static class Source
{
    private static readonly AssemblyName AssemblyName = typeof(Source).Assembly.GetName();

    public static string Name => AssemblyName.Name!;

    public static string Version => AssemblyName.Version!.ToString();
}
