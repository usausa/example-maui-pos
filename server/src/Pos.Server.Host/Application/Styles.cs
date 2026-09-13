namespace Pos.Server.Host.Application;

using MudBlazor;

public static class Styles
{
    public static MudTheme Theme { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = Colors.Blue.Darken3,
            PrimaryDarken = Colors.Blue.Darken4,
            PrimaryLighten = Colors.Blue.Darken2,
            Secondary = Colors.LightBlue.Darken2,
            AppbarBackground = Colors.Blue.Darken3,
            AppbarText = Colors.Shades.White,
            DrawerBackground = Colors.Gray.Lighten4,
            DrawerText = Colors.Gray.Darken4,
            Background = Colors.Gray.Lighten5,
            Surface = Colors.Shades.White
        },
        PaletteDark = new PaletteDark()
    };

    public static DialogOptions SmallDialog { get; } = new() { MaxWidth = MaxWidth.Small, FullWidth = true };

    public static DialogOptions MediumDialog { get; } = new() { MaxWidth = MaxWidth.Medium, FullWidth = true };

    public static DialogOptions LargeDialog { get; } = new() { MaxWidth = MaxWidth.Large, FullWidth = true };
}
