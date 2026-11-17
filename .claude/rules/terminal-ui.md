---
paths:
  - "terminal/Pos.Terminal/Modules/**"
  - "terminal/Pos.Terminal/Controls/**"
  - "terminal/Pos.Terminal/Converters/**"
  - "terminal/Pos.Terminal/Resources/Styles/**"
  - "terminal/Pos.Terminal/Markup/**"
  - "terminal/Pos.Terminal/Behaviors/**"
  - "terminal/Pos.Terminal/Messaging/**"
  - "terminal/Pos.Terminal/Shell/**"
  - "terminal/Pos.Terminal/MainPage*"
  - "terminal/Pos.Terminal/App.xaml.cs"
  - "terminal/Pos.Terminal/Diagnostics/**"
  - "terminal/Pos.Terminal/Models/**"
  - "terminal/Pos.Terminal/Extensions.cs"
  - "terminal/Pos.Terminal/MauiProgram.cs"
  - "terminal/Pos.Terminal/Services/NetworkService.cs"
  - "terminal/Pos.Terminal/Services/PinService.cs"
---
# 端末の画面

## 画面と遷移

- 画面は `ViewId` に足し、View に `[View(ViewId.Xxx)]` と `[Hierarchy(n)]` を付ける
- `[Hierarchy(n)]` の n は screen-design.md の遷移図の深さにする (先へ進む画面ほど大きい)
- XAML で画面 ID を渡すときは `{markup:ViewId Xxx}` と書く (`x:Static` にしない)
- 遷移は `ForwardAsync` で行う
- 戻り先は、固定にするか `Parameters.WithReturnTo` で受ける
- 呼び出し元がさらに戻る先は、`WithCallerReturnTo` で引き継ぐ
- パラメータは `Parameters` に `WithXxx` と `GetXxx` の対で足す (複数の値をまとめて渡す `WithScan` は、値ごとの `GetXxx` で読む)
- ナビゲーションイベントの中の遷移と非同期処理は、`PostForwardAsync`、`PostActionAsync` で後回しにする
- 根の画面 (ホーム、スタッフ選択、未登録の初期設定) の戻るは、`OnNotifyBackAsync` で `AndroidHelper.MoveTaskToBack()` を呼び、タスクをバックグラウンドへ回す (アプリを終了しない)

## シェルと F キー

- XAML には `shell:ShellProperty` を書く
- 使わない F キーは `—` で無効にする
- F キーを押せない条件は、ViewModel の bool (`CanXxx`、`HasXxx`、`XxxEnabled`) か Converter にバインドする
- F1 は戻るにする (戻れない画面は `—` で無効、根の画面は設定など)
- 根の画面を除き、F1 とハードウェアの戻るを同じ遷移にする
- スキャンできる画面の F2 はスキャンにする
- カメラを出す画面 (スキャン、初期設定) の F2 はライトにする
- F4 は画面の主操作 (確定、登録、会計) にする
- 主操作のない画面の F4 は、更新や次の画面にする
- 主操作も更新も次の画面もない画面 (商品検索、スタッフ選択) の F4 は `—` にする
- 例外として、ホームは F キーの列を出さない (`FunctionVisible="False"`)
- 例外として、販売の F1 はメニューにする
- 例外として、会員選択の F4 は会員の解除にする
- 例外として、完了の画面は F1 を無効にし、ハードウェアの戻るを F4 (次へ) と同じにする

## ViewModel

- ViewModel のプロパティは `[ObservableProperty] public partial` にする
- 例外として、画面の枠 (`MainPageViewModel`) のシェルの値 (`IShellControl`) は、テンプレートと同じ `NotificationValue<T>` と `FunctionState` の一覧にする
- コマンドは `MakeAsyncCommand`、`MakeDelegateCommand` で作る
- コマンドは、行や欄の操作 (`-`、`+`、入力) を先に、閉じる (`CloseCommand`) と確定 (`CommitCommand`) を後に並べる
- コマンドの宣言と代入は同じ順に並べる
- `ShowXxx` はダイアログを出すメソッドだけに使う
- ViewModel (シートと画面の枠も) のクラスの前に、「画面名: 役割」の 1 行コメントを書く
- 非同期で取得する間に対象 (選んだ会員や商品) が変わったら、前の対象の結果を出さない

## 表示

- 表示中に状態 (bool、列挙型) で切り替わる色・文言・アイコンは ViewModel で組まず、Smart.Maui の `BoolToXxxConverter`、`MapToXxxConverter` を `Styles.xaml` にキー付きで構成するか、Trigger で切り替える (表示のときに 1 回組む文言、行の表示用の文字列、`DisplayNameConverter` で出す列挙型の名前は除く)
- 列挙型の文言は `ViewHelper.Name` と `DisplayNameConverter` の両方に足す (漏れると列挙名が出る)
- 業務ルールの違反 (`RuleError`) は `ViewHelper.Reason` で出す
- 一覧は `ObservableCollection<T>` にし、`ObservableCollection.Replace` で入れ替える
- 要素を削除しながらループするときは、`Where(...).ToList()` で複製せず、後ろから `RemoveAt` する
- 一覧の行は、ViewModel のファイルの先頭に `XxxItem` を置く
- `XxxItem` は表示用の文字列を持つ record にし、変わる状態があれば `NotificationObject` にする
- 複数の画面で使う行は `Models/{機能}/` に置く (`CustomerItem`)
- 集計の行は `Models/SummaryRow` を使う

## 空の状態

- 一覧とスキャン待ちの空の状態は、`PosEmptyStack` の絵文字にする
- 空の状態の文言は、該当がない・取得できないときだけ添える
- 面の中の一覧 (未送信、売上照会の内訳) の空の状態は、文言だけにする
- 一覧を取得できない・該当がないときに切り替わる文言は、`Message` プロパティ (既定は空) に入れる
- 変わらない空の文言は XAML に書く
- 入力欄が空のときの表示は、案内ではなく空の状態の表示として扱う
- 電卓や選択で入れる欄の空の表示は、`EmptyTextConverter` で出す (「タップして入力」「タップして選択」)
- 文字の欄の空の表示は、`Placeholder` で出す

## 入力

- 物理キーボードを前提にしない
- ソフトウェアキーボードは、設定と、文字を含む項目 (名前、住所、メール、備考、シリアル番号、会員番号、検索キーワード、理由の自由入力) だけに使う
- 数値と番号は電卓のポップアップで入力する
- `PopupNavigatorExtensions` に、入力の種類ごとのメソッド (`InputPhoneAsync`、`InputQuantityAsync`、`InputAmountAsync`、`InputPinAsync` など) を置く
- 番号のタイトルは、入力の種類ごとのメソッドで決める (PIN はコンテキストを入れたタイトルを渡し、伝票番号は支払方法の名前を渡して「{支払方法} の伝票番号」にする)
- 数値は、画面のコンテキスト (最大数、現在庫) を入れたタイトルを渡す
- 電卓の引数 (数値の `NumberInputParameter`、番号と PIN の `DigitsInputParameter`) は画面で作らず、`PopupNavigatorExtensions` を通す (番号の桁数と PIN の伏せ字は画面で持たない)
- レシート番号のような英数字の番号は、自店の端末番号と連番のように、数字だけで入力できる形にする
- 値を入れる欄のボタンは、次のスタイルを基本にする
  - ラベルが上の欄は `PosFieldInputButton`
  - 行の右に値を出す欄は `PosFieldValueButton` (数量は `PosCountButton`)
  - 選択肢を選ぶ欄は `PosSelectButton`
- 既存の欄のボタンは、触ったときにこのスタイルにそろえる
- 理由は定型の選択 (`ReasonSelect`) にし、自由入力も受ける
- 定型の理由は、ViewModel の固定の一覧から出す (在庫の調整はマスタの調整理由)
- 理由を省けるかは操作で決める (返品と任意の値引は必須、入出金は省けるなど)

## メッセージと確認

- 確認と情報は、`IDialog` の `AskAsync` と `InformationAsync` で出す
- ViewModel から `Confirm` と `Message` のシートを `IPopupNavigator` で開かない (`SheetDialog` が `AskAsync` と `InformationAsync` をこれらのシートに変換するため)
- 操作の完了は `Toast` で表示する
- 入力の不足や失敗は `InformationAsync` で表示する
- 取り消しにくい操作の前は `AskAsync` で確認し、OK のボタンに操作の動詞を入れる
- 控えが要る結果 (受注番号) は `InformationAsync` で表示する
- 起動中 (`App.OnStart`) のメッセージ (前回の異常終了、ローカル DB を開けない) は、OS のダイアログで出す (シートの仕組みがそろう前のため)

## ポップアップ

- ポップアップは `DialogId` に足し、View に `[Popup(DialogId.Xxx)]` を付ける
- ポップアップの ViewModel は、`AppDialogViewModelBase` と `IPopupInitialize<T>` で引数を受け、`CloseAsync(result)` で結果を返す (引数なしの `CloseAsync()` はキャンセル)
- ポップアップ専用の引数と結果の型は、ViewModel のファイルの先頭に置く (電卓の `NumberInputParameter` と `DigitsInputParameter` は `Models/Input`)
- 同じ入力を複数の画面で使うときは、ポップアップを 1 つにして `DialogId` で開く (値引は明細値引と取引値引のどちらも `DiscountView`)
- 一覧からの選択 (操作メニュー、絞り込み、承認者) は、OS の選択ダイアログ (`IDialog.SelectAsync`) ではなく `Select` シート (`PopupNavigatorExtensions.ChooseAsync`) を使う
- ポップアップは、画面の下端に寄せた幅いっぱいのシート (上角が丸い) にする
- 外側のタップでも閉じるのは、一覧からの選択のシートだけにする
- シートの下段のボタンは `InputCancelButton` と `InputConfirmButton` にする (削除のあるシートは `InputDeleteButton` も)
- 下段のボタン (✕、✔) は、F キーと同じ位置と配色にする
- 下段のボタンのコマンドは `CloseCommand` と `CommitCommand` にする
- 確認と情報のシートは、下段を文言のボタンにする
- 閉じるだけの情報のシートの OK は `CloseCommand` にする
- シートの中の文字入力は、キーボードの Enter でも確定できるようにする (キーボードが出ている間は下段のボタンが隠れるため)

## 文字の欄とフォーカス

- 文字の欄の `EntryBind.Controller` に `EntryController` をバインドする
- `EntryBind` は、Enter と完了の後、コマンドが `EntryCompleteEvent.Handled` を立てなければ、次にフォーカスできるもの (次の文字の欄、最後の欄では先頭の欄、一覧) へフォーカスを移す
- `ReturnType="Next"` 以外の欄 (最後の欄、検索の欄、Enter でシートを確定する欄) は `new EntryController(EntryCompleteCommand.Create(command))` にし、フォーカスを欄に残す (コマンドがなければ `Create()`)
- 文字の欄が続くフォームは、次に文字の欄がある欄を `ReturnType="Next"` にする (`EntryBind` と Android が、間のボタンと日付の欄を飛ばして次の文字の欄へ移す)
- Enter で欄を移す処理は書かない
- 最後の文字の欄と、Enter でシートを確定する欄は、`ReturnType` を付けない (Android では Done)
- 検索の欄は `ReturnType="Search"` にする
- 画面の遷移時 (`OnNavigatedToAsync`) に `Focus()` しない (キーボードが一覧を隠すため)
- フォーカスを移すのは、クリアや入力不足のメッセージのような操作の結果だけにする
- 文字の欄がある画面は、ルートに `behaviors:Focus.SuppressDefaultFocus="True"` を付ける (`NavigationFocusPlugin` が、開いた画面の最初の欄にフォーカスを置くのを止めるため)

## デザイン

- 画面は、灰色の背景の見出しと幅いっぱいの白い面で組む (`SectionPanel`、`PosSectionTemplate`)
- 角丸のカードを並べる構成にしない
- 角丸は、チップ、アバター、ボタン、シート、カメラの読み取り枠、未送信のバッジだけに使う
- 主な金額は白いバー (`PosHeroBorder`) にする
- 件数や金額の集計は、罫線の表 (`PosStatGrid`) にする
- 販売、会計、返金、金種のような入力中の合計は、バーにせず行で出す
- 状態のある一覧は、左端のバーと行の背景で状態を示し、区切り線で並べる
- 状態・種別・属性の文言は、POS のチップ (`LabelChip`) で示す (濃い塗りに白い文字とアイコン)
- チップの色は `ChipColor` で指定する
- 折り返して並べるチップは `PosFlexLabelChip` にする
- テンプレートの `StatusChip` は、大きいので使わない
- 表示時の浮き上がり (`AnimationOption.EnterAnimation`) は、続けて更新する一覧 (販売の明細、棚卸) には付けない

## アイコン

- チップのアイコンは `LabelChip.Icon` で付ける
- 色付きのボタンの文言に添えるアイコンは、白い `FontImageSource` で付ける
- ボタンの `FontImageSource` は `Markup/AppIcons` の静的フィールドに足し、XAML から `{x:Static markup:AppIcons.Xxx}` で使う (`Styles.xaml` や画面の `Resources` に置かない)
- 選ぶと背景が濃くなるボタンは、選ばれていないときは濃い色のグリフにし、選んだら Trigger でアイコンも白 (`XxxSelected`) にする
- アイコンだけのボタン (`PosMethodIconButton`、`PosSelectIconButton`、`PosExpandButton`、シートの `InputDeleteButton`) は、`Text` にグリフを置き、フォントはスタイルで付ける
- 例外として、シートの下段の ✕ と ✔、テンキーの ⌫、塗りつぶしの小さな丸ボタン (`PosQuantityButton`) の `+`、`-`、✕ は、Material Icons にせず、U+FE0F を付けない文字にする
- 値や行を外す ✕ は、灰色の小さな丸ボタン (`PosQuantityButton` を `GrayLighten1` にしたもの) にする
- 画面を移ったり操作を始めたりする白いボタン (ホームのキーの `MenuButton`、設定の操作) は、文言の前に色付きの絵文字を付ける
- 値を選ぶ白いボタンには、絵文字を付けない
- カメラの画面 (スキャン、初期設定) のバーのメッセージは、追加できたときは白い文字の ✓、ほかのとき (見つからない、設定 QR でない、カメラの権限がない、登録できない) は ✕ で始める
- 例外として、トーストのアイコンは色付きの絵文字にする (OS が描き、背景が Android のバージョンとテーマで明るくも暗くもなるため)
- 例外として、すべての選択肢に付けるマーク (返金方法の `SelectedMarkConverter` の ◉、○) は、ラジオボタンの代わりなので文字にする
- 選んだ行だけに付けるマークは ✅ にする

## 処理中の表示

- 処理中の表示は `IDialog` のインジケータだけにする
- 通信のインジケータは `NetworkService` が出す
- 通信以外の時間のかかる処理と同期は、`IDialog.Indicator()` を使う
- 画面に「読み込んでいます」のような文言や `ActivityIndicator` を置かない
- 段階の文言を出す `IDialog.Loading` は使わない (MAUI のウィンドウのオーバーレイなので、カメラのある画面ではカメラの中に描かれて読み取り枠の下になる)

## オンラインの取得

- オンラインで取得する一覧、集計、詳細の全体 (受注詳細) と、端末の一覧をオンラインの結果に置き換える検索 (取引履歴のシリアル番号) は、`CurrentState` に `ViewHelper.LoadingState`、`OfflineState` を入れる
- 取得中は結果を空の面で隠し、取得できないときは理由を結果の上に重ねる (`EqualsConverter`)
- 理由を重ねている間は、結果に対する操作 (F キー) を押せないようにする
- 理由を重ねている間は、結果の外に出す件数も消す
- 取得のたびに `LoadingState` から始める
- 取得は `notify: false` で呼ぶ
- CommunityToolkit の `StateContainer` は使わない
- 詳細の一部をオンラインで取得するとき (会員照会の履歴、商品照会の他店在庫) は、`notify: false` で呼ぶ
- 詳細の一部を取得できないときは、見出しを残して「📡 取得できません」を出す
- 端末の記録を開く画面 (取引詳細) は、端末にない記録をオンラインで取得できないとき、メッセージを出して一覧へ戻る
