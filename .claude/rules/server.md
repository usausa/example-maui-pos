---
paths:
  - "server/src/**"
  - "server/tools/**"
---
# サーバ (ASP.NET Core)

Accessor の書き方は accessor.md、SQL は sql.md、管理画面は server-ui.md に置く。

## Core

- Endpoints と Blazor ページは `Services/` の `XxxService` を呼び、Service が Accessor と `Pos.Domain` を使う
- サーバの Core は Service だけにし、Usecase 層を置かない (件数 + ページ取得の合成も Service のメソッドにする)
- Service は個別に DI 登録しない (`AddCoreServices()` が自動で登録する)
- 表の行は `Models/Entity` の `XxxEntity`、結合・集計の結果と Service が組み立てる複合 (明細付き、帳票) は `Models/Views` の `XxxView` にする
- Service への入力は `Models/Parameters` に置く
- 並び順を選べる一覧は `PagedParameter<TSort>` を基底にした `XxxQueryParameter` (`Sort` は資源ごとの列挙型) にする。並び順のない一覧は、絞り込みがあれば `Page` と `Size` を持つ `XxxQueryParameter` にし、親の ID とページだけなら引数で受ける
- 列挙型 (並び順など) は `Views` や `Parameters` に混ぜず `Models/Enums` に置く
- 1 つの Service だけが返す結果型 (`XxxResult`) とその状態 (`XxxResultStatus`) は、その Service のファイルの先頭で定義する。複数の Service で使う型 (`DataWriteStatus`、`DataWriteResult<T>`) は独自のファイルにする
- 重複 (`IDialect.IsDuplicate`)、楽観ロック (`RETURNING` で行が返らない)、使用中 (件数クエリ) の判定は Service の中で行い、`DataWriteStatus` / `DataWriteResult<T>` で返す。「読んでから更新」で判定しない
- `IDialect.IsDuplicate` は一意・主キーの違反だけを重複にする (外部キーの違反を `DUPLICATE_CODE` にしない)
- 書き込みが指す参照先は Service が書く前に確かめ、なければ 422 にする (マスタは `Invalid`、ほかは `RuleError`)。外部キーの違反 (500) に任せない (端末の未送信が要確認にならずに送り直し続ける)
- 参照先の確かめでは、管理の入力 (マスタ、発注、入荷、移動) は削除済みもないものとし、端末の記録 (シフト、入出金、受注) は行があればよい (記録した後の無効・削除で拒まない)
- マスタの登録は、Service が `Id` (`Guid.CreateVersion7()`)、`CreatedAt`、`UpdatedAt`、`Version = 1` を補って `ServiceHelper.InsertAsync` で行う。更新は `ServiceHelper.UpdateAsync` を使う
- LIKE のパターンは Service で作る (`ServiceHelper.ToLikePattern`)
- 状態の遷移は `UpdateXxxedAsync` で行い、行が返らなければ読み直して NotFound か業務ルール違反 (`XxxLogic.ValidateXxx`) を返す
- 複数の文にまたがる書き込みは、Service の中で `IDbProvider.UsingTxAsync` を使う
- 業務で使う現在時刻 (`TimeProvider`) を読むのは Service と帳票だけにし、ページとエンドポイントは時計を持たない (計測の経過時間は除く)
- 「今日」「既定の期間」「通信中とみなす条件」「登録時刻」は業務の規則なので Service が持つ (`ReportService.Today`、`ResolvePeriod`、`TerminalService.IsOnline`、省略された `OccurredAt` の補完)
- 取引・シフト・在庫・受注・日次締めを書いた Service は、成功後に `ChangeNotificationService.Notify(DataChangeKind.Xxx)` を呼ぶ
- 初期データは SQL ファイルに書き (C# で組み立てない)、設定から作るもの (初期の管理者) だけ Service の `InitializeAsync` で作る
- アカウントの役割・有効・パスワードを変える更新は `Version` を進める (版が変わるとログイン中のセッションが切れる)。最終ログインのような付随列は版を進めない
- パスワード、PIN、端末のトークンは平文で持たず (`IPasswordProvider`、`PinHasher`、SHA-256)、応答にも出さない
- 例外として、端末の同期応答は `PinHash` を含め、ペアリングの応答はトークンを 1 回だけ返す
- 基盤のうち Core の機能が使うもの (データ、画像の形式、JSON、パスワード) は Core の `Infrastructure/` に置く

## Host

`Application/` はアプリ固有の構成と部品、`Infrastructure/` は画面や業務に依存しない基盤を置く (基盤からも `Application/Log.cs` のログと `Application/Telemetry` の計測は使う)。

| 種類 | 置き場所 |
| --- | --- |
| ページ (`XxxPage`) | `Components/Pages/` |
| レイアウト | `Components/Layout/` |
| ページ・コンポーネントの基底 (`PageComponentBase`、`AppComponentBase`) | `Components/` 直下 |
| 画面の部品 (`StatusChip` など) | `Components/Controls/` |
| ダイアログ (編集の `XxxEditDialog`、詳細や操作の `XxxDialog`)、その基底 (`EditDialogBase<TForm>`) と `IDialogService` の拡張 | `Components/Dialogs/` |
| razor 表示用の加工 (`ViewHelper` = 部品の文言と色、`ViewExtensions` = 書式の拡張メソッド) | `Application/` |
| 認証・認可 (`AuthClaims`、`Policies`、`TerminalAccess`、認証ハンドラ、`AuthenticationStateProvider`) | `Application/Authentication/` |
| 名称の辞書 (`NameLookup`) | `Application/Lookup/` |
| 共有する絞り込み (`StoreFilterState`) | `Application/State/` |
| 計測 (`ApplicationInstrument`) | `Application/Telemetry/` |
| ダウンロード URL (`ExportUrls`) | `Application/Urls/` |
| 設定 (`XxxSetting`) | `Settings/` |
| 帳票 (`XxxReportBuilder`) | `Reports/` (Endpoints と同階層) |
| API の文言・既定値・経路・Problem Details (`ApiRuleText`、`ApiDefaults`、`ApiRoutes`、`ApiProblems`) | `Endpoints/` |
| 要求の解析 (`EnumHelper`、`RequestHelper`) | `Helpers/` |
| Host だけが使う基盤 (CSV の入出力、例外処理、フィルター、ログ、帳票のフォント) | `Infrastructure/` の `Csv`、`ExceptionHandling`、`Filters`、`Logging`、`Reports` |
| API のクエリ (`[AsParameters]`) | `Models/Queries/` |
| CSV の行 (`XxxExportRow` と見出しの `XxxCsvHeader`、`XxxImportRow`) | `Models/Export/`、`Models/Import/` |
| ダイアログと受け渡すフォーム (`XxxForm` と `XxxFormValidator`) | `Models/Forms/` (Entity ↔ Form の変換はフォームが持つ。`Mappers` フォルダは作らない) |
| ページの中で完結するフォームと検証 | そのページの内部クラス |

- ヘルスチェックは生存確認 (`self`) だけにし、DB などの依存先を確かめるチェックは足さない

### 基盤

- `Program.cs` は拡張メソッドの呼び出しの列挙だけにし、実体は `Application/ApplicationExtensions.cs` に機能ごとの節で書く (節は `ConfigureXxx` の順、`UseXxx` は同じ機能の節)
- ミドルウェアの順序は ForwardedHeaders → W3CLog → ErrorHandler → UseRouting → Compression → HttpLog → Authentication → Authorization → RateLimiter → Antiforgery → Endpoints
- 例外ハンドラーは圧縮の外 (標準どおり)、W3C ログは例外ハンドラーの外 (未処理例外の 500 を記録する)、HTTP ログは圧縮の内 (ダンプが展開後) に置く
- `UseWhen` 内の `UseExceptionHandler("/error")` の再実行は暗黙のルーティングに乗らないので、`UseRouting()` を明示する
- アプリの部品 (Core の Service、パスワード、`TerminalAccess`、帳票、設定) は `ConfigureComponents` に System → Data → Service → Report → Setting の順で区切りコメントを付けて Singleton で登録する
- フレームワークの機能に付く登録と回線ごとの状態 (Scoped) は、その機能の `ConfigureXxx` に書く (Blazor の認証状態は `ConfigureBlazor` で `AddRazorComponents` の後)
- 設定は `Settings/XxxSetting` (DataAnnotations で制約) を `AddOptions<T>().BindConfiguration().ValidateDataAnnotations().ValidateOnStart()` で登録し、値を Singleton で再登録する。業務コードに `IOptions<T>` を渡さない
- 構成の段階で要る設定 (ログ、認証) は、登録の前に `GetSection().Get<T>()` で読む
- ログは `Application/Log.cs` の `[LoggerMessage]` に集約する (Info~、Warn~、Error~ の命名、`key=[{value}]` の書式)。文字列補間でログを書かない
- 計測は BCL の `Meter` と `ActivitySource` (`Application/Telemetry/ApplicationInstrument`) で行い、エクスポータは `ConfigureTelemetry` にだけ書く
- 要求ごとの文脈 (接続元アドレス) は `IHttpContextAccessor` からログ出力時に読み、`CallbackEnricher` で全ログ行に付ける (専用のミドルウェアは置かない)
- 変換は使うクラスの中に `[Mapper] private static partial` で書く。他のクラスも使う変換は元のクラス (`XxxEndpoints`、`XxxForm`) で `internal` / `public` にし、変換だけのクラスは作らない

### 認証と認可

- 管理画面はログイン (Cookie)、端末はペアリングで受け取ったトークン (Bearer、`TerminalAuthenticationHandler`) で認証する
- 端末のトークンは要求ごとに DB で照合する (登録の解除がすぐ効く)
- ポリシーは `ConfigureAuthentication` の `BuildPolicy` で作る (認証を無効にすると素通しになる)
- ポリシーを重ねるとスキームが合算されるので、管理画面と端末はスキームではなく要件 (アカウントの役割、端末のクレーム) で区別する
- API は既定の `Policies.Api` (ログインか端末) を基本にし、用途が管理だけの API (シフト一覧や商品別売上などの参照、CSV、PDF、日次締め、仕入先、入荷と移動の登録など) は `Policies.Admin`、マスタ・会社設定・端末登録の書き込みと締めの解除は `Policies.Administrator`、端末自身の通信は `Policies.Terminal` を付ける
- 資源のすべての API が管理だけのとき (日次締め、発注、仕入先) は、グループに `RequireAuthorization(Policies.Admin)` を付ける
- 端末も呼ぶ書き込みの API は `TerminalAccess` と `ClaimsPrincipal` を受け、本文や対象の店舗・端末がトークンと合わなければ `ApiProblems.TerminalMismatch()` を返す (既存の資源は読んでから確かめ、ないときは Service の NotFound に任せる)
- 読み取りと管理画面の要求には端末の照合を適用しない (端末を指定して読む現在のシフト `shifts/current` は照合する)
- 利用者と端末は `AuthClaims.AccountOf` / `TerminalOf` で読み、クレームを直接読まない。認証を無効にすると null になるので、記録する名前などは null を許す
- 匿名で受ける認証の入口 (ログイン、ペアリング) は `AllowAnonymous()` と `RequireRateLimiting(RateLimits.Auth)` を対にする
- ログインは静的 SSR のフォーム、ログアウトは `MainLayout` のフォーム (どちらも `<AntiforgeryToken />` 付き) から `AuthEndpoints` (`/auth`。API ではない) に POST し、結果は転送で返す

### エンドポイント

- API は `XxxEndpoints` (static。変換を持つときは partial) に書き、経路は `ApiRoutes` の定数にし、`MapXxxEndpoints` を `MapEndpoints` に足す
- 区切りは `// Mapping`、`// Mapper`、`// Handler` にする。ハンドラが多いときは `// Handler` の代わりに機能ごとの帯 (`// Create`、`// Image` など) にしてよい
- API のグループは `MapApiGroup` で作る (計測と既定の認可 `Policies.Api` が付き、401 と 500 の応答を宣言する)
- API の経路は `/api` の下に置く (例外処理、HTTP ログ、未認証の応答、流量制限の拒否が `/api` かどうかで分かれる)
- エンドポイントには `WithName` で操作名を付け、通信データと同じ機能名 + 操作名 (`XxxEndpoints` の `Xxx` + `HandleXxxAsync` の `Xxx`。`CategoryList`) にする
- エンドポイントは返す応答だけをすべて宣言する (本文は `Produces<T>(状態, 種類)`、本文なしは `Produces(状態)`、Problem Details は `ProducesProblem(状態)`)。401 と 500 はグループが宣言する
- 同じ状態は 1 回だけ宣言する (OpenAPI の文書には最後の宣言だけが載る)
- 入力を検証するエンドポイント (DataAnnotations、`IValidatableObject`) は 400 を `ProducesValidationProblem()` で、`ApiProblems.BadRequest` の 400 は `ProducesProblem(400)` で宣言し、両方を返すとき (レポート) は `errors` を省ける `ProducesValidationProblem()` だけにする (引数の束縛と JSON の読み取りの失敗は個別に宣言しない)
- 403 は、ポリシーで拒むもの (エンドポイントとグループの `RequireAuthorization`) を本文なしで、`TerminalMismatch` を Problem Details で宣言する (両方で拒むときは Problem Details だけにする)
- 流量制限する入口は 429 も宣言する
- ハンドラは要求の確認 (`TerminalAccess`、`RequestHelper`) → Request の変換 (`[Mapper]`) → Service → Response の変換だけを持つ (複数の Service の合成は Service に置く)
- 静的なユーティリティ (`TryParse` など) はエンドポイントのクラスに書かず、`Helpers/` に置く
- Response への変換は、返す型の名前から機能名を除いた名前に `To` を付ける (`ToListResponseItem`、`ToCashEventListResponseItem`、`ToSummaryResponsePaymentMethod`)
- 同じ型を返す手書きの変換が中で呼ぶ `[Mapper]` は、その名前に `Core` を付ける (`ToListResponseItem` が呼ぶ `ToListResponseItemCore`)。同じ型への別の変換は用途の名前にする (`ToSyncResponse`)
- Response 以外への変換は、Entity は `ToEntity` の多重定義、Service の入力は `ToParameter`、明細付きの複合は `ToDetail`、CSV の行は `ToExportRow`、`XxxResult` から応答へは `ToResult` にする
- 更新の応答は `DataWriteResult<T>` の行から作る
- 失敗は `ApiProblems` で Problem Details にする (`errorCode` は `ErrorCode`)。`DataWriteStatus` は `FromStatus`、`RuleError` は `FromViolation`、取引の検証は `FromValidation` で写す
- 新しい失敗は `ErrorCode` と `ApiProblems` のメソッドを足し、ハンドラで `ErrorCode` から Problem Details を組まない (`ErrorCode` を足すときの確かめと文言は common.md)
- 業務ルール違反の文言は、API・管理画面とも `ApiRuleText` から引く。それ以外の Problem Details の title は `ApiProblems` の既定値かハンドラに書く
- 端末が `Id` を決めて送る登録は冪等にする (同じ `Id` の再送は 200 で既存、内容が違えば 409 `DUPLICATE_ID_MISMATCH`、新規は 201 で Location は `ApiRoutes` から)
- 一括の登録 (在庫の変動) は、件ごとの結果 (`Created`、`Duplicate`) を 200 で返す
- 一覧の API は `page` (0 始まり) と `size` (`[Range(1, ApiDefaults.MaxPageSize)]`、既定は `ApiDefaults.PageSize`) を受け、`ListResponse` の形 (`total`、`page`、`size`、`items`) で返す
- 件数の少ないマスタ (税率、値引、支払方法、理由、仕入先) の一覧はページングせずに全件を返し、部門の一覧は `size` の既定を `ApiDefaults.MaxPageSize` にする
- API の入力検証は DataAnnotations で統一する (FluentValidation は管理画面のフォームだけに使う)
- 大小比較 (`from` ≤ `to`) や「どちらか必須」は `[AsParameters]` のクエリ型か Request の `IValidatableObject` で行い、ハンドラの中の `if` にしない
- `[AsParameters]` のクエリ型の引数は `[FromQuery(Name = "camelCase の名前")]` で名前を付ける (付けないと OpenAPI の引数名がプロパティ名になる)
- 必須の文字列のクエリは `[Required]` を付ける (空の値は束縛を通ってハンドラに届く)
- 一覧の `sort` は文字列で受けて列挙型に解析し、不正な値は既定の列にする
- レポートの `sort` と `groupBy` の不正な値は、ハンドラで 400 (`ApiProblems.BadRequest`) にする
- 本文をそのまま受ける API (画像、CSV) は `RequestHelper.IsMediaType` と `ReadBodyAsync` で形式と上限を確かめ、415 と 413 を返す。取込の行は文字列で受け、Service が行ごとに検証する

## ツール

- `tools/Pos.Server.SampleData` は API (`Pos.Contract`) と計算 (`Pos.Domain`) だけを参照し、Core を参照しない
- ツールは JSON の日時の変換と `ProblemResponse` を自前で持ち、ログインは管理画面のフォームで行う
