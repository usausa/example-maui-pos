# ソリューション構成

プロジェクト・層・パッケージと初期データ。  
設計方針は [decisions.md](decisions.md)、起動方法は [getting-started.md](getting-started.md)。

- [1. 方針](#-1-方針)
- [2. プロジェクト構成](#-2-プロジェクト構成)
- [3. サーバ (`Pos.Server.*`)](#-3-サーバ-posserver)
- [4. 共有プロジェクト (`Pos.Domain` / `Pos.Contract`)](#-4-共有プロジェクト-posdomain--poscontract)
- [5. 端末 (`Pos.Terminal`)](#-5-端末-posterminal)
- [6. 初期データ](#-6-初期データ)

---

## 🧭 1. 方針

- SQL は Accessor だけが持ち、業務の手順はサーバは **Service**、端末は **Service / Usecase** にまとめる。  
  Endpoints / Blazor ページ / ViewModel は入力の検証と表示に徹し、Service を呼ぶだけにする ([D-27](decisions.md#d-27-サーバは-service-に手順を集めsql-は-accessor-に置く), [D-28](decisions.md#d-28-端末は-viewmodel-から-service-と-usecase-を呼ぶ))
- 通信データ (`XxxRequest` / `XxxResponse`) は `Pos.Contract` に置いてサーバと端末で共有する ([D-26](decisions.md#d-26-サーバ端末共有のプロジェクトに分ける))
- JSON は camelCase ([D-31](decisions.md#d-31-api-は-camelcase-の-json-と-problem-details-にする))
- 管理画面はログイン (Cookie)、端末はペアリングで受け取るトークン (Bearer)、スタッフは端末で照合する PIN ([D-35](decisions.md#d-35-認証は管理画面のログイン端末のトークンスタッフの-pin-にする))。  
  端末発の要求は本文の `storeId` / `terminalId` / `staffId` で識別し、サーバはトークンと一致することを確かめる ([D-36](decisions.md#d-36-認可は要件で分け端末は自店自端末に限る))

---

## 🧱 2. プロジェクト構成

1 つのリポジトリに端末とサーバを同居させ (モノレポ)、Visual Studio では**サーバと端末を別々のソリューションで開いて個別に動かせる**ようにする ([D-39](decisions.md#d-39-モノレポにしサーバと端末のソリューションを分ける))。

```
example-maui-pos/
├─ .editorconfig / .gitattributes / .gitignore / Directory.Build.props / Directory.Build.targets
├─ Analyzers.ruleset / CodeCoverage.runsettings / AGENTS.md (AI 向け: 全体の書き方・検証・進め方) / LICENSE / README.md
│                                    ↑ ルートに 1 セット (MAUI 用の NoWarn NU1608 を含む)
├─ .claude/rules/                    AI 向け: コードの書き方の規則 (common = 全体と共有プロジェクト、他は paths で対象を絞る)
├─ .claude/skills/                   AI 向け: 繰り返す手順とスクリプト (verify = 検証、readme-images = README の画像、emulator = エミュレータでの確認)
├─ docs/                             設計文書、未実装と今後の検討事項 (backlog.md)、README の画像 (images/)、CSV 取込の例 (samples/)
├─ shared/                           両ソリューションに含める共有プロジェクト
│  ├─ Pos.Domain/                    ドメインロジック: 列挙型、計算 (税・値引按分・ポイント・返品)、業務ルール検証 (net10.0)
│  ├─ Pos.Domain.Tests/              計算ロジックの単体テスト。依存関係の検証テスト (Pos.Domain が UI / DB / HTTP に依存しない) を含む
│  └─ Pos.Contract/                    通信データ: XxxRequest / XxxResponse (net10.0)
├─ server/                           サーバ
│  ├─ Pos.Server.slnx                サーバ + shared/ (Domain / Contract / Domain.Tests) + tests + tools
│  ├─ Pos.Server.sln.DotSettings
│  ├─ src/
│  │  ├─ Pos.Server.AppHost/         Aspire AppHost (Host を起動し、ダッシュボードでログ・メトリクス・トレースを見る)
│  │  ├─ Pos.Server.Core/            Accessors (SQL) / Models (Entity / Views / Parameters) / Services
│  │  └─ Pos.Server.Host/            Minimal API + Blazor (MudBlazor) ホスト
│  ├─ tests/
│  │  ├─ Pos.Server.UnitTests/       bUnit (NavMenu / StatusChip / Login)、フォーム変換と CSV 取込の読み取りの単体テスト
│  │  └─ Pos.Server.IntegrationTests/ WebApplicationFactory による API・Accessor・起動処理 (スキーマと初期データ)・変更の通知・計測のテスト (SQLite 一時ファイル)
│  └─ tools/
│     └─ Pos.Server.SampleData/      サンプル取引の生成ツール (API 経由、§6)
└─ terminal/                         端末
   ├─ Pos.Terminal.slnx              端末 + shared/ (Domain / Contract)
   ├─ Pos.Terminal.sln.DotSettings / Settings.XamlStyler
   └─ Pos.Terminal/                  MAUI (net10.0-android)
```

- `server/Pos.Server.slnx` と `terminal/Pos.Terminal.slnx` は互いを含まない。  
  どちらも `../shared/` のプロジェクトを含む (同じプロジェクトを 2 つのソリューションに入れる)
- ルートの `Directory.Build.props` / `Analyzers.ruleset` / `.editorconfig` は両側に効く (MSBuild は上位フォルダの props を拾う。ruleset は各 csproj の `CodeAnalysisRuleSet` で相対参照)
- 全体を 1 度にビルドしたい場合は `dotnet build server/Pos.Server.slnx && dotnet build terminal/Pos.Terminal.slnx`。  
  両方を含む統合ソリューションは作らない (VS で個別に動かすため)

依存関係:

```
Pos.Terminal ──────────┐
                       ├──> Pos.Contract ──> Pos.Domain
Pos.Server.Host ───────┤                      ▲
        │              │                      │
        └──> Pos.Server.Core ─────────────────┘
```

- `Pos.Domain` は何にも依存しない (`Usa.Smart.Core` 程度)。  
  `Pos.Contract` は `Pos.Domain` の列挙型を使う
- `Pos.Server.Core` はエンティティと Accessor を持ち、`Pos.Domain` の列挙型を使う。  
  `Pos.Contract` は参照しない (Request / Response ↔ エンティティの変換は Host の Endpoints が `[Mapper]` で持つ)
- `Pos.Terminal` は `Pos.Contract` を通信と Outbox の直列化にそのまま使う。  
  ローカル DB のエンティティは端末側 (`Models/Entity`) に持つ
- `Pos.Server.SampleData` は `Pos.Contract` / `Pos.Domain` だけを参照し、サーバとは API でつながる

主なパッケージは次のとおり (バージョンは各 csproj に固定する)。

| 対象 | パッケージ |
| --- | --- |
| 全体 (`Directory.Build.props`) | StyleCop.Analyzers、Usa.Smart.Analyzers.JapaneseComment |
| `Pos.Domain` | Usa.Smart.Core |
| `Pos.Server.Core` | Usa.Smart.Data.Accessor (+ Extensions.DependencyInjection)、BunnyTail.ServiceRegistration |
| `Pos.Server.Host` | MudBlazor、FluentValidation、Usa.Smart.Mapper、Microsoft.Data.Sqlite、MiniDataProfiler.*、OysterReport、CsvHelper、QRCoder、Microsoft.AspNetCore.OpenApi + NSwag.AspNetCore、Serilog.*、OpenTelemetry.* (計装と OTLP / Prometheus のエクスポータ)、Microsoft.Extensions.Hosting.WindowsServices / Systemd |
| `Pos.Server.AppHost` | Aspire.AppHost.Sdk (SDK) |
| `Pos.Terminal` | Usa.Smart.Navigation.*、Usa.Smart.Mvvm、Usa.Smart.Maui (+ Extensions)、BunnyTail.DependencyInjection、Components.Maui、CommunityToolkit.Maui、Syncfusion.Maui.Toolkit (SfEffectsView)、BarcodeScanning.Native.Maui、SkiaSharp (+ Views.Maui.Controls)、QRCoder、Microsoft.Data.Sqlite + Usa.Smart.Data.Accessor、Microsoft.Extensions.Http、System.Reactive |
| テスト | xunit.v3、Microsoft.Testing.Extensions.CodeCoverage、bunit (`Pos.Server.UnitTests`)、Microsoft.AspNetCore.Mvc.Testing (`Pos.Server.IntegrationTests`) |

- 脆弱性の警告が出る推移的な依存は直接ピンし、GHSA 番号を csproj のコメントに書く (`Microsoft.OpenApi`、`SQLitePCLRaw.bundle_e_sqlite3`)

---

## 💻 3. サーバ (`Pos.Server.*`)

### 3.1 `Pos.Server.Core`

```
Accessors/
  MasterAccessor.cs                  マスタ (設定・店舗・端末・スタッフ・部門・税率・値引・支払方法・調整理由・仕入先) の一覧 / 取得 / 登録 / 更新 / 論理削除 / 件数
  ProductAccessor.cs, CustomerAccessor.cs, TransactionAccessor.cs, ShiftAccessor.cs, DailyClosingAccessor.cs, OrderAccessor.cs, InventoryAccessor.cs,
  InventoryReceiptAccessor.cs, InventoryTransferAccessor.cs, PurchaseOrderAccessor.cs, ReportAccessor.cs
  AccountAccessor.cs                 管理画面のアカウント (一覧 / 取得 / ログイン ID で取得 / 登録 / 更新 / パスワード / 最終ログイン / 削除)
  TerminalTokenAccessor.cs           端末の登録 (ペアリングコードの登録と照合、トークンのハッシュの照合、失効、端末ごとの登録の状態)
  GenericAccessor.cs                 テーブルに紐付かない処理: PRAGMA、後から増えた列 (追加と初期値) と支払方法「前受金」の追加、SQL ファイル (スキーマ・初期データ) の実行 ([DirectSql])
  Sql/{Accessor}.{Method}.sql       2-way SQL (DDL は Host の Assets/Data/Schema.sql)
  SqlHelper.cs                       2-way SQL の /*# */ から呼ぶ SQL 断片だけ (集計の GROUP BY 式、商品別売上の並び順の列)。/*!helper */ で参照する
  SchemaHelper.cs                    後から増えた列の追加 (EnsureColumnAsync)
  DataProfile.cs                     [AccessorProfile]: 列挙型ごとの EnumTextConverter<T> と DateOnly / DateTime のコンバータ
Models/Entity/                       {Table 単数}Entity (Smart.Data.Accessor の [Key]。テーブル名はクラスの [Name("Stores")])
Models/Views/                        DB から読んだ結果 (XxxView。一覧・詳細の結合 (TransactionDetailView / OrderDetailView など)、集計 (SalesSummaryView / ShiftTotalsView など)、
                                     帳票 (ShiftReportView / DailySalesReportView / ReceiptReportView / OrderReportView / PurchaseOrderReportView)、同期 (SyncMasterDataView)、端末の登録 (TerminalRegistrationView / TerminalTokenView))
Models/Parameters/                   Service に渡す条件と入力 (XxxQueryParameter。並び順を選べる一覧は PagedParameter<TSort> を基底にする。InventoryChangeParameter / ShiftCloseParameter / OrderUpdateParameter / PurchaseOrderUpdateParameter、
                                     ProductImportLine: CSV の 1 行を文字列のまま)
Models/Enums/                        一覧の並び順 (StoreSort / TerminalSort / StaffSort / CategorySort / ProductSort / CustomerSort / InventoryLevelDetailSort /
                                     ShiftSort / DailyClosingSort / OrderSort / TransactionSort / InventoryReceiptSort / InventoryTransferSort / PurchaseOrderSort。列挙名 = 列名、先頭が既定)、SalesSummaryGroupBy / ProductSalesSort、
                                     ProductImportColumn / ProductImportProblem (CSV 取込の誤りの列と種類)、DataChangeKind (変更の通知の種類)、AccountRole (管理画面の役割)
Models/PagedResult.cs                一覧の結果 (Total / Page / Size / Items)
Models/InventoryReferenceType.cs     在庫変動の参照の種類 (Transaction / InventoryReceipt / InventoryTransfer)
Services/                            業務の手順 (サブジェクトごと): マスタの XxxService (Store / Terminal / Staff / Category / TaxRate / Discount / PaymentMethod /
                                     AdjustmentReason / Supplier / Settings)、ProductService (画像・CSV 取込を含む)、CustomerService、TransactionService (登録・取消・照会)、
                                     ShiftService (開設・精算・入出金・集計)、DailyClosingService (日次締め・解除・店舗 × 営業日の一覧)、
                                     OrderService (受注の登録・変更・入荷・キャンセル、前受金の受取・返金)、InventoryService、
                                     InventoryReceiptService (入荷予定の登録・受領・キャンセル)、PurchaseOrderService (発注の登録・変更・発注で入荷予定を作る・キャンセル)、InventoryTransferService (店舗間移動の依頼・出荷・受領・キャンセル)、ReportService、
                                     SyncService、DatabaseService (スキーマ作成・初期データ)、
                                     ChangeNotificationService (書き込みの後のプロセス内の通知。管理画面のダッシュボードが購読する)、
                                     AccountService (ログイン・セッションの版の確認・アカウントの管理)、TerminalTokenService (ペアリングコードの発行・ペアリング・登録の解除・トークンの照合)
  DataWriteStatus.cs                 書き込みの結果 (Success / NotFound / Duplicate / VersionMismatch / InUse / Invalid)
  DataWriteResult.cs                 更新の結果 (Status + 更新後の行)
  RuleViolationException.cs          DB トランザクション内の業務ルール違反
  ServiceHelper.cs                   重複 (IDialect.IsDuplicate) と楽観ロック (更新後の行が返らない) の判定、LIKE のエスケープ
Infrastructure/Data/                 EnumTextConverter<T> (列挙型 ↔ TEXT)、DateOnlyTextConverter、DateTimeTextConverter (UTC)
Infrastructure/Json/                 JsonDateTimeConverter (yyyy-MM-ddTHH:mm:ss.fffZ。Host の JSON 設定で使う)
Infrastructure/Imaging/              ImageContentType (画像の形式を先頭のバイトで判定する)
Infrastructure/Security/             IPasswordProvider / DefaultPasswordProvider (PBKDF2 のソルト + ハッシュ)
ServiceCollectionExtensions.cs      AddCoreServices (BunnyTail.ServiceRegistration で Services/ の XxxService を Singleton 登録)
```

- SQL は Accessor だけが持つ。  
  Service が Accessor を束ね、複数テーブルにまたがる書き込み (取引登録・取消・精算・日次締め・受注・発注・入荷・移動など) は Service の中で `IDbProvider.UsingTxAsync` を使う ([db-design.md §5](db-design.md#-5-整合性と更新の単位))
- 業務ルールは `Pos.Domain`、LIKE のエスケープ・既定値・現在時刻 (`TimeProvider`) は Service が扱う
- 重複 (`IDialect.IsDuplicate`)、楽観ロック (`UPDATE ... RETURNING *` で更新後の行が返らない)、使用中 (件数クエリ) の判定は Service の中で行い、`DataWriteStatus` / `DataWriteResult<T>` で返す (API と管理画面で同じ規則)
- Accessor の DI 登録は Host の `AddDataAccessors(typeof(DataProfile).Assembly)`、Service は `AddCoreServices()`

### 3.2 `Pos.Server.Host`

```
Application/                         アプリ固有の部品
  ApplicationExtensions.cs           起動構成 (camelCase JSON、Problem Details、認証・認可・試行回数の制限、Serilog、ヘルスチェック、MudBlazor、OpenAPI (開発時 /swagger, /redoc。端末のトークンの Bearer))
  Authentication/                    Policies (Api / Admin / Administrator / Terminal)、AuthClaims (クレームの組み立てと読み取り)、TerminalAuthenticationHandler (Bearer を DB で照合)、
                                     TerminalAccess (端末のトークンと本文の店舗・端末の一致)、AccountAuthenticationStateProvider (開いている回線でアカウントの版を 1 分ごとに確かめる)
  ViewHelper.cs                      画面の部品の文言と色 (チップ・マーク・見出し)
  ViewExtensions.cs                  表示用の書式 (金額・数量・日時・列挙型の日本語名) の拡張メソッド
  SnackbarExtensions.cs, Styles.cs, Log.cs ([LoggerMessage] の集約), NamingPolicy.cs
  Telemetry/                         ApplicationInstrument (Meter / ActivitySource: 稼働時間、API の要求数と長時間実行)、Source、TelemetryExtensions
  Lookup/NameLookup.cs               ID → 名称 (店舗・端末・スタッフ・支払方法)
  State/StoreFilterState.cs          一覧ページ間で共有する店舗の絞り込み (scoped)
  Urls/ExportUrls.cs                 管理画面から開くダウンロード URL (CSV / PDF)
Reports/                             OysterReport の帳票: ShiftReportBuilder (精算レポート)、DailySalesReportBuilder (売上日報)、ReceiptReportBuilder (レシートの控え)、PurchaseOrderReportBuilder (発注書)、OrderReportBuilder (受注票)、ReportText (D-21)
Endpoints/                           静的クラス + MapApiGroup (計測フィルタ付きのグループ。ハンドラは private static)。Request → Entity / Parameter の変換 ([Mapper]) と Service の呼び出しだけを担う
  EndpointExtensions.cs (MapApiGroup), ApiRoutes.cs (/api/v1), ApiDefaults.cs (ページサイズ), ApiProblems.cs (errorCode / errors / expected 付き Problem Details と DataWriteStatus からの変換),
  ApiRuleText.cs (業務ルール違反と警告の文言)
  AuthEndpoints (管理画面のログイン・ログアウトのフォーム。API ではない)
  SettingsEndpoints, StoreEndpoints, TerminalEndpoints (+ pair, me/heartbeat), StaffEndpoints, CategoryEndpoints, TaxRateEndpoints,
  ProductEndpoints, DiscountEndpoints, PaymentMethodEndpoints, SyncEndpoints, CustomerEndpoints,
  TransactionEndpoints, ShiftEndpoints, DailyClosingEndpoints, OrderEndpoints, InventoryEndpoints, AdjustmentReasonEndpoints (/inventory/adjustment-reasons),
  SupplierEndpoints (/inventory/suppliers), PurchaseOrderEndpoints (/inventory/purchase-orders), InventoryReceiptEndpoints (/inventory/receipts), InventoryTransferEndpoints (/inventory/transfers), ReportEndpoints
Helpers/EnumHelper.cs                クエリ文字列や並び順ラベルの列挙値 (大文字小文字を区別せず、数値や未定義の値は受け付けない)
Helpers/RequestHelper.cs             本文をそのまま受ける API (商品画像・CSV 取込) の Content-Type の確認と、上限付きの読み取り
Infrastructure/                      アプリに依存しない部品: Csv (CsvExport、CsvImport: UTF-8 / Shift_JIS の判別と見出しでの読み取り)、Logging (ErrorBoundaryLogger、CallbackEnricher: IHttpContextAccessor から読んだ接続元アドレスを全ログ行に付ける)、
                                     ExceptionHandling (GlobalExceptionHandler)、Filters (RequestMetricsEndpointFilter: API の要求数と長時間実行の警告)、Reports (EmbeddedFontResolver: 同梱 IPAex ゴシック)
Models/Forms/                        管理画面のフォーム + FluentValidation (FormValidator<T> を基底に XxxForm / XxxFormValidator。マスタ 10 種 + Customer / StaffPin / InventoryChange / PointAdjust / Order /
                                     PurchaseOrder / InventoryReceipt / InventoryTransfer / InventoryMovement (出荷・受領の確認) / Account / AccountPassword)。
                                     Entity ↔ Form の変換 ([Mapper]。Guid? / DateOnly の変換は [MapUsing]) はフォームが持つ。
                                     1 つのページだけで使うフォーム (SettingsForm) はそのページの内部クラス。文字列の長さは Pos.Domain.Length の定数
Models/Queries/                      API のクエリ ([AsParameters]): ReportPeriodQuery (店舗と期間。from ≤ to は IValidatableObject)
Models/Export/                       CSV の行 (ProductExportRow, SalesSummaryExportRow, ProductSalesExportRow。CsvHelper の [Name] で日本語見出し)、ProductCsvHeader (商品 CSV の見出し)
Models/Import/                       取込の行 (ProductImportRow: 見出しは出力と同じで値は文字列のまま。Service の ProductImportLine に写す)
Components/
  App.razor, Routes.razor, _Imports.razor
  AppComponentBase.cs                Disposable をまとめて破棄するコンポーネントの基底
  PageComponentBase.cs               ページの基底 (読み込み / 実行 / エラー / 確認 / 編集ダイアログ、DataWriteStatus / DataWriteResult の通知)
  Layout/ (MainLayout, NavMenu (MudNavGroup。現在の URL のグループを開く), EmptyLayout, ReconnectModal)
  Pages/  Home (S-01), SalesSummaryPage (S-10), ProductSalesPage (S-11), TransactionsPage (S-20), OrdersPage (S-91), ShiftsPage (S-30), DailyClosingsPage (S-90), InventoryPage (S-40),
          InventoryChangesPage (S-42), PurchaseOrdersPage (S-48), AdjustmentReasonsPage (S-44), InventoryReceiptsPage (S-45), InventoryTransfersPage (S-46), SuppliersPage (S-47),
          ProductsPage (S-50), CategoriesPage (S-53), TaxRatesPage (S-54), DiscountsPage (S-55),
          PaymentMethodsPage (S-56), CustomersPage (S-60), CustomerDetailPage (S-61), StoresPage (S-70), TerminalsPage (S-71), StaffPage (S-72), SettingsPage (S-80),
          AccountsPage (S-92), Login (S-02), AccessDenied, Error, NotFound。ページは .razor + .razor.cs。_Imports.razor で全ページに [Authorize] (ログイン・エラー・404 は AllowAnonymous)
  Controls/ (ErrorBanner, ProgressOverlay, StoreSelect (店舗セレクタ), StatusChip (ViewHelper の文言 + 色))
  Dialogs/ (EditDialogBase<TForm>, DialogServiceExtensions (情報・確認), AppMessageBox, XxxEditDialog (マスタ 10 種 + Customer), TransactionDetailDialog (S-21), ShiftDetailDialog (S-31), DailyClosingDialog (S-90),
            OrderDialog (S-91), OrderEditDialog, ProductImportDialog (S-52), ProductInventoryDialog (S-41), InventoryChangeDialog (S-43), PurchaseOrderDialog (S-48), PurchaseOrderEditDialog, InventoryReceiptDialog (S-45), InventoryReceiptEditDialog,
            InventoryTransferDialog (S-46), InventoryTransferEditDialog, InventoryMovementDialog (出荷・受領の確認), PointAdjustDialog (S-62), TerminalPairingDialog (S-71),
            StaffPinDialog (S-72), AccountEditDialog / AccountPasswordDialog (S-92))
  RedirectToLogin.razor              ログインしていないときにログイン画面へ (戻り先を付ける)
Assets/                              Fonts/ipaexg.ttf、Reports/*.xlsx (帳票テンプレート)、Data/Schema.sql (DDL) と Data/InitialData.sql (初期データ。起動時に読んで実行する)。どれも出力ディレクトリへコピーする
Settings/                            LogSetting (HTTP ログ・本文ダンプ・W3C アクセスログ) / ProfilerSetting (SQL のログとトレース) / TelemetrySetting (長時間実行のしきい値) /
                                     AuthSetting (認証の有効、Cookie の期限、試行回数、初期の管理者)
wwwroot/                             css/app.css, js/reconnect.js
```

- エンドポイントは「Request を Entity / Parameter に写す → Service を呼ぶ → 結果 (`DataWriteStatus` / `TransactionResult` など) を Response か Problem Details に写す」だけを担う。  
  Response への変換は Smart.Mapper の `[Mapper]` をエンドポイントの静的部分メソッドで持つ。  
  取引登録 (`POST /transactions`) の流れは [db-design.md §5.1](db-design.md#51-取引登録-post-transactions-は-1-つの-db-トランザクション)
- Blazor ページも同じ Service を `[Inject]` して使う。  
  Razor の表示用の加工は `ViewHelper` / `ViewExtensions` に集約し、Accessor / `IDbProvider` はページから使わない
- `InitializeApplicationAsync` で `DatabaseService.InitializeAsync` (スキーマ作成、後から増えた列の追加、初期データ) を行い、アカウントがなければ初期の管理者を作る
- 認証は管理画面が Cookie、端末が `Terminal` スキーム (Bearer を要求ごとに DB で照合)。  
  API のグループは既定で `Api` ポリシー、管理だけの API は `Admin`、マスタ・会社設定の書き込みは `Administrator` を重ねる。  
  ポリシーを重ねるとスキームが合算されるので、管理画面と端末は要件 (役割・端末のクレーム) で分ける ([D-36](decisions.md#d-36-認可は要件で分け端末は自店自端末に限る))。  
  端末の一致は各ハンドラで `TerminalAccess` を使って確かめる。  
  管理者だけの操作は `AuthorizeView` (`Administrator`) で Operator に出さない
- テレメトリは OpenTelemetry。  
  `OTEL_EXPORTER_OTLP_ENDPOINT` があるとき (Aspire から起動したときなど) だけログ・メトリクス・トレースを OTLP で送り、`Prometheus:Uri` が設定されていればメトリクスを HTTP で公開する (既定 9464)。  
  SQL のトレース (`Profiler:SqlTelemetry`) と API の要求数・長時間実行 (`Telemetry:LongExecutionThreshold`) も同じ経路
- 一覧の `sort` / レポートの `groupBy` は文字列で受け取り `EnumHelper` で列挙型にする (一覧の不正な値は既定、`groupBy` / レポートの `sort` は 400)。  
  レポートの期間 (`ReportPeriodQuery`) の `from > to` は `IValidatableObject` で 400 にする (API の入力検証は DataAnnotations、FluentValidation は管理画面のフォームだけ)
- 描画モードは対話型 (プリレンダリングなし)。  
  ログイン・エラー・404 のページ (`[ExcludeFromInteractiveRouting]`) は静的 SSR にする (ログインはフォームの POST、エラーと 404 は例外や 404 の再実行で描画され、常に対話型にすると回線のない再実行で空の HTML になる)

---

## 🧩 4. 共有プロジェクト (`Pos.Domain` / `Pos.Contract`)

ドメインロジックの共有と通信データの共有は別の概念なので、プロジェクトを分ける ([D-26](decisions.md#d-26-サーバ端末共有のプロジェクトに分ける))。

### 4.1 `Pos.Domain`

```
Length.cs             文字列の長さと入力の桁数 (DB の列長・Request の検証・管理画面のフォーム・端末の電卓で共用)
Enums/                列挙型を 1 型 1 ファイルで: TransactionType, TransactionStatus, ProductKind, PaymentKind, DiscountType, DiscountScope,
                      TaxKind, StaffRole, ShiftStatus, DailyClosingStatus, OrderType, OrderStatus, OrderDepositType, CashEventType, InventoryChangeType, InventoryReceiptStatus,
                      InventoryTransferStatus, PurchaseOrderStatus, PointHistoryType, ImportAction (取込の行の結果),
                      TaxRounding, PointBasis、
                      ErrorCode / WarningCode (+ ErrorCodeExtensions.ToCode: UPPER_SNAKE_CASE)、RuleReason (違反の理由)
Logic/
  SalesLogic.cs       SalesInput (明細・値引・支払・会社設定) → SalesResult (計算項目)。SalesLogic.Comparison.cs は端末が送った計算項目と再計算の一致判定
  SalesInput.cs / SalesResult.cs  計算の入出力 (record。Request / Response とは別の純粋な型)
  ReturnLogic.cs      ReturnInput (元取引の明細 + 返品明細) → SalesResult
  ReturnInput.cs      返品の入力 (ReturnOriginalLine = 元明細の事実)
  TaxLogic.cs         税グループ集計と明細への按分税額 (販売・返品で共用)
  AllocationLogic.cs  最大剰余法の按分、RoundingLogic.cs TaxRounding の丸め
  TransactionLogic.cs ValidateInput (入力だけの検証) / ValidateSale / ValidateReturn / ValidateVoid → TransactionValidation (Errors / Warnings / Expected)
  TransactionValidation.cs  検証結果 (RuleError = ErrorCode + RuleReason、RuleWarning) と、検証に必要な事実 (SaleContext / ReturnContext / VoidContext、ShiftFact / ProductFact / OrderFact ...)
  OrderLogic.cs       受注の状態遷移 (最初の状態、変更・入荷・キャンセル・会計ができるか)、明細の金額、前受金 (受取・返金ができるか、会計で充てる額)
  InventoryMovementLogic.cs  入荷・店舗間移動の状態遷移 (受領・出荷・キャンセルができるか)
  PurchaseOrderLogic.cs  発注の状態遷移 (変更・発注は下書きだけ、キャンセルは入荷の前だけ)
  StaffLogic.cs       担当と承認者 (有効で自店か本部の所属、承認は店長以上、レジ係の取消は承認が要る)
  PinHasher.cs        スタッフの PIN のハッシュ (PBKDF2 + ソルト。サーバで作り、端末は同期した値で照合する)
```

- 純粋関数 (入力を変更しない) にし、`Pos.Domain.Tests` で [api-design.md §4.6](api-design.md#46-計算例) を含むケースを固定する
- `SalesLogic` の入出力は `Pos.Contract` の Request / Response に依存しない。  
  変換は呼び出し側 (端末のカート、サーバの Service) が行う
- `TransactionLogic` は DB を見ない。  
  シフト・商品・元取引などの事実は呼び出し側が Context に詰めて渡す。  
  検証できる入力なら再計算結果を `Expected` に返すので、サーバはそれを `CALCULATION_MISMATCH` の `expected` と応答の計算項目に使う
- 違反は `RuleReason` (理由) で返し、利用者向けの文言はサーバ (`Endpoints/ApiRuleText`) と端末 (`Modules/Helpers/ViewHelper`) がそれぞれ持つ
- `Pos.Domain.Tests` の `DependencyTests` が「UI / DB / HTTP / `Pos.Contract` を参照していない」ことを検証する

### 4.2 `Pos.Contract`

```
ListResponse.cs  一覧の共通形 (Total / Page / Size / Items)。一覧は XxxResponse、その要素は XxxResponseItem ([D-31](decisions.md#d-31-api-は-camelcase-の-json-と-problem-details-にする))
Settings/      SettingsResponse / SettingsUpdateRequest
Stores/ Terminals/ Staff/ Categories/ TaxRates/ Products/ Discounts/ PaymentMethods/
               XxxResponse (一覧) / XxxResponseItem / XxxCreateRequest / XxxUpdateRequest
               Terminals は TerminalPairRequest / TerminalPairResponse / TerminalHeartbeatRequest も、Products は ProductImportResponse (+ Item) も持つ
Sync/          SyncMastersResponse
Customers/     CustomerResponse / CustomerResponseItem / CustomerCreateRequest / CustomerUpdateRequest,
               CustomerPointHistoryResponse / CustomerPointHistoryResponseItem, CustomerPointAdjustRequest
Orders/        OrderCreateRequest (+ Line), OrderUpdateRequest (+ Line), OrderCancelRequest, OrderDepositRequest, OrderDepositRefundRequest,
               OrderResponse / OrderResponseItem (+ Line / Deposit)
Transactions/  TransactionCreateRequest (+ TransactionCreateRequestLine / Discount / TaxSummary / Payment / Delivery / Void),
               TransactionResponse / TransactionResponseItem (+ TransactionResponseLine / ... / Warning),
               TransactionVoidRequest, TransactionCalculateRequest, TransactionCalculateResponse (計算項目のみ。calculate の応答と expected)
Shifts/        ShiftOpenRequest, ShiftResponse / ShiftResponseItem (+ Denomination / Totals), ShiftCloseRequest (+ Denomination),
               ShiftCashEventRequest / ShiftCashEventResponse / ShiftCashEventResponseItem, ShiftSummaryResponse (+ PaymentMethod / TaxRate / Category / Points / Cash)
DailyClosings/ DailyClosingCreateRequest, DailyClosingResponse / DailyClosingResponseItem, DailyClosingSummaryResponse (+ PaymentMethod / TaxRate / Shift)
Inventory/     InventoryLevelResponse / InventoryLevelResponseItem, InventoryProductResponse (+ Level),
               InventoryChangeRequest (+ Change) / InventoryChangeResultResponse (+ Result、InventoryChangeResultStatus),
               InventoryChangeResponse / InventoryChangeResponseItem
AdjustmentReasons/ AdjustmentReasonResponse / AdjustmentReasonResponseItem / AdjustmentReasonCreateRequest / AdjustmentReasonUpdateRequest
Suppliers/     SupplierResponse / SupplierResponseItem / SupplierCreateRequest / SupplierUpdateRequest
InventoryReceipts/  InventoryReceiptCreateRequest (+ Line), InventoryReceiptReceiveRequest (+ Line), InventoryReceiptResponse / InventoryReceiptResponseItem (+ Line)
InventoryTransfers/ InventoryTransferCreateRequest (+ Line), InventoryTransferShipRequest, InventoryTransferReceiveRequest (+ Line),
               InventoryTransferResponse / InventoryTransferResponseItem (+ Line)
PurchaseOrders/ PurchaseOrderCreateRequest (+ Line), PurchaseOrderUpdateRequest (+ Line), PurchaseOrderResponse / PurchaseOrderResponseItem (+ Line)
Reports/       ReportSalesSummaryResponse (+ Row), ReportProductSalesResponse (+ Row)
```

- 名前空間はフォルダごと (`Pos.Contract.Transactions` など)。  
  書き方は `sealed class` と `{ get; set; }` (null にならない参照型は `= default!`) で、Request の検証は `Required` / `MaxLength` / `Range` (文字列の長さは `Pos.Domain.Length` の定数)。  
  camelCase への変換はシリアライザ設定で行い、属性は付けない
- クラス名はエンドポイントのクラス名 + メソッド名 (`TransactionCreateRequest` / `TransactionCalculateResponse` / `CustomerPointHistoryResponse` / `ShiftCashEventRequest` / `ReportSalesSummaryResponse`)。  
  入れ子の要素は親の名前に要素名を続ける (`TransactionResponseLine`)
- 列挙型は `Pos.Domain` のものをそのまま使う。  
  エラーコード定数は持たず、`Pos.Domain` の `ErrorCode.ToCode()` / `WarningCode.ToCode()` と端末の `ProblemResponse.ErrorCode` (文字列) で突き合わせる
- 日付は `DateOnly`、日時は `DateTime` (UTC)。  
  `JsonDateTimeConverter` (`yyyy-MM-ddTHH:mm:ss.fffZ`) と Problem Details の型 (`ProblemResponse`) は契約ではないので使う側がそれぞれ持つ (サーバは `Pos.Server.Core` の `Infrastructure/Json`、端末は `Helpers/Json` / `Services`、`Pos.Server.SampleData` と `Pos.Server.IntegrationTests` も持つ)。  
  サーバは `ConfigureHttpJsonOptions`、端末は `HttpService.JsonOptions` で同じ設定を登録し、形式は `Pos.Server.IntegrationTests` の `JsonContractTests` で固定
- `ImageUrl` の CA1056 と、公開型を internal にする CA1515 は `Pos.Contract` の `GlobalSuppressions.cs` で抑止している ([D-41](decisions.md#d-41-警告は-0-にし抑止は理由のあるものだけにする))

---

## 📱 5. 端末 (`Pos.Terminal`)

`net10.0-android`。

```
MauiProgram.cs                       BunnyTail DI、Navigator (HierarchyEffectPlugin で Forward / Back のスライド (D-23)、NavigationFeedbackPlugin)、Dialog / Popup、フォントは MaterialIcons のみ
                                     + BarcodeScanning、HttpClient (IHttpClientFactory)、IDbProvider (SQLite)、DataAccessor、Service / Usecase、State
MainPage.xaml / MainPageViewModel    シェル (タイトル + 店舗-端末 担当 + 未送信バッジ + F1〜F4)。起動時に Setup (未登録) または StaffSelect へ。要求が 401 になったら知らせて Setup へ。  
                                     根の画面 (AppViewModelBase.HandlesBack = false) の戻るはプラットフォームに任せる (MainActivity がタスクを背面へ回す)
App.xaml.cs                          起動時に DatabaseService でローカル DB を作り、CredentialService でトークンを読み、SyncService でセッションを復元して同期を始める
Extensions.cs                        拡張メソッド (リソース、IDialog の日本語ボタン、PostForwardAsync / PostActionAsync、TrimToNull、ObservableCollection.Replace)
Shell/ ShellProperty (+ Active: 表示中の View だけがシェルを更新) / ShellEvent / ShellUpdateBehavior / IShellControl
Behaviors/                           Entry / Label / Scroll などの動作、EntryBind (EntryController)、BarcodeBind (CameraView)
Messaging/                           BarcodeController、EntryController
Controls/                            SectionPanel (見出しと白い面の節)、StatusChip (状態を色と短い文言で示すチップ)
Components/                          StorageManager (アプリのフォルダ。ローカル DB の置き場所)
Extender/                            NavigationFeedbackPlugin (遷移先の画面にボタンの押下表示を残さない。Android)
Log.cs                               [LoggerMessage] の集約
Converters/                          DisplayNameConverter (列挙型 → 文言)、EmptyTextConverter、QrImageSourceConverter (QRCoder)、YenConverter、StockSendTextConverter、AvatarColorConverter / InitialConverter (会員のアバターの色と頭文字)。  
                                     色や選択マーク・画面固有の文言は Smart.Maui の BoolToColor / MapToColor / BoolToText を Styles.xaml で構成する
Helpers/                             アプリに依存しない処理だけ: DateTimeHelper (日付書式の集約)、SettingParser (Key=Value)、CrashReport、ElementHelper、
                                     Data/ (EnumTextConverter<T> / DateOnlyTextConverter / DateTimeTicksConverter / SchemaHelper / SqlHelper: LIKE のエスケープ)、Json/JsonDateTimeConverter
Permissions.cs                       カメラ権限
Modules/
  ViewId.cs, DialogId.cs, Parameters.cs (遷移パラメータ: スキャンのモードと結果 / 戻り先 / 取引・シフト・商品・受注の ID / 会員), AppViewModelBase.cs, AppDialogViewModelBase.cs
  PopupNavigatorExtensions.cs        入力の種類ごとの電卓 (電話番号 / 郵便番号 / 生年月日 / 会員番号 / 商品コード / レシート番号 / 伝票番号 / ペアリングコード / PIN (伏せ字) / 数量 / 金額 / 実査金額 / ポイント / 枚数 / 在庫 / 値引。桁数は Pos.Domain.Length) と一覧からの選択 (ChooseAsync)
  Helpers/ViewHelper.cs              金額・数量・日時・列挙型・業務ルールの文言 (XAML からは DisplayNameConverter で使う)
  Setup/      SetupView (T-00), StaffSelectView (T-01)
  Main/       MenuView (T-02)
  Shift/      ShiftOpenView (T-03), CashEventView (T-50), ShiftCloseView (T-51), ShiftReportView (T-52), DenominationsView (金種別入力ポップアップ)
  Sales/      SalesContext (カートと支払)、SalesView (T-10), ScanView (T-11), ProductSearchView (T-12), CustomerSelectView (T-14),
              DeliveryView (T-16), HoldView (T-17), PaymentView (T-20), CompleteView (T-21), ReceiptView (T-22),
              LineEditView (P-13), DiscountView (P-15。取引値引と明細値引の両方)
  Returns/    ReturnContext (元取引・返品明細・理由)、ReturnView (T-40), ReturnLinesView (T-41), RefundView (T-42)
  History/    TransactionListView (T-30), TransactionDetailView (T-31)
  Orders/     OrderListView (T-92), OrderDetailView (T-93), OrderCreateView (T-94)
  Inquiry/    ProductInquiryView (T-60), CustomerInquiryView (T-61), CustomerEditView (T-62)、CustomerDraft (スキャン中の入力内容)
  Inventory/  StockContext (入力リスト)、StockCountView (T-70)、ReceivingContext (検品中の伝票と数えた数)、ReceivingListView (T-71), ReceivingCheckView (T-72)
  Report/     SalesReportView (T-80)
  Setting/    SettingView (T-90)
  Dialogs/    InputNumberView (電卓)、ReasonSelectView (理由の選択)、SelectView (一覧からの選択)、ConfirmView / MessageView (確認と情報)、SheetDialog (IDialog の確認と情報をシートで出す)
Models/
  Cart/       SalesCart, CartLine, CartDiscount, CartPayment, CartDelivery
  Entity/     ローカル DB のエンティティ (LocalTransaction / LocalShift / LocalCashEvent / LocalOrderDeposit / Outbox / SyncState / HoldCart。マスタは Pos.Contract の Response をそのまま使う)
  Input/      NumberInputParameter, NumberInputModel
  SummaryRow.cs (集計・詳細画面の行と節), StockChange.cs, SelectItem.cs, ReceivingDocument.cs (受領待ちの伝票と明細), ReceivingKind.cs, ReceivingLineState.cs
Services/                            単機能の部品
  DataAccessor.cs + Sql/            ローカル SQLite (Smart.Data.Accessor、2-way SQL。DDL は Resources/Raw/Schema.sql。ローカルのエンティティのキーによる取得・削除は [SelectSingle] / [Delete])、DataProfile (型変換)
  DatabaseService.cs                 ローカル DB の初期化 (PRAGMA、テーブル作成、後から増えた列の追加)
  HttpService.cs / ApiResult.cs / ApiContext.cs / ApiNames.cs / ProblemResponse.cs   HttpClient による API 呼び出し (Pos.Contract の Request / Response、失敗時は Problem Details、D-28)。  
                                     要求ごとに端末のトークンを Bearer で付け、401 は ApiContext が一度だけ知らせる
  CredentialService.cs               端末のトークン (SecureStorage) と登録日時。登録済みか、保存、解除
  PinService.cs                      PIN の照合 (3 回まで、背景スレッドで PBKDF2) と承認者の選択 (自店か本部の店長以上で PIN があるスタッフ)
  NetworkService.cs                  オンライン限定操作の接続確認・インジケータ・エラー通知
  SyncService.cs                     マスタ差分同期と Outbox 送信のバックグラウンド実行 (未登録の間は止める)、heartbeat (1 分ごと)、レシート番号の採番
  ReceiptService.cs                  レシート画像の組み立て (ReceiptTextBuilder: 等幅 32 桁、ReceiptImageBuilder: SkiaSharp で桁位置に描画)
  ProductImageService.cs             商品画像の取得と CacheDirectory へのキャッシュ (URL の v ごと。オフラインはキャッシュだけ)
  ShiftReportTextBuilder.cs          精算レポートの共有テキストの組み立て
Usecases/                            通信 → DB → 完了までの一連の手順
  TransactionUsecase.cs              取引の保存 (ローカル取引 + Outbox + 自店在庫を 1 トランザクション)、取消、履歴 (送信状態付き)、端末にない取引はオンラインでサーバから
  SalesUsecase.cs / ReturnUsecase.cs 会計・返品の計算 (Pos.Domain) と確定、保留、元取引の検索 (オンラインならサーバの最新)
  ShiftUsecase.cs                    開設 (サーバに残ったシフトの引き継ぎ)、精算、入出金、集計
  OrderUsecase.cs                    受注の登録 (カートから)、受注の取得と会計前の読み直し、受注から会計のカートを作る (前受金を含む)、前受金の受取と返金 (今のシフトの記録を端末に写す)
  StockUsecase.cs / SetupUsecase.cs  棚卸・在庫調整の送信、初期設定 (ペアリング・トークンと設定の保存・初回同期)
  ReceivingUsecase.cs                受領待ちの入荷・移動の取得と受領 (オンライン。受領したら在庫の差分同期を促す)
  ReceivingMapper.cs                 入荷・移動の応答 → 受領待ちの伝票、数えた数 → 受領の要求
  TransactionMapper.cs               Cart → Pos.Domain の計算入力 → TransactionCreateRequest の変換
  ShiftSummaryCalculator.cs          ローカルの取引・入出金・前受金からのシフト集計
State/
  DeviceState.cs / StartupState.cs
  Settings.cs                        ApiEndPoint / StoreId / TerminalId / PairedAt / OpenSalesAfterLogin (IPreferences。トークンは CredentialService の SecureStorage)
  Session.cs                         使用者に紐付く状態: 会社設定、店舗、端末、選択中スタッフ、開設中シフト、未送信 / 要確認件数、営業日、CanTransact
Resources/
  Fonts/      MaterialIcons のみ
  Styles/     Colors.xaml、Styles.xaml (Converter の構成、POS 節: Pos 接頭辞のスタイル、ヘッダの状態表示 / 一覧行 / チップ / テンキー / 入力欄)
Platforms/Android/ MainActivity (pos.terminal.MainActivity)、AndroidHelper。CAMERA 権限
```

- ViewModel は入力の検証と表示に徹し、通信 → DB → 完了までの一連の手順は `Usecases/` の `XxxUsecase`、単機能は `Services/` の `XxxService` に置く ([D-28](decisions.md#d-28-端末は-viewmodel-から-service-と-usecase-を呼ぶ))。  
  ViewModel から `IDbProvider` は使わない
- 特定の機能の画面間でだけ共有する状態 (カート、返品、棚卸の入力、入荷・移動の検品、会員編集の下書き) は `State` ではなく `SalesContext` / `ReturnContext` / `StockContext` / `ReceivingContext` / `CustomerDraft` とし、ViewModel の `[Scope]` プロパティに Smart.Navigation の Scope プラグインが注入する (DI に transient 登録)。  
  同じ名前のプロパティを持つ画面の間で同じインスタンスが共有され、どの画面からも参照されなくなると破棄される (会計完了やメニューへ戻ると新しいカートになる)。  
  スキャンなど途中の画面は、呼び出し元の機能の状態を保持するために各コンテキストのプロパティを持つ
- 使用者に常に紐付く情報 (店舗・端末・担当・シフト) は `Session` に集約する
- 通信は HttpClient + `System.Text.Json` (`HttpService.JsonOptions`: camelCase / null 省略 / 列挙型は文字列 / `JsonDateTimeConverter`) ([D-28](decisions.md#d-28-端末は-viewmodel-から-service-と-usecase-を呼ぶ))
- 画面遷移は `Navigator.ForwardAsync` のみ (スタックは使わない)。  
  複数の画面から使う画面 (スキャン、会員選択、レシートなど) は `Parameters.WithReturnTo` で戻り先を受け取る
- 物理キーボードは前提にしない ([D-24](decisions.md#d-24-端末の入力は電卓と下端のシートにする))。  
  数値・番号は電卓 (`PopupNavigatorExtensions` の入力の種類ごとのメソッド)、理由は定型の選択 (`ReasonSelect`) で入力し、キーボードは会員・配送先の文字項目、検索、設定に限る
- 販売・会計画面は `Styles.xaml` の POS 節 (白い行 + 区切り線、名称は太字、金額は青、[D-23](decisions.md#d-23-端末はメニュー型のホームと-f1f4-のシェルにする)) を使う。  
  ポップアップは画面の下端に寄せたシート (CommunityToolkit の Popup。下段の ✕ / ✔ は F キーと同じ位置と配色) で、電卓 (`InputNumber`)・理由 (`ReasonSelect`)・一覧からの選択 (`Select`) は重ねて開ける ([D-24](decisions.md#d-24-端末の入力は電卓と下端のシートにする))。  
  確認と情報も `IDialog` を実装した `SheetDialog` がシート (`Confirm` / `Message`) で出す ([D-24](decisions.md#d-24-端末の入力は電卓と下端のシートにする))

---

## 🌱 6. 初期データ

起動時に会社設定がない (空の DB) なら投入する ([D-42](decisions.md#d-42-サンプルの取引は-api-を呼ぶツールで作る))。  
支払方法「前受金」は、それより前に作った DB にも起動時に足す。  
すべて日本語のサンプル。

| データ | 件数 | 内容 |
| --- | --- | --- |
| 会社設定 | 1 | 税端数 `Floor`、ポイント基準 `TaxIncluded`、営業日切替 `05:00` |
| 店舗 | 2 | `S001` 本店、`S002` 支店 (他店在庫照会のため 2 店舗) |
| レジ端末 | 3 | 本店 01 / 02、支店 01 |
| スタッフ | 4 | 本部管理者 (Admin、所属なし)、店長 (Manager、本店)、レジ担当 × 2 (Cashier、本店と支店)。PIN は A001 = 0000、M001 = 1111、C001 = 2222、C002 = 3333 |
| 管理画面のアカウント | 1 | アカウントが 1 件もなければ設定 (`Auth:InitialName` / `InitialPassword`、既定 `admin` / `admin`) の管理者を作る (初期データの SQL ではなく起動処理) |
| 税率 | 3 | 標準 10% / 軽減 8% / 非課税 |
| 支払方法 | 7 | 現金 (釣銭あり) / クレジット (参照要) / QR / 電子マネー / 商品券 / ポイント / 前受金 (受注の前受金を会計で充てる) |
| 部門 | 大分類 4 + 中分類 9 | 家電 (テレビ・レコーダー、冷蔵庫・洗濯機、生活家電)、カメラ (デジタルカメラ、レンズ、アクセサリ)、ホームセンター (工具、園芸、日用品)、サービス (中分類なし) |
| 商品 | 33 | 大分類ごとに 10 件 (JAN・型番・メーカー・還元率 10% / 5% / 1%・シリアル要フラグを織り交ぜる) + サービス 3 件 (配送料・延長保証・設置工事、`allowsPriceOverride`) |
| 値引 | 3 | 社員割引 10% (取引)、展示品 5% (明細、承認要)、端数値引 (定額、明細) |
| 在庫調整理由 | 5 | 破損 / 廃棄 / 万引き / 自家消費 / 棚卸差異 |
| 仕入先 | 3 | カメラ・家電の卸と日用品の商社 |
| 会員 | 5 | ポイント残高あり (0 / 少額 / 多額)、住所あり (配送先の複写用) |
| 在庫 | 物品 30 件 × 2 店舗 | 固定値 (0 / 少量 / 多量を混ぜる。他店在庫の表示確認用。サービスは在庫を持たない) |

取引・シフトのサンプルは起動時には投入しない (端末から作る)。  
レポート確認用には `server/tools/Pos.Server.SampleData` で直近数日分を生成できる ([D-42](decisions.md#d-42-サンプルの取引は-api-を呼ぶツールで作る))。

| `Pos.Server.SampleData` | 内容 |
| --- | --- |
| 対象 | 有効な店舗 × 端末ごとに、`--days` 日分 (既定 7)。開設中のシフトがある端末は省略 |
| 1 日の流れ | 08:30 に入荷 (初日のみ、物品を 10〜30 個。仕入先へ発注し、発注で作った入荷予定を予定どおり受領する。仕入先がなければ作る) → 09:00 開設 (釣銭準備金 3 万円) → 販売 `--per-day` ± 2 件 (既定 6) → 返品 (販売の 25%) → 取消 (30% の日に 1 件) → 出金 (60% の日) → 20:00 精算 (ときどき過不足) |
| 日次締め | 店舗ごとに、前日までの各日を締める (今日は営業中として残す)。締め済みや未精算のシフトがある日は省略する |
| 発注 | 店舗ごとに、入荷待ちの発注 (物品 3 つ、希望納期は 3 日後) を 1 件残す |
| 受注 | 作らない (受注と前受金は端末か管理画面で作る) |
| 認証 | `--user` / `--password` (既定 `admin` / `admin`) で管理画面のフォームからログインし、Cookie で API を呼ぶ (端末の一致は確かめられない) |
| 販売の内容 | 端末と同じ手順 (`SalesLogic.Calculate` → `TransactionCreateRequest` → `POST /transactions`)。1〜3 明細、明細値引 (明細の 20%。承認が要るものは承認者付き)、取引値引 (販売の 15%。社員割引か端数値引)、シリアル番号、会員 (販売の 35%。ポイント利用は残高まで)、支払はカード (35%。伝票番号付き) か現金 (千円単位の預り)、サービス明細のある販売の半分に配送先 |
| レシート番号 | `terminals/{id}` の `lastReceiptSeq` から連番を続ける |
| 乱数 | `--seed` (既定 1) で再現できる。サーバが 409 / 422 で拒否した取引は省略して続行する |
