# ソリューション構成

プロジェクト・層・パッケージと初期データ。  
設計判断は [decisions.md](decisions.md)、起動方法は [getting-started.md](getting-started.md)。

- [1. 方針](#1-方針)
- [2. プロジェクト構成](#2-プロジェクト構成)
- [3. サーバ (`Pos.Server.*`)](#3-サーバ-posserver)
- [4. 共有プロジェクト (`Pos.Domain` / `Pos.Shared`)](#4-共有プロジェクト-posdomain--posshared)
- [5. 端末 (`Pos.Terminal`)](#5-端末-posterminal)
- [6. 初期データ](#6-初期データ)

---

## 1. 方針

- **Service / Usecase の層は置かない**。  
  Endpoints / Blazor ページが Accessor (SQL) と Domain (ロジック) を直接使う ([D-19](decisions.md#d-19-技術スタックプロジェクト構成-テンプレート準拠))
- 通信データ (`XxxRequest` / `XxxResponse`) は `Pos.Shared` に置いてサーバと端末で共有する ([D-22](decisions.md#d-22-共有プロジェクト-通信データとドメインロジックは別プロジェクト))
- JSON は camelCase ([D-20](decisions.md#d-20-json-契約-camelcase))
- 認証は持たない ([D-09](decisions.md#d-09-認証端末登録-後回し))。  
  端末発の要求は本文の `storeId` / `terminalId` / `staffId` で識別する

---

## 2. プロジェクト構成

1 つのリポジトリに端末とサーバを同居させ (モノレポ)、Visual Studio では**サーバと端末を別々のソリューションで開いて個別に動かせる**ようにする ([D-32](decisions.md#d-32-リポジトリ構成-モノレポ--2-ソリューション))。

```
template-maui-pos/
├─ .editorconfig / .gitattributes / .gitignore / Directory.Build.props / Directory.Build.targets
├─ Analyzers.ruleset / CodeCoverage.runsettings / AGENTS.md / CLAUDE.md / LICENSE / README.md
│                                    ↑ ルートに 1 セット (MAUI 用の NoWarn NU1608 を含む)
├─ docs/                             本設計
├─ shared/                           両ソリューションに含める共有プロジェクト
│  ├─ Pos.Domain/                    ドメインロジック: 列挙型、計算 (税・値引按分・ポイント・返品)、業務ルール検証 (net10.0)
│  ├─ Pos.Domain.Tests/              計算ロジックの単体テスト。依存関係の検証テスト (Pos.Domain が UI / DB / HTTP に依存しない) を含む
│  └─ Pos.Shared/                    通信データ: XxxRequest / XxxResponse (net10.0)
├─ server/                           Service-CloudManager と同じ形
│  ├─ Pos.Server.slnx                サーバ + shared/ (Domain / Shared / Domain.Tests) + tests + tools
│  ├─ Pos.Server.sln.DotSettings
│  ├─ src/
│  │  ├─ Pos.Server.AppHost/         Aspire AppHost (template-blazor-server から)
│  │  ├─ Pos.Server.Core/            Accessors (SQL) / Models.Entity / Infrastructure
│  │  └─ Pos.Server.Host/            Minimal API + Blazor (MudBlazor) ホスト
│  ├─ tests/
│  │  ├─ Pos.Server.UnitTests/       bUnit (NavMenu)、SqlHelper などの単体テスト
│  │  └─ Pos.Server.IntegrationTests/ WebApplicationFactory による API テスト (SQLite 一時ファイル)
│  └─ tools/
│     └─ Pos.Server.SampleData/      サンプル取引の生成ツール (API 経由、§8)
└─ terminal/                         template-maui-keyboard と同じ形
   ├─ Pos.Terminal.slnx              端末 + shared/ (Domain / Shared)
   ├─ Pos.Terminal.sln.DotSettings / Settings.XamlStyler
   └─ Pos.Terminal/                  MAUI (net10.0-android)
```

- `server/Pos.Server.slnx` と `terminal/Pos.Terminal.slnx` は互いを含まない。  
  どちらも `../shared/` のプロジェクトを含む (同じプロジェクトを 2 つのソリューションに入れる)
- ルートの `Directory.Build.props` / `Analyzers.ruleset` / `.editorconfig` は両側に効く (MSBuild は上位フォルダの props を拾う。ruleset は csproj から `..\..\..\Analyzers.ruleset` で参照)
- 全体を 1 度にビルドしたい場合は `dotnet build server/Pos.Server.slnx && dotnet build terminal/Pos.Terminal.slnx`。  
  両方を含む統合ソリューションは作らない (VS で個別に動かすため)

依存関係:

```
Pos.Terminal ──────────┐
                       ├──> Pos.Shared ──> Pos.Domain
Pos.Server.Host ───────┤                      ▲
        │              │                      │
        └──> Pos.Server.Core ─────────────────┘
```

- `Pos.Domain` は何にも依存しない (`Usa.Smart.Core` 程度)。  
  `Pos.Shared` は `Pos.Domain` の列挙型を使う
- `Pos.Server.Core` はエンティティと Accessor を持ち、`Pos.Domain` の列挙型を使う。  
  `Pos.Shared` は参照しない (Request / Response ↔ エンティティの変換は Host の `Mappers`)
- `Pos.Terminal` は `Pos.Shared` を通信と Outbox の直列化にそのまま使う。  
  ローカル DB のエンティティは端末側 (`Models/Entity`) に持つ

---

## 3. サーバ (`Pos.Server.*`)

### 3.1 `Pos.Server.Core`

```
Accessors/
  StoreAccessor.cs, TerminalAccessor.cs, StaffAccessor.cs, CategoryAccessor.cs, TaxRateAccessor.cs,
  ProductAccessor.cs, DiscountAccessor.cs, PaymentMethodAccessor.cs, AdjustmentReasonAccessor.cs,
  CustomerAccessor.cs, TransactionAccessor.cs, ShiftAccessor.cs, InventoryAccessor.cs, ReportAccessor.cs,
  SettingsAccessor.cs
  Sql/{Accessor}.{Method}.sql       2-way SQL。Create.sql に DDL
Models/Entity/                       {Table 単数}Entity (Smart.Data.Accessor の [Key]。テーブル名は Builder 属性の Table)
Models/                              集計結果の record (ShiftTotals / PaymentMethodTotal / TaxRateTotal / CategoryTotal / PointTotals /
                                     SalesSummaryRow / ProductSalesRow / ProductInventoryLevel、SalesSummaryGroup)
Infrastructure/
  Data/SqlHelper.cs                  並び替え列の検証
  Data/DataProfile.cs                [AccessorProfile]: 列挙型ごとの EnumTextConverter<T> と DateOnly / DateTime のコンバータ
  Data/EnumTextConverter.cs          列挙型 ↔ TEXT、DateOnlyTextConverter.cs / DateTimeTextConverter.cs (UTC)
  Data/ReportSql.cs                  売上集計の GROUP BY 式 (生 SQL へ渡す閉じた集合)
Extensions.cs                        拡張メソッド置き場
```

- 複数テーブルにまたがる書き込み (取引登録・取消・精算) は、呼び出し側が `IDbProvider.UsingTxAsync` で Accessor の `DbTransaction` 付きメソッド (`InsertTransactionAsync(DbTransaction tx, ...)` など) を順に呼ぶ ([db-design.md §5](db-design.md#5-整合性と更新の単位))
- Accessor は SQL の実行だけを担い、業務ルールは `Pos.Domain` に置く
- Accessor の DI 登録は Host の `AddDataAccessors(typeof(SqlHelper).Assembly)` (Core アセンブリを走査)
- `DatabaseAccessor` (PRAGMA) を含めて 16 Accessor。  
  マスタは 一覧 (Count + QueryList) / QueryAsync / InsertAsync / UpdateAsync (Version 楽観ロック、スカラー引数) / DeleteAsync (論理削除) を共通形にする

### 3.2 `Pos.Server.Host`

```
Application/ApiRoutes.cs             /api/v1 の定数 (Prefix)
Application/ApplicationExtensions.cs ConfigureOpenApi / MapOpenApi (開発時 /swagger, /redoc)
Application/Log.cs, NamingPolicy.cs (camelCase), Styles.cs (MudBlazor テーマ + ダイアログ幅)
Application/DisplayText.cs, ChipText.cs  管理画面の表示文字列 (列挙型の日本語名・金額・日時) と、状態を示すチップの文言と色 (D-39)
Endpoints/                           静的クラス + MapGroup (ハンドラは private static、引数は DI + ルート / クエリ / 本文)
  SettingsEndpoints, StoreEndpoints, TerminalEndpoints, StaffEndpoints, CategoryEndpoints, TaxRateEndpoints,
  ProductEndpoints, DiscountEndpoints, PaymentMethodEndpoints, SyncEndpoints, CustomerEndpoints,
  TransactionEndpoints, ShiftEndpoints (+ summary/pdf), InventoryEndpoints (+ adjustment-reasons), ReportEndpoints (+ daily/pdf)
Infrastructure/Api/                  ApiProblems (errorCode / errors / expected 付き Problem Details)、ApiHelper (ページサイズ、並び替え、LIKE)、
                                     RuleViolationException (DB トランザクション内の業務ルール違反 → 422)
Models/Forms/                        管理画面のフォーム + FluentValidation (FormValidator<T> を基底に XxxForm / XxxFormValidator。マスタ 10 種 + Customer / Settings / InventoryChange / PointAdjust)
Models/Export/                       CSV の行 (ProductExportRow, SalesSummaryExportRow, ProductSalesExportRow。CsvHelper の [Name] で日本語見出し)
Mappers/                             Smart.Mapper: MasterMapper (マスタ・顧客)、TransactionMapper (取引一式と Domain の入出力)、ShiftMapper (集計付き応答)、
                                     InventoryMapper、ReportMapper、FormMapper (Entity ↔ Form。Guid? / DateOnly の変換は [MapUsing])
Components/
  App.razor, Routes.razor, _Imports.razor
  Layout/ (MainLayout, NavMenu (MudNavGroup。現在の URL のグループを開く), EmptyLayout, ReconnectModal)
  Pages/  Home (S-01), SalesSummaryPage (S-10), ProductSalesPage (S-11), TransactionsPage (S-20), ShiftsPage (S-30), InventoryPage (S-40),
          InventoryChangesPage (S-42), AdjustmentReasonsPage (S-44), ProductsPage (S-50), CategoriesPage (S-53), TaxRatesPage (S-54), DiscountsPage (S-55),
          PaymentMethodsPage (S-56), CustomersPage (S-60), CustomerDetailPage (S-61), StoresPage (S-70), TerminalsPage (S-71), StaffPage (S-72), SettingsPage (S-80),
          Error, NotFound。ページは .razor + .razor.cs
  Controls/ (ErrorBanner, ProgressOverlay, StoreSelect (店舗セレクタ), StatusChip (ChipText の文言 + 色))
  Dialogs/ (AppMessageBox, XxxEditDialog (EditDialogBase<TForm>。マスタ 10 種 + Customer), TransactionDetailDialog (S-21), ShiftDetailDialog (S-31),
            ProductInventoryDialog (S-41), InventoryChangeDialog (S-43), PointAdjustDialog (S-62), TerminalQrDialog (S-71))
Infrastructure/                      Components (AppComponentBase, DialogServiceExtensions, ErrorBoundaryLogger, SnackbarExtensions,
                                     PageComponentBase (読み込み / 実行 / エラー / 確認 / 編集ダイアログ), EditDialogBase<TForm>, StoreFilterState (scoped), NameLookup (ID → 名称)),
                                     ExceptionHandling (GlobalExceptionHandler), HealthChecks (DatabaseHealthCheck)
Infrastructure/Data/InitialData.cs   起動時の初期データ (§6)。固定 ID (InitialData.MainStoreId など) をテストと設定 QR で使う
Infrastructure/Data/                 CategoryOrder (大分類 → 中分類の並び)、InventoryChangeApplier (棚卸・調整の適用。API と画面で共用)
Infrastructure/Reports/              SalesSummaryQuery (売上集計の groupBy 振り分け・合計行・期間の既定値。API / CSV / 画面で共用)
                                     OysterReport の帳票: EmbeddedFontResolver (同梱 IPAex ゴシック)、ReportText (表示文字列・明細行の複製)、
                                     ShiftReportBuilder (精算レポート)、DailySalesReportBuilder (売上日報)   (D-37)
Assets/                              Fonts/ipaexg.ttf、Reports/*.xlsx (帳票テンプレート。出力ディレクトリへコピー)
Settings/                            LogSetting / ProfilerSetting
wwwroot/                             css/app.css, js/reconnect.js
```

- エンドポイントのハンドラは「Request を受ける → Domain で検証・計算 → Accessor で読み書き → Response を返す」を担う。  
  取引登録 (`POST /transactions`) の流れは [db-design.md §5.1](db-design.md#51-取引登録-post-transactions-は-1-つの-db-トランザクション)
- Blazor ページも同じ Accessor / Domain を `[Inject]` して使う。  
  エンドポイントとページで同じ処理が要る場合は静的ヘルパーにまとめる (層は増やさない): `SalesSummaryQuery`、`InventoryChangeApplier`、`ShiftMapper.ToSummaryResponseAsync`
- 管理画面の一覧・ダイアログはページ側で Accessor を呼び、コード重複は `IDialect.IsDuplicate`、楽観ロックは `UpdateAsync` の戻り値 0、使用中は件数クエリで判定する (API と同じ規則)
- `InitializeApplicationAsync` で起動時にスキーマ作成と初期データ投入を行う。  
  後から増えた列は `SchemaHelper.EnsureColumnAsync` で既存の DB に足す

---

## 4. 共有プロジェクト (`Pos.Domain` / `Pos.Shared`)

ドメインロジックの共有と通信データの共有は別の概念なので、プロジェクトを分ける ([D-22](decisions.md#d-22-共有プロジェクト-通信データとドメインロジックは別プロジェクト))。

### 4.1 `Pos.Domain`

```
Enums.cs              TransactionType, TransactionStatus, ProductKind, PaymentKind, DiscountType, DiscountScope,
                      TaxKind, StaffRole, ShiftStatus, CashEventType, InventoryChangeType, PointHistoryType,
                      TaxRounding, PointBasis
Sales/
  SalesCalculator.cs  SalesInput (明細・値引・支払・会社設定) → SalesResult (計算項目、api-design §4)
  SalesInput.cs / SalesResult.cs  計算の入出力 (record。Request / Response とは別の純粋な型)
  ReturnCalculator.cs ReturnInput (元取引の明細 + 返品明細) → SalesResult (§4.5)
  ReturnInput.cs      返品の入力 (ReturnOriginalLine = 元明細の事実)
  TaxCalculator.cs    税グループ集計と明細への按分税額 (internal、販売・返品で共用)
  Allocation.cs       最大剰余法の按分
  Rounding.cs         TaxRounding の丸め
  SalesResultComparer.cs  端末が送った計算項目と再計算の一致判定
Rules/
  TransactionRules.cs ValidateSale / ValidateReturn / ValidateVoid (api-design §3.12) → TransactionValidation (Errors / Warnings / Expected)
  TransactionValidation.cs  検証結果と、検証に必要な事実 (SaleContext / ReturnContext / VoidContext、ShiftFact / ProductFact ...)
  ErrorCode.cs        ErrorCode / WarningCode (api-design §5) と UPPER_SNAKE_CASE への変換 (ToCode)
```

- 純粋関数 (入力を変更しない) にし、`Pos.Domain.Tests` で [api-design.md §4.6](api-design.md#46-計算例) を含むケースを固定する (73 件)
- `SalesCalculator` の入出力は `Pos.Shared` の Request / Response に依存しない。  
  変換は呼び出し側 (端末のカート、サーバのエンドポイント) が行う
- `TransactionRules` は DB を見ない。  
  シフト・商品・元取引などの事実は呼び出し側が Context に詰めて渡す。  
  検証できる入力なら再計算結果を `Expected` に返すので、エンドポイントはそれを `CALCULATION_MISMATCH` の `expected` と応答の計算項目に使う
- `Pos.Domain.Tests` の `DependencyTests` が「UI / DB / HTTP / `Pos.Shared` を参照していない」ことを検証する

### 4.2 `Pos.Shared`

```
Common/        ListResponse<T> (Total / Page / Size / Items。XxxListResponse の基底)、ProblemResponse (Problem Details + errorCode / errors / expected)、
               JsonDateTimeConverter (yyyy-MM-ddTHH:mm:ss.fffZ。サーバと端末で共用)
Settings/      SettingsResponse / SettingsUpdateRequest
Stores/ Terminals/ Staff/ Categories/ TaxRates/ Products/ Discounts/ PaymentMethods/
               XxxResponse / XxxListResponse / XxxCreateRequest / XxxUpdateRequest
Sync/          SyncMastersResponse
Customers/     CustomerResponse / CustomerListResponse / CustomerCreateRequest / CustomerUpdateRequest,
               PointHistoryResponse / PointHistoryListResponse, PointAdjustRequest
Transactions/  TransactionRequest (+ TransactionRequestLine / Discount / TaxSummary / Payment / Delivery / Void),
               TransactionResponse (+ TransactionResponseLine / ... / Warning) / TransactionListResponse,
               TransactionVoidRequest, TransactionCalculateRequest, TransactionCalculationResponse (計算項目のみ。calculate の応答と expected)
Shifts/        ShiftOpenRequest, ShiftResponse (+ Denomination / Totals) / ShiftListResponse, ShiftCloseRequest (+ Denomination),
               CashEventRequest / CashEventResponse / CashEventListResponse, ShiftSummaryResponse (+ PaymentMethod / TaxRate / Category / Points / Cash)
Inventory/     InventoryLevelResponse / InventoryLevelListResponse, ProductInventoryResponse (+ Level),
               InventoryChangeRequest (+ Change) / InventoryChangeResultResponse (+ Result、InventoryChangeResultStatus),
               InventoryChangeResponse / InventoryChangeListResponse, AdjustmentReasonResponse / ListResponse / CreateRequest / UpdateRequest
Reports/       SalesSummaryResponse (+ Row), ProductSalesResponse (+ Row)
```

- 名前空間はフォルダごと (`Pos.Shared.Transactions` など)。  
  書き方は (`{ get; set; } = default!` のクラス、Request には `Required` / `MaxLength` / `Range`)。  
  camelCase への変換はシリアライザ設定で行い、属性は付けない
- 入れ子の要素は親の名前に要素名を続ける (`TransactionResponseLine`)
- 列挙型は `Pos.Domain` のものをそのまま使う。  
  エラーコード定数は持たず、`Pos.Domain` の `ErrorCode.ToCode()` / `WarningCode.ToCode()` と `ProblemResponse.ErrorCode` (文字列) で突き合わせる
- 日付は `DateOnly`、日時は `DateTime` (UTC)。  
  サーバは `ConfigureHttpJsonOptions` で `JsonDateTimeConverter` と `JsonStringEnumConverter` を登録し、端末は `HttpService.JsonOptions` で同じものを登録する。  
  形式は `Pos.Server.IntegrationTests` の `JsonContractTests` で固定
- 名前空間 `Pos.Shared` は VB の予約語と重なるため CA1716 を、`ImageUrl` は CA1056 を `Pos.Shared` の `GlobalSuppressions.cs` で抑止している ([D-38](decisions.md#d-38-警告の抑止))

---

## 5. 端末 (`Pos.Terminal`)

`net10.0-android`。

```
MauiProgram.cs BunnyTail DI、Navigator (HierarchyEffectPlugin で Forward / Back のスライド、D-36)、Dialog / Popup、フォントは MaterialIcons のみ
                                     + BarcodeScanning、HttpClient (IHttpClientFactory)、IDbProvider (SQLite)、DataAccessor、HttpService / NetworkOperator / SyncWorker、State
MainPage.xaml / MainPageViewModel シェル (タイトル + 店舗-端末 担当 + 未送信バッジ + F1〜F4)。起動時に Setup (未設定) または StaffSelect へ
App.xaml.cs                          起動時にローカル DB の作成、Session の復元、SyncWorker の開始
Shell/ ShellProperty (+ Active: 表示中の View だけがシェルを更新) / ShellEvent / ShellUpdateBehavior / IShellControl
Input/ Behaviors/ Helpers/ 物理キー・ショートカット、Entry / Label / Scroll などの動作、フォーカス制御
Behaviors/BarcodeBind.cs, Messaging/BarcodeController.cs   CameraView のバインド
Converters/                          QrImageSourceConverter (QRCoder)、YenConverter
Helpers/                             DisplayText (金額・日時・列挙型の日本語)、AppDialogExtensions (IDialog の日本語ボタン)、SettingParser (設定 QR)、Data/ (DataProfile と型変換)
Permissions.cs                       カメラ権限
Modules/
  ViewId.cs, DialogId.cs, Parameters.cs (遷移パラメータ: スキャンモード / 戻り先 / 取引 ID / 会員 / 呼び出し元の状態), AppViewModelBase.cs, AppDialogViewModelBase.cs
  Setup/      SetupView (T-00), StaffSelectView (T-01)
  Main/       MenuView (T-02)
  Shift/      ShiftOpenView (T-03), CashEventView (T-50), ShiftCloseView (T-51), ShiftReportView (T-52), DenominationsView (金種別入力ポップアップ)
  Sales/      SalesView (T-10), ScanView (T-11), ProductSearchView (T-12), CustomerSelectView (T-14),
              DeliveryView (T-16), HoldView (T-17), PaymentView (T-20), CompleteView (T-21), ReceiptView (T-22),
              LineEditView (P-13), DiscountView (P-15), DiscountChooser (定義済み / 任意の値引を IDialog で選ぶ)
  Returns/    ReturnView (T-40), ReturnLinesView (T-41), RefundView (T-42)
  History/    TransactionListView (T-30), TransactionDetailView (T-31)
  Inquiry/    ProductInquiryView (T-60), CustomerInquiryView (T-61), CustomerEditView (T-62)
  Inventory/  StockCountView (T-70)
  Report/     SalesReportView (T-80)
  Setting/    SettingView (T-90)
  Navigation/Modal/  InputNumberView、ReasonSelectView (理由の選択)
Models/
  Input/      NumberInputParameter, NumberInputModel
  Entity/     ローカル DB のエンティティ (LocalTransaction / LocalShift / LocalCashEvent / Outbox / SyncState / HoldCart。マスタは Pos.Shared の Response をそのまま使う)
  Sales/      会計中の状態 (Cart, CartLine, CartDiscount, CartPayment, CartDelivery)
  SummaryRow.cs                      集計・詳細画面の行と節
Services/
  DataAccessor.cs + Sql/            ローカル SQLite (Smart.Data.Accessor、2-way SQL)
  HttpService.cs / ApiResult.cs / ApiContext.cs / ApiNames.cs   HttpClient による API 呼び出し (Pos.Shared の Request / Response、失敗時は Problem Details、D-40)
  NetworkOperator.cs                 オンライン限定操作の接続確認・インジケータ・エラー通知
  SyncWorker.cs                      マスタ差分同期と Outbox 送信のバックグラウンド実行 (api-design §6)、レシート番号の採番
  TransactionBuilder.cs              Cart / 返品明細 → Pos.Domain の入力 → TransactionRequest (端末側の履歴用 TransactionResponse も作る)
  TransactionWriter.cs               ローカル取引 + Outbox + 自店在庫の書き込み (販売・返品・取消)
  ShiftSummaryBuilder.cs             ローカルの取引・入出金からシフト集計 (精算の予想現金、オフライン時の精算レポート)
  ReceiptFormatter.cs / ReceiptRenderer.cs   レシート文字列 (等幅 32 桁) と画像化 (SkiaSharp、桁位置で描画)
State/
  DeviceState.cs / StartupState.cs
  Settings.cs                        ApiEndPoint / StoreId / TerminalId / OpenSalesAfterLogin (IPreferences)
  Session.cs                         会社設定、店舗、端末、選択中スタッフ、開設中シフト、未送信 / 要確認件数、営業日
  SalesState.cs / StockState.cs      画面をまたぐ会計中の状態 (カート・支払・完了取引・返品元) と棚卸の入力リスト
Resources/
  Fonts/      MaterialIcons のみ
  Styles/     Colors.xaml、Styles.xaml (POS 節: Pos 接頭辞のスタイル、ヘッダの状態表示 / 一覧行 / チップ / テンキー / 入力欄)
Platforms/Android/ MainActivity (pos.terminal.MainActivity)、KeyInputDriver、AndroidHelper。CAMERA 権限
```

- ViewModel が `DataAccessor` / `HttpService` / `Pos.Domain` を直接使う (Service / Usecase の層は置かない)。  
  画面をまたぐ処理は `Services/` の静的ヘルパー (`TransactionBuilder` / `TransactionWriter` / `ShiftSummaryBuilder`) に置く
- 通信は HttpClient + `System.Text.Json` (`HttpService.JsonOptions`: camelCase / null 省略 / 列挙型は文字列 / `JsonDateTimeConverter`) ([D-40](decisions.md#d-40-端末の通信-rester-ではなく-httpclient))
- 画面遷移は `Navigator.ForwardAsync` のみ (スタックは使わない)。  
  複数の画面から使う画面 (スキャン、会員選択、レシートなど) は `Parameters.WithReturnTo` で戻り先を受け取り、スキャンは呼び出し元の戻り先と状態 (`WithCallerReturnTo` / `WithState`) をそのまま返す
- 販売・会計画面は `Styles.xaml` の POS 節 (白い行 + 区切り線、名称は太字、金額は青、[D-43](decisions.md#d-43-端末シェルのデザイン-pos-画面に合わせる)) を使う。  
  ポップアップの中では別のポップアップを重ねず、数量などの入力は `IDialog.PromptAsync` を使う

---

## 6. 初期データ

起動時にテーブルが空なら投入する ([D-30](decisions.md#d-30-初期データの規模))。  
すべて日本語のサンプル。

| データ | 件数 | 内容 |
| --- | --- | --- |
| 会社設定 | 1 | 税端数 `Floor`、ポイント基準 `TaxIncluded`、営業日切替 `05:00` |
| 店舗 | 2 | `S001` 本店、`S002` 支店 (他店在庫照会のため 2 店舗) |
| レジ端末 | 3 | 本店 01 / 02、支店 01 |
| スタッフ | 4 | 本部管理者 (Admin)、店長 (Manager)、レジ担当 × 2 (Cashier) |
| 税率 | 3 | 標準 10% / 軽減 8% / 非課税 |
| 支払方法 | 6 | 現金 (釣銭あり) / クレジット (参照要) / QR / 電子マネー / 商品券 / ポイント |
| 部門 | 大分類 3 + 中分類 9 | 家電 (テレビ・冷蔵庫・生活家電)、カメラ (デジタルカメラ・レンズ・アクセサリ)、ホームセンター (工具・園芸・日用品) + サービス |
| 商品 | 33 | 大分類ごとに 10 件 (JAN・型番・メーカー・還元率 10% / 5% / 1%・シリアル要フラグを織り交ぜる) + サービス 3 件 (配送料・延長保証・設置工事、`allowsPriceOverride`) |
| 値引 | 3 | 社員割引 10% (取引)、展示品 5% (明細、承認要)、端数値引 (定額、明細) |
| 在庫調整理由 | 5 | 破損 / 廃棄 / 万引き / 自家消費 / 棚卸差異 |
| 会員 | 5 | ポイント残高あり (0 / 少額 / 多額)、住所あり (配送先の複写用) |
| 在庫 | 全商品 × 2 店舗 | 固定値 (0 / 少量 / 多量を混ぜる。他店在庫の表示確認用) |

取引・シフトのサンプルは起動時には投入しない (端末から作る)。  
レポート確認用には `server/tools/Pos.Server.SampleData` で直近数日分を生成できる ([D-41](decisions.md#d-41-サンプル取引の生成-api-経由のコンソールツール))。

| `Pos.Server.SampleData` | 内容 |
| --- | --- |
| 対象 | 有効な店舗 × 端末ごとに、`--days` 日分 (既定 7)。開設中のシフトがある端末は省略 |
| 1 日の流れ | 08:30 に在庫調整 (初日のみ、物品を 10〜30 個「サンプル入荷」) → 09:00 開設 (釣銭準備金 3 万円) → 販売 `--per-day` ± 2 件 (既定 6) → 返品 (販売の 25%) → 取消 (30% の日に 1 件) → 出金 (60% の日) → 20:00 精算 (ときどき過不足) |
| 販売の内容 | 端末と同じ手順 (`SalesCalculator` → `TransactionRequest` → `POST /transactions`)。1〜3 明細、明細値引 (承認者付き) / 取引値引 15%、シリアル番号、会員 (ポイント利用は残高まで)、カード 35% (伝票番号付き) / 現金 (千円単位の預り)、サービス明細には配送先 |
| レシート番号 | `terminals/{id}` の `lastReceiptSeq` から連番を続ける |
| 乱数 | `--seed` (既定 1) で再現できる。サーバが 409 / 422 で拒否した取引は省略して続行する |
