---
paths:
  - "terminal/**"
---
# 端末 (.NET MAUI)

## 層

- ViewModel は `Services/` の `XxxService` (単機能の部品) と `Usecases/` の `XxxUsecase` (通信 → DB → 完了までの複合的な手順) を呼ぶ。  
  両者はフォルダと名前空間を分ける
- ViewModel は `IDbProvider` を使わず、検証済みの入力を Service / Usecase に渡すだけにする。  
  フィールドは Component → State → Service の順に並べる
- 特定の機能の画面間だけで持ち回る状態 (`SalesContext` / `ReturnContext` / `StockContext` / `CustomerDraft`) は Smart.Navigation の Scope プラグインで注入する。  
  各 ViewModel に同じ名前の `[Scope]` プロパティを宣言し、DI には transient で登録する。  
  遷移パラメータで渡さない
- スキャンのような途中の画面は、呼び出し元の機能の状態を保持するために各コンテキストのプロパティを持つ。  
  使用者に常に紐付く情報 (店舗・端末・担当・シフト) は `Session` に集約する
- ナビゲーションイベントの中の遷移と非同期処理は `PostForwardAsync` / `PostActionAsync` で後回しにする。  
  根の画面の戻るは `HandlesBack = false` で宣言し、`MainActivity` がタスクを背面へ回す (ViewModel は遷移だけを行う)
- 色・列挙型の文言・選択マーク・画面固有の文言は ViewModel ではなく Converter (`BoolToColorConverter` / `MapToColorConverter` / `BoolToTextConverter` / `DisplayNameConverter`) と Trigger で扱う。  
  一覧は `ObservableCollection<T>`。  
  `Show...` はダイアログを出すメソッドだけに使う
- 削除しながら回すときは `Where(...).ToList()` で複製せず、後ろから `RemoveAt` する

## 置き場所と名前

- 表示用の書式や文言 (`ViewHelper`)、業務ルールの文言はモデルではなく `Modules/Helpers/` に置く。  
  UI 固有の定義は Modules の下の Helpers にまとめ、名前は `XxxHelper`
- `Helpers/` にはアプリに依存しない処理だけを置く。  
  日付の書式は `Helpers/DateTimeHelper`、LIKE のエスケープのような SQL の値の組み立ては `Helpers/Data/SqlHelper`
- Converter は `Converters/` にまとめ、画面固有の Converter でも Modules の下には置かない
- 共通のダイアログは `Modules/Dialogs/`、カートは `Models/Cart/`
- 値引は明細値引・取引値引とも同じ `DiscountView` のポップアップを使う。  
  `Modules` 直下に選択肢を組み立てる静的クラス (`XxxChooser`) を置かない

## 入力とポップアップ

- 物理キーボードを前提にしない。  
  ソフトウェアキーボードは設定と、文字の項目 (会員・配送先の名前や住所、検索キーワード) だけに使う
- 数値・番号は電卓のポップアップで入力する。  
  表題と桁数は画面ごとに書かず、`PopupNavigatorExtensions` に入力の種類ごとのメソッド (`InputPhoneAsync` / `InputQuantityAsync` / `InputAmountAsync` など) を用意し、桁数は `Length` の定数から取る
- 理由 (取消・入出金・値引・返品・在庫調整) は定型の選択 (`ReasonSelect`) にし、自由入力は任意にする
- レシート番号のような英数字の番号は、自店の端末番号と連番のように数字だけで入力できる形にする
- 一覧からの選択 (操作メニュー・絞り込み・承認者) は OS の選択ダイアログ (`IDialog.SelectAsync`) ではなく `Select` シート (`IPopupNavigator.ChooseAsync`) を使う
- ポップアップは画面の下端に寄せたシート (幅いっぱい、上角が丸い) にする。  
  重ねて開けるように CommunityToolkit の Popup (`VerticalOptions=End`) で実現し、ページ内のボトムシート部品は使わない。  
  下段のボタン (✕ / ✔) は F キーと同じ位置・配色にし、一覧からの選択のシートだけ外側のタップでも閉じる
- シートの中の文字入力はキーボードの Enter でも確定できるようにする (キーボードが出ている間は下段のボタンが隠れる)
- 画面を離れるときは入力欄のフォーカスを外し、ソフトウェアキーボードを次の画面に残さない (`ShellUpdateBehavior`)

## デザイン

- シェルは POS 画面の配色 (紺のヘッダ、白い行、青い金額) に合わせ、タイトルは左寄せにする
- F キーの割り当てと色は F1 = 戻る・メニュー、F4 = 会計・確定・開設・精算 で揃える
- 支払方法のボタンには `shortName` (省略時は `name`) を表示する

## ローカル DB (`DataAccessor`)

- メソッド名は DB の操作 (`Query` / `Count` / `Insert` / `Update` / `Delete`) で付ける (`UpdateShiftClosedAsync`、`InsertServerShiftAsync`)。  
  業務の動詞は Usecase の名前にする
- ローカルのエンティティのキーによる取得と削除は `[SelectSingle]` / `[Delete]` の Builder を使い、1 文だけの書き込みにはトランザクションを使わない
- マスタは `Pos.Contract` の Response をそのままエンティティにする (この場合だけ Builder 属性に `Table` を書く)。  
  それ以外のエンティティはテーブル名を `[Name]` で持つ
- 列挙型・日付の変換は `Services/DataProfile` に登録する。  
  後から増えた列は `Helpers/Data/SchemaHelper.EnsureColumnAsync` で起動時に足す
