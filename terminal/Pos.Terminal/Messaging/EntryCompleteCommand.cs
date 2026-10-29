namespace Pos.Terminal.Messaging;

// 文字の欄の Enter と完了を処理済みにする (EntryBind は処理済みでなければ、次の欄や一覧へフォーカスを移す)。
// 最後の欄、検索の欄、Enter でシートを確定する欄に使い、検索や確定のコマンドは続けて実行する
public static class EntryCompleteCommand
{
    public static ICommand Create(ICommand? command = null) =>
        new DelegateCommand<EntryCompleteEvent>(x =>
        {
            x.Handled = true;
            if ((command is not null) && command.CanExecute(null))
            {
                command.Execute(null);
            }
        });
}
