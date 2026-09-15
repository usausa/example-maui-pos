# 実装の指針

レビューの指摘を「どうあるべきか」の形でまとめたもの。  
新しい指摘を受けたら該当する節に追記し、経緯は書かない (経緯は [decisions.md](decisions.md))。

- [1. 共通](#1-共通)
- [2. 共有プロジェクト (`Pos.Domain` / `Pos.Contract`)](#2-共有プロジェクト-posdomain--poscontract)
- [3. サーバ Core](#3-サーバ-core)
- [4. サーバ Host](#4-サーバ-host)
- [5. 端末](#5-端末)
- [6. ドキュメント](#6-ドキュメント)
- [7. 検証](#7-検証)

---

## 1. 共通

### 層と置き場所

- SQL は Accessor だけが持つ。  
  業務の手順はサーバは `Services/` の `XxxService`、端末は `Usecases/` の `XxxUsecase` に置き、Endpoints / Blazor ページ / ViewModel は入力の検証と表示だけを担う
- 単機能の部品 (`XxxService`) と、通信 → DB → 完了までの複合的な手順 (`XxxUsecase`) はフォルダと名前空間を分ける
- フォルダ直下には共通の部品だけを置く。  
  何かの機能に属するもの (名称の辞書、絞り込みの状態、URL の生成、帳票) はサブフォルダか同階層の専用フォルダに置く
- あるフォルダの中でしか使わないものはそのフォルダに置く (`Components/` でしか使わない基底クラスは `Components/` 直下、ダイアログ用の拡張メソッドはダイアログと同じ場所)
- 特定の画面 (ページ・ダイアログ) だけで使うフォームと検証は、その画面の内部クラスにする
- 1 つの Service だけが返す結果型 (`XxxResult`) は、その Service のファイルの先頭で定義する。  
  複数の Service が使う型 (`DataWriteStatus` / `DataWriteResult<T>`) は独自のファイルにする
- Converter は `Converters/` にまとめる。  
  画面固有の Converter でも Modules の下には置かない
- 拡張メソッドは、複数の項目を意味でまとめて判定するもの (`IsReturnable`) だけに使う。  
  単一の値との比較 (`status == TransactionStatus.Voided`) を拡張メソッドにしない。  
  型に対する拡張メソッドは、その型の定義と同じファイルに書く
- `Builder` という名前は文字列や画像の組み立てだけに使う。  
  データの変換は `XxxMapper`、計算は `XxxCalculator` と呼ぶ
- `Helpers/` にはアプリに依存しない処理だけを置く。  
  アプリ固有の表示用の定義は UI 側 (端末は `Modules/Helpers/`、サーバは `Application/`) に置く

### 名前

- 通信データは `XxxRequest` / `XxxResponse`。  
  クラス名はエンドポイントのクラス名 + メソッド名から付ける (`TransactionCreateRequest`、`CustomerPointHistoryResponse`、`ReportSalesSummaryResponse`)。  
  一覧は `XxxResponse`、その要素は `XxxResponseItem`、入れ子の要素は親の名前に要素名を続ける (`TransactionResponseItemLine`)
- 「DTO」という語はコード・名前空間・文書のどこにも使わない
- DB から読んだ結果の型は `XxxView` に統一する
- 列挙型は `Views` や `Parameters` の中に混ぜず、`Models/Enums` (端末は `Pos.Domain.Enums`) に置く
- 文字列が入り得るプロパティに `EmptyText` のような名前を付けない。  
  汎用の文言なら `Message` とする
- Accessor のメソッド名は DB の操作 (`Query` / `Count` / `Insert` / `Update` / `Delete`) で付ける。  
  取消・精算・取り込みのような業務の動詞は Service / Usecase の名前にし、Accessor は状態を変える UPDATE として `UpdateVoidedAsync` / `UpdateClosedAsync` と呼ぶ
- `Show...` はダイアログを出すメソッドだけに使う

### データ長と入力桁数

- 文字列の長さ (DB の列長、Request の `MaxLength`、フォームの `MaximumLength`、端末の電卓の桁数) は `Pos.Domain.Length` の定数を使い、数値を直接書かない

### 引数の null チェック

- `ArgumentNullException.ThrowIfNull` は書かない (CA1062 は無効にしている)

### SQL

- 2-way SQL は句 (`SELECT` / `FROM` / `WHERE` / `ORDER BY` / `UPDATE` / `SET` / `INSERT INTO` / `VALUES` / `RETURNING`) を行頭に置き、表名・列・条件・値を次の行に 4 桁字下げする。  
  `AND` / `OR` は行頭に置く

  ```sql
  UPDATE
      Customers
  SET
      Code = /*@ code */'',
      Name = /*@ name */'',
      UpdatedAt = /*@ updatedAt */'',
      Version = Version + 1
  WHERE
      Id = /*@ id */''
      AND Version = /*@ version */0
      AND IsDeleted = 0
  ```

- テーブル名は Entity クラスの `[Name("Stores")]` で持ち、Builder 属性 (`[SelectSingle]` / `[Insert]` / `[Delete]`) に `Table` を個別に書かない。  
  Contract の型をそのままエンティティにする端末のマスタだけは例外
- 並び替えの列や集計の軸のような「SQL に展開する値」は文字列で受け取らず、列挙型 (列挙名 = 列名) を受け取って 2-way SQL の中で `/*# sort */` と `/*% if (desc) { */` で展開する。  
  差分同期の `UpdatedAt, Id` 順のような固定の並びも SQL 側の分岐で書く
- `SqlHelper` に置くのは 2-way SQL の `/*# */` から呼ぶ SQL 断片 (集計の GROUP BY 式など) だけ。  
  SQL に関係しない処理 (列の追加、タイムゾーンの修飾子) は別のクラスか呼び出し側に置く
- 更新は `UPDATE ... RETURNING *` を `[QueryFirst]` で受けて更新後の行を返す。  
  更新してから `SELECT` で読み直さない (読み直す間に削除される余地があるため)
- キーによる 1 件の取得と削除は `[SelectSingle]` / `[Delete]` の Builder を使う。  
  1 文だけの書き込みにはトランザクションを使わない
- 起動時に流し込む初期データのような固定の SQL は外部ファイルに置き、`[DirectSql]` + `[Execute]` (第 1 引数の文字列が SQL、残りの引数が `@name` のパラメータ) で実行する。  
  C# で初期データを組み立てない
- 生 SQL のプレースホルダ (`/*# expr */`) は次の 1 トークンだけを置き換える

## 2. 共有プロジェクト (`Pos.Domain` / `Pos.Contract`)

- 長さは `Pos.Domain.Length`、列挙型は `Pos.Domain.Enums` (1 型 1 ファイル)、ロジックは `Pos.Domain.Logic` に置く。  
  Domain は文言を持たず、エラーは `RuleReason` だけを返す
- Request の検証属性 (`Required` / `MaxLength` / `Range`) の値は `Length` の定数から取る
- 通信データの名前は [1. 共通](#名前) のとおり。  
  資源ごとに名前空間とフォルダを分け、在庫調整理由のように独立した資源は独自の名前空間 (`Pos.Contract.AdjustmentReasons`) とエンドポイントを持つ
- 契約でないもの (`JsonDateTimeConverter` / `ProblemResponse`) は Contract に置かず、サーバと端末がそれぞれ持つ
- Contract の型に対する判定 (`IsReturnable`) は、その型と同じファイルの拡張メソッドにする

## 3. サーバ Core

- Accessor は処理の単位でまとめる (`MasterAccessor` にマスタ全種、`ProductAccessor` / `CustomerAccessor` / `TransactionAccessor` / `ShiftAccessor` / `InventoryAccessor` / `ReportAccessor`)。  
  テーブルに紐付かない処理 (PRAGMA、後から増えた列、SQL ファイルの実行) は `GenericAccessor` に置く
- DB の結果は `Models/Views`、Service への入力は `Models/Parameters`、`DataProfile` / `SqlHelper` / `SchemaHelper` は `Accessors/` に置く
- 初期データは Host の `Assets/Data/InitialData.sql` を起動時に読み、`GenericAccessor.ExecuteScriptAsync` で会社設定がない DB へ投入する。  
  `InitialData` クラスは固定 ID だけを持つ
- 一覧の条件は `PagedParameter<TSort>` を基底にした `XxxQueryParameter` で受け、`Sort` は資源ごとの列挙型にする
- 重複 (`IDialect.IsDuplicate`)、楽観ロック (`RETURNING` で行が返らない)、使用中 (件数クエリ) の判定は Service の中で行い、`DataWriteStatus` / `DataWriteResult<T>` で返す。  
  「読んでから更新」で判定しない
- LIKE のエスケープや既定値の補完は Service で行う。  
  複数テーブルにまたがる書き込みは Service の中で `IDbProvider.UsingTxAsync` を使う
- Host だけが使う部品でも、アプリに依存しない基盤 (`JsonDateTimeConverter` など) は Core の `Infrastructure/` に置く
- Service は `AddCoreServices()` に一括登録する

## 4. サーバ Host

### 置き場所

`Application/` はアプリ固有、`Infrastructure/` はアプリに依存しないものを置く。

| 種類 | 置き場所 |
| --- | --- |
| ページ・コンポーネントの基底 (`PageComponentBase` / `AppComponentBase`) | `Components/` 直下 |
| ダイアログの基底と `IDialogService` の拡張 | `Components/Dialogs/` |
| razor 表示用の加工 (`ViewHelper` = 部品の文言と色、`ViewExtensions` = 書式の拡張メソッド) | `Application/` |
| 名称の辞書 (`NameLookup`)、共有する絞り込み (`StoreFilterState`)、ダウンロード URL (`ExportUrls`) | `Application/Lookup` / `Application/State` / `Application/Urls` |
| 帳票 (`XxxReportBuilder`) | `Reports/` (Endpoints と同階層) |
| API の文言・既定値・経路・Problem Details (`ApiRuleText` / `ApiDefaults` / `ApiRoutes` / `ApiProblems`) | `Endpoints/` |
| 列挙値の解析 (`EnumHelper`) | `Helpers/` |
| CSV 出力、ログ、例外処理 | `Infrastructure/Csv` / `Infrastructure/Logging` / `Infrastructure/ExceptionHandling` |
| API のクエリ (`[AsParameters]`) | `Models/Queries/` |
| 複数の画面で使うフォーム | `Models/Forms/` (Entity ↔ Form の変換はフォームが持つ。`Mappers` フォルダは作らない) |
| 1 つのページだけのフォーム | そのページの内部クラス |

- 自前のヘルスチェック (`DatabaseHealthCheck` のような) は置かない

### エンドポイント

- ハンドラは Request → Entity / Parameter の変換 (`[Mapper]`) → Service → Response の変換だけを持つ。  
  静的なユーティリティ (`TryParse` など) をエンドポイントのクラスに書かず、`Helpers/` に置く
- 更新の応答は `DataWriteResult<T>` の行から作る
- 大小比較 (`from` ≤ `to`) のような入力の検証は `[AsParameters]` のクエリ型の `IValidatableObject` で行い、ハンドラの中の `if` にしない。  
  API の入力検証は DataAnnotations で統一し、FluentValidation は管理画面のフォームだけに使う
- 省略された期間の既定値は逆転しないように補う (`to` は今日、`from` が未来ならその日)
- 一覧の `sort` は文字列で受けて列挙型に解析し、不正な値は既定の列にする。  
  レポートの `sort` / `groupBy` の不正な値は 400 にする

### 管理画面

- CSV / PDF などのダウンロード URL はページで組み立てず、`Application/Urls/ExportUrls` で作る
- 描画モードは対話型 (プリレンダリングなし)。  
  エラーと 404 のページは再実行で描画されるため静的 SSR のままにし、常に対話型にはしない
- フォームの検証の長さは `Length` の定数を使う

## 5. 端末

### 入力

- 物理キーボードを前提にしない。  
  ソフトウェアキーボードは設定と、文字の項目 (会員・配送先の名前や住所、検索キーワード) だけに使う
- 数値・番号は電卓のポップアップで入力する。  
  表題と桁数は画面ごとに書かず、`PopupNavigatorExtensions` に入力の種類ごとのメソッド (`InputPhoneAsync` / `InputPostalCodeAsync` / `InputQuantityAsync` / `InputAmountAsync` など) を用意し、桁数は `Length` の定数から取る
- 理由 (取消・入出金・値引・返品・在庫調整) は定型の選択 (`ReasonSelect`) にし、自由入力は任意にする
- レシート番号のような英数字の番号は、自店の端末番号と連番のように数字だけで入力できる形にする
- 一覧からの選択 (操作メニュー・絞り込み・承認者) は OS の選択ダイアログ (`IDialog.SelectAsync`) ではなく `Select` シート (`IPopupNavigator.ChooseAsync`) を使う
- 画面を離れるときは入力欄のフォーカスを外し、ソフトウェアキーボードを次の画面に残さない (`ShellUpdateBehavior`)

### 画面間の状態

- 特定の機能の画面間だけで持ち回る状態 (カート、返品、棚卸、会員編集の下書き) は Smart.Navigation の Scope プラグインで注入する。  
  ViewModel の `[Scope]` プロパティに同じ名前で宣言し、DI には transient で登録する。  
  遷移パラメータで渡さない
- スキャンのような途中の画面は、呼び出し元の機能の状態を保持するために各コンテキストのプロパティを持つ
- 使用者に常に紐付く情報 (店舗・端末・担当・シフト) は `Session` に集約する

### ViewModel

- ViewModel は `IDbProvider` を使わず、検証済みの入力を Service / Usecase に渡すだけにする。  
  フィールドは Component → State → Service の順に並べる
- ナビゲーションイベントの中の遷移と非同期処理は `PostForwardAsync` / `PostActionAsync` で後回しにする
- 色・列挙型の文言・選択マーク・画面固有の文言は ViewModel ではなく Converter (`BoolToColorConverter` / `MapToColorConverter` / `BoolToTextConverter` / `DisplayNameConverter`) と Trigger で扱う
- 根の画面の戻るは `HandlesBack = false` で宣言し、`MainActivity` がタスクを背面へ回す (ViewModel は遷移だけを行う)
- 一覧は `ObservableCollection<T>` にする

### 置き場所と名前

- 表示用の書式や文言 (`ViewHelper`)、業務ルールの文言はモデルではなく `Modules/Helpers/` に置く。  
  UI 固有の定義は Modules の下の Helpers にまとめ、名前は `XxxHelper` にする
- 日付の書式は `Helpers/DateTimeHelper` に集約する
- 共通のダイアログは `Modules/Dialogs/`、カートは `Models/Cart/` に置く
- 値引の選択は明細値引・取引値引とも同じ `DiscountView` のポップアップを使う。  
  `Modules` 直下に選択肢を組み立てる静的クラス (`XxxChooser`) を置かない
- LIKE のエスケープのような SQL の値の組み立ては `Helpers/Data/SqlHelper` に置く
- Accessor のメソッド名は DB の操作で付ける ([1. 共通](#名前))

### デザイン

- シェルは POS 画面の配色 (紺のヘッダ、白い行、青い金額) に合わせ、タイトルは左寄せにする
- F キーの割り当てと色は F1 = 戻る・メニュー、F4 = 会計・確定・開設・精算 で揃える
- 支払方法のボタンには `shortName` (省略時は `name`) を表示する
- ポップアップは画面の下端に寄せたシート (幅いっぱい、上角が丸い) にする。  
  重ねて開けるように CommunityToolkit の Popup (`VerticalOptions=End`) で実現し、ページ内のボトムシート部品は使わない。  
  下段のボタン (✕ / ✔) は F キーと同じ位置・配色にし、一覧からの選択のシートだけ外側のタップでも閉じる
- シートの中の文字入力はキーボードの Enter でも確定できるようにする (キーボードが出ている間は下段のボタンが隠れる)

### コレクション

- 削除しながら回すときは `Where(...).ToList()` で複製せず、後ろから `RemoveAt` する

## 6. ドキュメント

- 設計文書は現状だけを書く。  
  経緯は `decisions.md` にだけ書き、日付は書かない
- ソースから設計文書 (節番号・`§`・画面 ID・決定番号) を参照しない
- README は主要な画面と文書へのリンクだけを載せる
- Markdown は `。` で文を終え、2 スペースで改行する (表・見出し・コードは除く)
- レビューの指摘は本書に「どうあるべきか」として追記し、該当する設計文書と `AGENTS.md` も合わせて直す

## 7. 検証

- 作業の単位ごとに、ビルド (Release、警告 0)、テスト (Domain / 単体 / 統合)、InspectCode (両ソリューション、0 件) を通す
- 警告の抑止は理由のあるものだけにし、抑止する前に確認する
- テストは実行順に依存させない。  
  同じ DB (フィクスチャ) を共有するテストが登録・削除した行を、件数の検証に含めない
- 端末は Debug と Release の両方をビルドし、UI に関わる変更はエミュレータで動作を確認する
