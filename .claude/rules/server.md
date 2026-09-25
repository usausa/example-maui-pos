---
paths:
  - "server/**"
---
# サーバ (ASP.NET Core)

## Core

- Endpoints と Blazor ページは `Services/` の `XxxService` を呼び、Service が Accessor と `Pos.Domain` を使う。Web 系の Core は Service だけで Usecase 層を置かない (件数 + ページ取得の合成程度は Service のメソッド)。Service は個別に DI 登録しない (`AddCoreServices()` が自動で登録する)
- DB から読んだ結果は `Models/Views` の `XxxView` に統一する。列挙型 (並び順など) は `Views` や `Parameters` に混ぜず `Models/Enums` に置く。Service への入力は `Models/Parameters` (一覧は `PagedParameter<TSort>` を基底にした `XxxQueryParameter`。`Sort` は資源ごとの列挙型)
- 1 つの Service だけが返す結果型 (`XxxResult`) は、その Service のファイルの先頭で定義する。複数の Service で使う型 (`DataWriteStatus` / `DataWriteResult<T>`) は独自のファイルにする
- 重複 (`IDialect.IsDuplicate`)、楽観ロック (`RETURNING` で行が返らない)、使用中 (件数クエリ) の判定は Service の中で行い、`DataWriteStatus` / `DataWriteResult<T>` で返す。「読んでから更新」で判定しない
- LIKE のエスケープと既定値の補完は Service で行う。複数テーブルにまたがる書き込みは Service の中で `IDbProvider.UsingTxAsync` を使う
- 現在時刻 (`TimeProvider`) を扱うのは Service と帳票だけ。「今日」「既定の期間」「通信中とみなす条件」「登録時刻」は業務の規則なので Service が持つ (`ReportService.Today` / `ResolvePeriod`、`TerminalService.IsOnline`、省略された `OccurredAt` の補完)
- 省略された期間の既定値は逆転しないように補う (`to` は今日、`from` が未来ならその日、`from` は `to` の 30 日前)
- Host だけが使う部品でも、アプリに依存しない基盤 (`JsonDateTimeConverter` など) は Core の `Infrastructure/` に置く

## Accessor

- 新しいテーブルの Accessor は処理の単位でまとめる (マスタは `MasterAccessor`、それ以外は `TransactionAccessor` のように資源ごと)。テーブルに紐付かない処理 (PRAGMA、後から増えた列、スキーマと初期データの SQL ファイルの実行) は `GenericAccessor`
- メソッド名は DB の操作 (`Query` / `Count` / `Insert` / `Update` / `Delete`) で付ける。取消や精算のような業務の動詞は Service の名前にし、Accessor は状態を変える UPDATE として `UpdateVoidedAsync` / `UpdateClosedAsync` と呼ぶ
- テーブル名は Entity クラスの `[Name("Stores")]` で持ち、Builder 属性 (`[SelectSingle]` / `[Insert]` / `[Delete]`) に `Table` を書かない
- 更新は `UPDATE ... RETURNING *` を `[QueryFirst]` で受けて更新後の行を返し、更新してから読み直さない (読み直す間に削除される余地がある)。更新の引数は列ごとに渡す (`/*@ entity.Prop */` にはコンバータが効かない)
- 列挙型は `DataProfile` の `EnumTextConverter<T>` で文字列として保存する (新しい列挙型は `DataProfile` に登録する)。日付・日時は `DateOnlyTextConverter` / `DateTimeTextConverter`
- `SqlHelper` に置くのは 2-way SQL の `/*# */` から呼ぶ SQL 断片 (集計の GROUP BY 式など) だけ。SQL に関係しない処理 (列の追加、タイムゾーンの修飾子) は `SchemaHelper` や Service に置く
- 後から増えた列は `SchemaHelper.EnsureColumnAsync` で起動時に足して初期値を補完し、既存の DB を壊さない
- 初期データの追加・変更は `Host/Assets/Data/InitialData.sql` に書く (C# で組み立てない)

## Host

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
| 1 つのページだけのフォームと検証 | そのページの内部クラス |

- 自前のヘルスチェック (`DatabaseHealthCheck` のような) は置かない

### 基盤

- `Program.cs` は `ConfigureXxx()` / `UseXxx()` / `MapXxx()` の宣言列挙だけにし、実体は `Application/ApplicationExtensions.cs` に区切りコメント付きで書く (節の順 = 呼び出し順)
- ミドルウェアの順序は ForwardedHeaders → W3CLog → ErrorHandler → UseRouting → Compression → HttpLog → Authentication → Authorization → RateLimiter → Antiforgery → Endpoints。例外ハンドラーは圧縮の外 (標準どおり)。W3C ログは例外ハンドラーの外 (未処理例外の 500 を記録する)、HTTP ログは圧縮の内 (ダンプが展開後。未処理例外は 200 と記録される開発用)。`UseWhen` 内の `UseExceptionHandler("/error")` の再実行は暗黙のルーティングに乗らないので `UseRouting()` を明示する
- DI の登録は `ConfigureComponents` に System → Data → Service → Report → Setting の順で区切りコメントを付けて書く。既定は Singleton
- 設定は `Settings/XxxSetting` (DataAnnotations で制約) を `AddOptions<T>().BindConfiguration().ValidateDataAnnotations().ValidateOnStart()` で登録し、値を Singleton で再登録する。業務コードに `IOptions<T>` を渡さない
- ログは `Application/Log.cs` の `[LoggerMessage]` に集約する (Info~ / Warn~ / Error~ の命名、`key=[{value}]` の書式)。文字列補間でログを書かない
- 計測は BCL の `Meter` / `ActivitySource` (`Application/Telemetry/ApplicationInstrument`) で行い、エクスポータは `ConfigureTelemetry` にだけ書く。要求ごとの文脈 (接続元アドレス) は `IHttpContextAccessor` からログ出力時に読み、`CallbackEnricher` で全ログ行に付ける (専用のミドルウェアは置かない)
- マッピングは使うクラスの中に `[Mapper] private static partial` で局所化する。複数のクラスから使うときだけ `Application/ModelMapper`

### エンドポイント

- API のグループは `MapApiGroup` で作る (要求数と長時間実行の計測が付く)


- ハンドラは Request → Entity / Parameter の変換 (`[Mapper]`) → Service → Response の変換だけを持つ。静的なユーティリティ (`TryParse` など) はエンドポイントのクラスに書かず、`Helpers/` に置く
- 更新の応答は `DataWriteResult<T>` の行から作る
- 大小比較 (`from` ≤ `to`) のような入力の検証は `[AsParameters]` のクエリ型の `IValidatableObject` で行い、ハンドラの中の `if` にしない。API の入力検証は DataAnnotations で統一し、FluentValidation は管理画面のフォームだけに使う
- 一覧の `sort` は文字列で受けて列挙型に解析し、不正な値は既定の列にする。レポートの `sort` / `groupBy` の不正な値は 400 にする

### 管理画面

- razor の表示用の加工は `Application/ViewHelper` と `ViewExtensions` だけに置く
- CSV / PDF などのダウンロード URL はページで組み立てず、`Application/Urls/ExportUrls` で作る
- 描画モードは対話型 (プリレンダリングなし)。エラーと 404 のページは再実行で描画されるため静的 SSR のままにし、常に対話型にはしない
- フォームの検証の長さは `Length` の定数を使う
- スタイルは `wwwroot/css/app.css` のクラスに集約する。`.razor.css` を作らず、要素に `Style=` / `style=` を書かない (テーブルの列幅も `w-120` のような幅クラスを app.css に定義して使う)。MudBlazor のユーティリティクラス (`pa-3`、`mud-width-full`、`font-weight-bold`) と併記する
- `MudTable` の `FooterContent` は `<tr>` の中に描画されるので `MudTFootRow` を書かず `MudTd` を直接置く (太字は `FooterClass`)
- 見出しに付ける件数の `MudBadge` は見出しの右に縦中央で並べる (`Origin="Origin.CenterRight"`、`BadgeClass="ml-1"`)。`Overlap` で右上に重ねない (見出しの余白の分だけ行から浮く)
- 状態のチップは `ViewHelper` で (文言, 色, アイコン) の組にし、文言に絵文字を入れない (`StatusChip` が `MudChip` の `Icon` で出す)
- 横スクロールする `MudDataGrid` (`grid-nowrap`) では操作列を `StickyRight="true"` にし、名称のように折り返してよい列は `CellClass="cell-wrap"` にする (`grid-nowrap` が表の `width: max-content` を `100%` に戻すので、幅が足りないときだけ折り返す)。一覧の日時は年なしの `ToShortDateTimeText()`、詳細は `ToDateTimeText()`
- `MudChart` の描画領域は 650×400 の比率で `Height` に合わせて拡大される (幅は高さで決める)。金額の軸は `YAxisFormat`、ラベルは短く (日別は月日だけ)、13 本以上は `XAxisLabelRotation` で斜めにする
- 絞り込みの入力欄は行の残りいっぱいに伸びるので、期間 (`MudDateRangePicker`) は `filter-range`、文字の検索欄は `search-field` で上限を付け、他は `min-w-*`
- ページは `TimeProvider` を注入しない。期間の既定は `ReportService.Today` / `ResolvePeriod`、通信中の表示は `TerminalService.IsOnline`、登録時刻は Service に任せる
