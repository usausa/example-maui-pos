---
paths:
  - "terminal/Pos.Terminal/Modules/**"
  - "terminal/Pos.Terminal/Controls/**"
  - "terminal/Pos.Terminal/Converters/**"
  - "terminal/Pos.Terminal/Resources/Styles/**"
---
# 端末の画面

## 画面と遷移

- 画面は `ViewId` に足し、View に `[View(ViewId.Xxx)]` と `[Hierarchy(n)]`、XAML に `shell:ShellProperty` (使わないキーは `—` で無効、押せない条件は `CanXxx` に結ぶ) を書き、F1 と戻るを同じ遷移にする
- 遷移は `ForwardAsync` で行い、戻り先は固定か `Parameters.WithReturnTo` で受ける。パラメータは `Parameters` に `WithXxx` / `GetXxx` の対で足す
- ナビゲーションイベントの中の遷移と非同期処理は `PostForwardAsync` / `PostActionAsync` で後回しにする。根の画面の戻るは `HandlesBack = false` で宣言し、`MainActivity` がタスクを背面へ回す (ViewModel は遷移だけを行う)
- F1 は戻る (戻れない画面は `—` で無効、根の画面は設定など)、スキャンできる画面の F2 はスキャン、F4 は画面の主操作 (確定、登録、会計。なければ更新や次の画面) にする
- ViewModel のプロパティは `[ObservableProperty] public partial`、コマンドは `MakeAsyncCommand` / `MakeDelegateCommand` で作る

## ViewModel と表示

- 状態 (bool、列挙型) で切り替わる色・文言・記号は ViewModel で組まず、Smart.Maui の `BoolToXxxConverter` / `MapToXxxConverter` を `Styles.xaml` にキー付きで構成するか、Trigger で切り替える
- 列挙型の文言は `ViewHelper.Name` と `DisplayNameConverter` の両方に足す (漏れると列挙名が出る)。`XxxUsecase.Validate` の `RuleError` は `ViewHelper.Reason` で出す
- 一覧は `ObservableCollection<T>` にする。行は ViewModel のファイルの先頭に `XxxItem` (表示用の文字列を持つ record、変わる状態があれば `NotificationObject`) を置き、`ObservableCollection.Replace` で入れ替える
- 一覧の空・取得できないときの案内文は `Message` プロパティに入れる
- `ShowXxx` はダイアログを出すメソッドだけに使う
- 削除しながら回すときは `Where(...).ToList()` で複製せず、後ろから `RemoveAt` する

## 入力とポップアップ

- 物理キーボードを前提にしない。ソフトウェアキーボードは設定と、文字を含む項目 (名前、住所、メール、備考、シリアル番号、会員番号、検索キーワード) だけに使う
- 数値・番号は電卓のポップアップで入力し、`PopupNavigatorExtensions` に入力の種類ごとのメソッド (`InputPhoneAsync`、`InputQuantityAsync`、`InputAmountAsync`、`InputPinAsync` など) を置く。番号は表題も固定し、数値は画面の文脈 (最大数、現在庫) を入れた表題を渡す
- `NumberInputParameter` は画面で作らず `PopupNavigatorExtensions` を通す (番号は桁数、PIN は伏せ字)
- レシート番号のような英数字の番号は、自店の端末番号と連番のように数字だけで入力できる形にする
- 理由は定型の選択 (`ReasonSelect`) にし、自由入力は任意にする
- 一覧からの選択 (操作メニュー、絞り込み、承認者) は OS の選択ダイアログ (`IDialog.SelectAsync`) ではなく `Select` シート (`PopupNavigatorExtensions.ChooseAsync`) を使う
- 確認と情報は `IDialog` (`AskAsync` / `InformationAsync`) で出す。`SheetDialog` が `Confirm` / `Message` のシートに変換するので、ViewModel からこれらのシートを `IPopupNavigator` で開かない
- 操作の完了は `Toast`、入力の不足や失敗は `InformationAsync`、取り消しにくい操作の前は `AskAsync` (OK に操作の動詞を入れる) で知らせる。控えが要る結果 (受注番号) は `InformationAsync` にする
- ポップアップは `DialogId` に足し、View は `[Popup(DialogId.Xxx)]`、ViewModel は `AppDialogViewModelBase` と `IPopupInitialize<T>` で受けて `CloseAsync(result)` で返す (引数なし = キャンセル)。下段のボタンは `InputCancelButton` / `InputConfirmButton`
- ポップアップは画面の下端に寄せた幅いっぱいのシート (上角が丸い) にし、下段のボタン (✕ / ✔) は F キーと同じ位置・配色にする。一覧からの選択のシートだけ外側のタップでも閉じる
- 同じ入力を複数の画面で使うときは、ポップアップを 1 つにして `DialogId` で開く (値引は明細値引・取引値引とも `DiscountView`)
- シートの中の文字入力はキーボードの Enter でも確定できるようにする (キーボードが出ている間は下段のボタンが隠れる)
- 文字の欄は `EntryController` を `EntryBind.Controller` で結び、Enter で確定する欄は `new EntryController(command)` にする。電卓で入れる欄は `PosFieldInputButton` にし、空の表示は `EmptyTextConverter` で出す
- 画面の遷移時 (`OnNavigatedToAsync`) に `Focus()` しない (キーボードが一覧を隠す)。フォーカスを移すのは、クリアや入力不足の知らせのような操作の結果だけにする

## デザイン

- 画面は灰色の背景の見出しと幅いっぱいの白い面で組む (`SectionPanel` / `PosSectionTemplate`)。角丸のカードを並べる作りにせず、角丸はチップ・アバター・ボタン・シートだけに使う
- 主な金額は白い帯 (`PosHeroBorder`)、件数や金額の集計は罫線の表 (`PosStatGrid`) にする
- 状態のある一覧は左端の帯と行の背景で状態を示し、区切り線で並べる。状態の文言はチップ (`StatusChip`) で示す
- 記号は、チップなら `StatusChip.Icon` (Material Icons のグリフ)、濃い背景のボタンなら白い `FontImageSource` (`PosSearchIcon` など) で付ける。選択で背景が濃くなるボタンは Trigger でアイコンも白にする
- 一覧やスキャン待ちの空の状態は `PosEmptyStack` (絵文字 + 案内文) にし、案内文だけの `Label` にしない
- 時間のかかる処理を待つ画面は `PosLoadingIndicator` を出す
- オンラインで取る一覧・集計は `CurrentState` に `ViewHelper.LoadingState` / `OfflineState` を入れて `EqualsConverter` で結果の上に重ね、取得のたびに `LoadingState` から始めて `notify: false` で呼ぶ (CommunityToolkit の `StateContainer` は使わない)
- 表示時の浮き上がり (`AnimationOption.EnterAnimation`) は、続けて更新する一覧 (販売の明細、棚卸) には付けない
