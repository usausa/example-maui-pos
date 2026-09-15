namespace Pos.Terminal.Modules.Dialogs;

public sealed record SelectParameter(string Title, IReadOnlyList<string> Items, int Selected);

public sealed record SelectRow(int Index, string Name, bool IsSelected);

// 一覧からの選択 (操作メニュー・絞り込み・承認者)。タップした行の番号を返す (null = キャンセル)
public sealed partial class SelectViewModel : AppDialogViewModelBase, IPopupInitialize<SelectParameter>
{
    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    public ObservableCollection<SelectRow> Items { get; } = [];

    public IObserveCommand SelectCommand { get; }

    public IObserveCommand CloseCommand { get; }

    public SelectViewModel(IPopupNavigator popupNavigator)
    {
        SelectCommand = MakeAsyncCommand<SelectRow>(async x => await popupNavigator.CloseAsync<int?>(x.Index));
        CloseCommand = MakeAsyncCommand(async () => await popupNavigator.CloseAsync());
    }

    public void Initialize(SelectParameter parameter)
    {
        Title = parameter.Title;
        Items.Replace(parameter.Items.Select((x, i) => new SelectRow(i, x, i == parameter.Selected)));
    }
}
