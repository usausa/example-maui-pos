namespace Pos.Terminal;

using MauiPermissions = Microsoft.Maui.ApplicationModel.Permissions;

#pragma warning disable CA1724
public static class Permissions
{
    public static ValueTask<bool> RequestCameraAsync() =>
        CheckAndRequestAsync<MauiPermissions.Camera>();

    private static async ValueTask<bool> CheckAndRequestAsync<TPermission>()
        where TPermission : MauiPermissions.BasePermission, new()
    {
        var status = await MauiPermissions.CheckStatusAsync<TPermission>();
        if (status != PermissionStatus.Granted)
        {
            status = await MauiPermissions.RequestAsync<TPermission>();
        }

        return status == PermissionStatus.Granted;
    }
}
#pragma warning restore CA1724
