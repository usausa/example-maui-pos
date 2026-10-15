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
  - "terminal/Pos.Terminal/Platforms/Android/MainActivity.cs"
---
# 端末の画面

## 画面と遷移

- XAML で画面 ID を渡すときは `{markup:ViewId Xxx}` と書く (`x:Static` にしない)
- 起動の途中 (`App.OnStart`) の知らせ (前回の異常終了、ローカル DB を開けない) は、シートの仕組みが揃う前なので OS のダイアログで出す
- 画面は `ViewId` に足し、View に `[View(ViewId.Xxx)]` と `[Hierarchy(n)]` を付ける (n は根からの深さ。先へ進む画面ほど大きい)
- XAML には `shell:ShellProperty` を書く。使わないキーは `—` で無効にし、押せない条件は ViewModel の bool (`CanXxx`、`HasXxx`、`XxxEnabled`) か Converter に結ぶ
- 遷移は `ForwardAsync` で行い、戻り先は固定か `Parameters.WithReturnTo` で受ける。パラメータは `Parameters` に `WithXxx` と `GetXxx` の対で足す
- ナビゲーションイベントの中の遷移と非同期処理は、`PostForwardAsync`、`PostActionAsync` で後回しにする
- 根の画面の戻るは `HandlesBack = false` で宣言し、`MainActivity` がタスクを背面へ回す (ViewModel は遷移だけを行う)
- F1 は戻る (戻れない画面は `—` で無効、根の画面は設定など) にし、F1 と戻るを同じ遷移にする
- スキャンできる画面の F2 はスキャン (カメラを出す画面 (スキャン、初期設定) はライト)、F4 は画面の主操作 (確定、登録、会計。なければ更新や次の画面) にする
- 例外として、ホームは F キーの列を出さず (`FunctionVisible="False"`)、販売の F1 はメニュー、会員選択の F4 は会員の解除にし、完了の画面は F1 を無効にして戻るを F4 (次へ) と同じにする
- ViewModel のプロパティは `[ObservableProperty] public partial`、コマンドは `MakeAsyncCommand`、`MakeDelegateCommand` で作る
- ViewModel のクラスの前に「画面名: 役割」の 1 行コメントを書く

## ViewModel と表示

- 表示中に状態 (bool、列挙型) で切り替わる色・文言・記号は ViewModel で組まず、Smart.Maui の `BoolToXxxConverter`、`MapToXxxConverter` を `Styles.xaml` にキー付きで構成するか、Trigger で切り替える (表示のときに 1 回組む文言と、行の表示用の文字列は除く)
- 列挙型の文言は `ViewHelper.Name` と `DisplayNameConverter` の両方に足す (漏れると列挙名が出る)
- 業務ルールの違反 (`RuleError`) は `ViewHelper.Reason` で出す
- 一覧は `ObservableCollection<T>` にし、`ObservableCollection.Replace` で入れ替える
- 一覧の行は ViewModel のファイルの先頭に `XxxItem` (表示用の文字列を持つ record、変わる状態があれば `NotificationObject`) を置く。複数の画面で使う集計の行は `Models/SummaryRow` を使う
- 入力欄が空のときの表示 (「タップして入力」「タップして選択」) は案内ではなく空の状態の表示で、`EmptyTextConverter` で出す
- 一覧が取得できない・該当がないときに切り替わる文言は `Message` プロパティ (既定は空) に入れ、変わらない空の文言は XAML に書く
- `ShowXxx` はダイアログを出すメソッドだけに使う
- 削除しながら回すときは `Where(...).ToList()` で複製せず、後ろから `RemoveAt` する

## 入力とポップアップ

- 物理キーボードを前提にしない。ソフトウェアキーボードは設定と、文字を含む項目 (名前、住所、メール、備考、シリアル番号、会員番号、検索キーワード) だけに使う
- 数値・番号は電卓のポップアップで入力し、`PopupNavigatorExtensions` に入力の種類ごとのメソッド (`InputPhoneAsync`、`InputQuantityAsync`、`InputAmountAsync`、`InputPinAsync` など) を置く
- 番号の表題は入力の種類ごとのメソッドで決め (PIN と伝票番号は文脈を入れた表題を渡す)、数値は画面の文脈 (最大数、現在庫) を入れた表題を渡す
- `NumberInputParameter` は画面で作らず `PopupNavigatorExtensions` を通す (番号は桁数、PIN は伏せ字)
- レシート番号のような英数字の番号は、自店の端末番号と連番のように数字だけで入力できる形にする
- 理由は定型の選択 (`ReasonSelect`) にし、自由入力は任意にする
- 一覧からの選択 (操作メニュー、絞り込み、承認者) は OS の選択ダイアログ (`IDialog.SelectAsync`) ではなく `Select` シート (`PopupNavigatorExtensions.ChooseAsync`) を使う
- 確認と情報は `IDialog` の `AskAsync` と `InformationAsync` で出す。`SheetDialog` が `Confirm` と `Message` のシートに変換するので、ViewModel からこれらのシートを `IPopupNavigator` で開かない
- 操作の完了は `Toast`、入力の不足や失敗は `InformationAsync`、取り消しにくい操作の前は `AskAsync` (OK に操作の動詞を入れる) で知らせる。控えが要る結果 (受注番号) は `InformationAsync` にする
- ポップアップは `DialogId` に足し、View は `[Popup(DialogId.Xxx)]`、ViewModel は `AppDialogViewModelBase` と `IPopupInitialize<T>` で受けて `CloseAsync(result)` で返す (引数なし = キャンセル)
- シートの下段のボタンは `InputCancelButton` と `InputConfirmButton` にし、コマンドは `CloseCommand` と `CommitCommand` にする
- ポップアップは画面の下端に寄せた幅いっぱいのシート (上角が丸い) にし、下段のボタン (✕、✔) は F キーと同じ位置・配色にする。一覧からの選択のシートだけ外側のタップでも閉じる
- 同じ入力を複数の画面で使うときは、ポップアップを 1 つにして `DialogId` で開く (値引は明細値引・取引値引とも `DiscountView`)
- シートの中の文字入力はキーボードの Enter でも確定できるようにする (キーボードが出ている間は下段のボタンが隠れる)
- 文字の欄は `EntryController` を `EntryBind.Controller` で結び、Enter で確定する欄は `new EntryController(command)` にする
- 電卓で入れる欄は `PosFieldInputButton` にし、空の表示は `EmptyTextConverter` で出す
- 画面の遷移時 (`OnNavigatedToAsync`) に `Focus()` しない (キーボードが一覧を隠す)。フォーカスを移すのは、クリアや入力不足の知らせのような操作の結果だけにする

## デザイン

- 画面は灰色の背景の見出しと幅いっぱいの白い面で組む (`SectionPanel`、`PosSectionTemplate`)。角丸のカードを並べる作りにせず、角丸はチップ・アバター・ボタン・シートだけに使う
- 主な金額は白い帯 (`PosHeroBorder`)、件数や金額の集計は罫線の表 (`PosStatGrid`) にする
- 状態のある一覧は左端の帯と行の背景で状態を示し、区切り線で並べる。状態の文言はチップ (`StatusChip`) で示す
- チップのアイコンは `StatusChip.Icon`、色付きのボタンの文言に添えるアイコンは白い `FontImageSource` で付ける
- ボタンの `FontImageSource` は `Markup/AppIcons` の静的フィールドに足し、XAML から `{x:Static markup:AppIcons.Xxx}` で使う (`Styles.xaml` や画面の `Resources` に置かない)
- 選ぶと背景が濃くなるボタンは、選ばれていないときは濃い色のグリフにし、選んだら Trigger でアイコンも白 (`XxxSelected`) にする
- アイコンだけのボタン (`PosMethodIconButton`、`PosSelectIconButton`、`PosExpandButton`、シートの `InputDeleteButton`) は、`Text` にグリフを置き、フォントはスタイルで付ける
- 例外として、シートの下段の ✕、✔ と、テンキーの ⌫、塗りつぶしの小さな丸ボタン (`PosQuantityButton`) の `+`、`-`、✕ は、Material Icons にせず U+FE0F を付けない文字にする
- 値や行を外す ✕ は、灰色の小さな丸ボタン (`PosQuantityButton` を `GrayLighten1` にしたもの) にする
- 画面を移ったり操作を始めたりする白いボタン (ホームのキーの `MenuButton`、設定の操作) は、文言の前に色付きの絵文字を付ける (値を選ぶ白いボタンには付けない)
- カメラの画面 (スキャン、初期設定) の帯の知らせは、追加できたときを白い文字の ✓、ほかのとき (見つからない、設定 QR でない、カメラの権限がない、登録できない) を ✕ で始める
- 例外として、トーストの記号は色付きの絵文字にする (OS が描くので、背景は Android の版とテーマで明るくも暗くもなる)
- 例外として、選択肢のすべてに付ける印 (返金方法の `SelectedMarkConverter` の ◉、○) は、ラジオボタンの代わりなので文字にする (選んだ行だけに付ける印は ✅)
- 一覧やスキャン待ちの空の状態は `PosEmptyStack` の絵文字にし、文言は該当がない・取得できないときだけ添える
- 処理中の表示は IDialog のインジケータだけにする (通信は `NetworkService` が出し、通信以外の時間のかかる処理と同期は `IDialog.Indicator()`)。画面に「読み込んでいます」のような文言や `ActivityIndicator` を置かない
- 段階の文言を出す `IDialog.Loading` は使わない (MAUI のウインドウの重ね描きなので、カメラのある画面ではカメラの中に描かれて読み取り枠の下になる)
- オンラインで取る一覧・集計と詳細の全体 (受注詳細)、端末の一覧をオンラインの結果に置き換える検索 (取引履歴のシリアル番号) は、`CurrentState` に `ViewHelper.LoadingState`、`OfflineState` を入れ、取得中は結果を空の面で隠し、取得できないときは理由を結果の上に重ねる (`EqualsConverter`)
- 理由を重ねている間は、結果に対する操作 (F キー) を押せないようにし、結果の外に出す件数も消す
- 取得のたびに `LoadingState` から始め、`notify: false` で呼ぶ (CommunityToolkit の `StateContainer` は使わない)
- 詳細の一部をオンラインで取るとき (会員照会の履歴、商品照会の他店在庫) は、取得できなくても見出しを残し、「📡 取得できません」を出す
- 端末の記録を開く画面 (取引詳細) が、端末にない記録をオンラインで取れないときは、知らせて一覧へ戻る
- 表示時の浮き上がり (`AnimationOption.EnterAnimation`) は、続けて更新する一覧 (販売の明細、棚卸) には付けない
