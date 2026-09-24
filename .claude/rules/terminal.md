---
paths:
  - "terminal/**"
---
# 端末 (.NET MAUI)

## 層

- ViewModel は `Services/` の `XxxService` (単機能の部品) と `Usecases/` の `XxxUsecase` (通信 → DB → 完了までの複合的な手順) を呼ぶ。両者はフォルダと名前空間を分ける
- ViewModel は `IDbProvider` を使わず、検証済みの入力を Service / Usecase に渡すだけにする。フィールドは Component → State → Service の順に並べる
- 特定の機能の画面間だけで持ち回る状態 (`SalesContext` / `ReturnContext` / `StockContext` / `CustomerDraft`) は Smart.Navigation の Scope プラグインで注入する。各 ViewModel に同じ名前の `[Scope]` プロパティを宣言し、DI には transient で登録する。遷移パラメータで渡さない
- スキャンのような途中の画面は、呼び出し元の機能の状態を保持するために各コンテキストのプロパティを持つ。使用者に常に紐付く情報 (店舗・端末・担当・シフト) は `Session` に集約する
- ナビゲーションイベントの中の遷移と非同期処理は `PostForwardAsync` / `PostActionAsync` で後回しにする。根の画面の戻るは `HandlesBack = false` で宣言し、`MainActivity` がタスクを背面へ回す (ViewModel は遷移だけを行う)
- 色・列挙型の文言・選択マーク・画面固有の文言は ViewModel ではなく Converter (`BoolToColorConverter` / `MapToColorConverter` / `BoolToTextConverter` / `DisplayNameConverter`) と Trigger で扱う。一覧は `ObservableCollection<T>`。`Show...` はダイアログを出すメソッドだけに使う
- 文言が入り得るプロパティは `EmptyText` ではなく `Message`
- 削除しながら回すときは `Where(...).ToList()` で複製せず、後ろから `RemoveAt` する

## 置き場所と名前

- 表示用の書式や文言 (`ViewHelper`)、業務ルールの文言はモデルではなく `Modules/Helpers/` に置く。UI 固有の定義は Modules の下の Helpers にまとめ、名前は `XxxHelper`
- `Helpers/` にはアプリに依存しない処理だけを置く。日付の書式は `Helpers/DateTimeHelper`、LIKE のエスケープのような SQL の値の組み立ては `Helpers/Data/SqlHelper`
- Converter は `Converters/` にまとめ、画面固有の Converter でも Modules の下には置かない
- 共通のダイアログは `Modules/Dialogs/`、カートは `Models/Cart/`
- 値引は明細値引・取引値引とも同じ `DiscountView` のポップアップを使う。`Modules` 直下に選択肢を組み立てる静的クラス (`XxxChooser`) を置かない

## 入力とポップアップ

- 物理キーボードを前提にしない。ソフトウェアキーボードは設定と、文字の項目 (会員・配送先の名前や住所、検索キーワード) だけに使う
- 数値・番号は電卓のポップアップで入力する。表題と桁数は画面ごとに書かず、`PopupNavigatorExtensions` に入力の種類ごとのメソッド (`InputPhoneAsync` / `InputQuantityAsync` / `InputAmountAsync` など) を用意し、桁数は `Length` の定数から取る
- 理由 (取消・入出金・値引・返品・在庫調整) は定型の選択 (`ReasonSelect`) にし、自由入力は任意にする
- レシート番号のような英数字の番号は、自店の端末番号と連番のように数字だけで入力できる形にする
- 一覧からの選択 (操作メニュー・絞り込み・承認者) は OS の選択ダイアログ (`IDialog.SelectAsync`) ではなく `Select` シート (`IPopupNavigator.ChooseAsync`) を使う
- 確認と情報は `IDialog` (`AskAsync` / `InformationAsync`) で出す。`SheetDialog` が `Confirm` / `Message` のシートに変換するので、ViewModel からこれらのシートを `IPopupNavigator` で開かない
- 画面の遷移時 (`OnNavigatedToAsync`) に `Focus()` しない (キーボードが一覧を隠す)。フォーカスを移すのはクリアのような操作の結果だけ
- ポップアップは画面の下端に寄せたシート (幅いっぱい、上角が丸い) にする。重ねて開けるように CommunityToolkit の Popup (`VerticalOptions=End`) で実現し、ページ内のボトムシート部品は使わない。下段のボタン (✕ / ✔) は F キーと同じ位置・配色にし、一覧からの選択のシートだけ外側のタップでも閉じる
- シートの中の文字入力はキーボードの Enter でも確定できるようにする (キーボードが出ている間は下段のボタンが隠れる)

## デザイン

- 画面の F キーは F1 = 戻る・メニュー、F4 = 会計・確定・開設・精算 (色は `Styles.xaml` の `FunctionButton1〜4`)
- 一覧やスキャン待ちの空の状態は `PosEmptyStack` (絵文字 + 案内文) にし、案内文だけの `Label` にしない
- 時間のかかる描画 (SkiaSharp のレシート画像) は `Task.Run` で背景に回し、終わるまで `PosLoadingIndicator` を出す
- 画面は灰色の背景の見出しと幅いっぱいの白い面で組む (`SectionPanel` / `PosSectionTemplate`)。角丸のカードを並べる作りにせず、角丸はチップ・アバター・ボタン・シートだけに使う
- 主な金額は白い帯 (`PosHeroBorder`)、件数や金額の集計は罫線の表 (`PosStatGrid`) にする
- 状態のある一覧は左端の帯と行の背景で状態を示し、区切り線で並べる。状態の文言はチップ (`StatusChip`) で示す
- チップの記号は `StatusChip.Icon` (Material Icons のグリフ) で付ける。濃い背景のボタンの記号は白い `FontImageSource` (`PosSearchIcon` など) か MaterialIcons のフォントにし、選択で背景が濃くなるボタンはトリガーでアイコンも白にする
- 表示時の浮き上がり (`AnimationOption.EnterAnimation`) は、続けて更新する一覧 (販売の明細・棚卸) には付けない
- オンラインで取る画面の集計中・取得できない表示は結果の上に重ねる (CommunityToolkit の `StateContainer` は使わない)

## ローカル DB (`DataAccessor`)

- メソッド名は DB の操作 (`Query` / `Count` / `Insert` / `Update` / `Delete`) で付ける (`UpdateShiftClosedAsync`、`InsertServerShiftAsync`)。業務の動詞は Usecase の名前にする
- ローカルのエンティティのキーによる取得と削除は `[SelectSingle]` / `[Delete]` の Builder を使い、1 文だけの書き込みにはトランザクションを使わない
- マスタは `Pos.Contract` の Response をそのままエンティティにする (この場合だけ Builder 属性に `Table` を書く)。それ以外のエンティティはテーブル名を `[Name]` で持つ
- 列挙型・日付の変換は `Services/DataProfile` に登録する。後から増えた列は `Helpers/Data/SchemaHelper.EnsureColumnAsync` で起動時に足す
