namespace Pos.Terminal.Services;

using Android.Content;

public sealed partial class CredentialService
{
    // 削除も復号を伴って同じ例外になるので、暗号化の層を通さずに SecureStorage の実体の SharedPreferences を消す (端末の設定は別のファイル)
    private static partial void ResetSecureStorage()
    {
        var context = Android.App.Application.Context;
        using var preferences = context.GetSharedPreferences($"{context.PackageName}.microsoft.maui.essentials.preferences", FileCreationMode.Private)!;
        using var editor = preferences.Edit()!;
        editor.Clear()!.Apply();
    }
}
