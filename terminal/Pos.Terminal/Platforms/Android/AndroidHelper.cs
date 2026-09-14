#pragma warning disable IDE0130
// ReSharper disable once CheckNamespace
namespace Pos.Terminal;

using Android.App;

public static class AndroidHelper
{
    public static string GetExternalFilesDir() =>
        Application.Context.GetExternalFilesDir(string.Empty)!.Path;
}
