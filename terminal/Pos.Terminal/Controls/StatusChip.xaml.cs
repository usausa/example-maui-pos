namespace Pos.Terminal.Controls;

// 状態を色と短い文言 (絵文字付き) で示すチップ。色は画面側の Converter で決める
public sealed partial class StatusChip
{
    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text),
        typeof(string),
        typeof(StatusChip),
        string.Empty);

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    // 既定値は GrayLighten1 (PosChipBorder と同じ)
    public static readonly BindableProperty ChipColorProperty = BindableProperty.Create(
        nameof(ChipColor),
        typeof(Color),
        typeof(StatusChip),
        Color.FromArgb("#BDBDBD"));

    public Color ChipColor
    {
        get => (Color)GetValue(ChipColorProperty);
        set => SetValue(ChipColorProperty, value);
    }

    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(
        nameof(TextColor),
        typeof(Color),
        typeof(StatusChip),
        Colors.White);

    public Color TextColor
    {
        get => (Color)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    public StatusChip()
    {
        InitializeComponent();
    }
}
