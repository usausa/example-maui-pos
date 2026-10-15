namespace Pos.Terminal.Modules.Dialogs;

public sealed record SelectParameter(string Title, IReadOnlyList<string> Items, int Selected);

public sealed record SelectItem(int Index, string Name, bool IsSelected);

// 選択: 一覧 (操作メニュー、絞り込み、種別などの値、承認者) からタップした行の番号を返す (null = キャンセル)
public sealed partial class SelectViewModel : AppDialogViewModelBase, IPopupInitialize<SelectParameter>
{
    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    public ObservableCollection<SelectItem> Items { get; } = [];

    public IObserveCommand SelectCommand { get; }

    public IObserveCommand CloseCommand { get; }

    public SelectViewModel()
    {
        SelectCommand = MakeAsyncCommand<SelectItem>(async x => await CloseAsync<int?>(x.Index));
        CloseCommand = MakeAsyncCommand(async () => await CloseAsync());
    }

    public void Initialize(SelectParameter parameter)
    {
        Title = parameter.Title;
        Items.Replace(parameter.Items.Select((x, i) => new SelectItem(i, x, i == parameter.Selected)));
    }
}
