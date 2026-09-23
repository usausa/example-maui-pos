namespace Pos.Terminal.Controls;

// 背景の上の見出し (絵文字付きの文言) と幅いっぱいの白い面。集計や詳細の行を Content に載せる
public sealed partial class SectionPanel
{
    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title),
        typeof(string),
        typeof(SectionPanel),
        string.Empty);

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public SectionPanel()
    {
        InitializeComponent();
    }
}
