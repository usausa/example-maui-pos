# ソリューション構成 (v0.4)

参考プロジェクト (既存テンプレート) の流儀に合わせた構成。判断の経緯は [decisions.md §3](decisions.md#3-参考プロジェクトを確認して合わせた判断) と [D-34](decisions.md#d-34-参考プロジェクトの差し替え-phase-0)。

- [1. 参考プロジェクトと対応](#1-参考プロジェクトと対応)
- [2. プロジェクト構成](#2-プロジェクト構成)
- [3. サーバ (`Pos.Server.*`)](#3-サーバ-posserver)
- [4. 共有プロジェクト (`Pos.Domain` / `Pos.Shared`)](#4-共有プロジェクト-posdomain--posshared)
- [5. 端末 (`Pos.Terminal`)](#5-端末-posterminal)
- [6. 実行・開発](#6-実行開発)
- [7. 実装計画](#7-実装計画)
- [8. 初期データ](#8-初期データ)
- [9. 実装時に確認する事項](#9-実装時に確認する事項)

---

## 1. 参考プロジェクトと対応

| 本サンプル | ベースにするプロジェクト | 流用したもの |
| --- | --- | --- |
| サーバ (`Pos.Server.*`) | `D:\GitHubService\Service-CloudManager` (`CloudManager.Core` / `CloudManager.Host`) | ソリューション構成 (Core / Host / UnitTests / IntegrationTests)、`Program.cs` と `ApplicationExtensions` (Serilog / ヘルスチェック / ProblemDetails / 圧縮 / エラーページ)、MudBlazor レイアウト・ダイアログ・Snackbar・FluentValidation、`NavMenu` のグループ化、`SqlHelper`、テスト基盤 (`TestApplicationFactory`、`MudBlazorTestBase`) |
| | `D:\GitHubTemplate\template-blazor-server` (`Template.BlazorServer.*`) | Aspire AppHost、OpenAPI (`Microsoft.AspNetCore.OpenApi` + NSwag の Swagger UI / ReDoc)。CSV 出力 (CsvHelper) と PDF 帳票 (OysterReport) は Phase 2 以降で参考にする |
| | `D:\GitHubTemplate\template-maui-server` | Phase 2 の認証 (管理画面 Cookie ログイン、API JWT)、設定 QR (`QrPage`) の参考 |
| 端末 (`Pos.Terminal`) | `D:\GitHubTemplate\template-maui-keyboard` (`Template.MobileApp`) | `MauiProgram` の構成 (BunnyTail DI、`[ComponentRegistration]`)、シェル (`MainPage` + `ShellProperty` + F1〜F4)、`AppViewModelBase`、`InputNumber` ポップアップ、Input (物理キー・ショートカット)、Behaviors、`Colors.xaml` / `Styles.xaml` |
| | `D:\GitHubTemplate\template-maui` (`Template.MobileApp`) | 販売・会計画面のデザイン (`UIPosView`)。QR スキャン / 表示、`Settings` / `SettingParser`、`HttpService` / `NetworkOperator`、SQLite `DataAccessor` は Phase 6 で取り込む |

テンプレートと違う点 (利用者指示):

- **Service / Usecase の層は置かない**。Endpoints / Blazor ページが Accessor (SQL) と Domain (ロジック) を直接使う
- 通信データ (`XxxRequest` / `XxxResponse`) は `Pos.Shared` に置いてサーバと端末で共有する
- JSON は camelCase (`Service-CloudManager` の `NamingPolicy` と同じ)
- 認証は MVP では持たない (ベースの `Service-CloudManager` にもない)。Phase 2 で追加

---

## 2. プロジェクト構成

1 つのリポジトリに端末とサーバを同居させ (モノレポ)、Visual Studio では**サーバと端末を別々のソリューションで開いて個別に動かせる**ようにする ([D-32](decisions.md#d-32-リポジトリ構成-モノレポ--2-ソリューション))。

```
template-maui-pos/
├─ .editorconfig / .gitattributes / .gitignore / Directory.Build.props / Directory.Build.targets
├─ Analyzers.ruleset / CodeCoverage.runsettings / AGENTS.md / CLAUDE.md / LICENSE / README.md
│                                    ↑ ルートに 1 セット (テンプレート間で同一。MAUI 用の NoWarn NU1608 を含める)
├─ docs/                             本設計
├─ shared/                           両ソリューションに含める共有プロジェクト
│  ├─ Pos.Domain/                    ドメインロジック: 列挙型、計算 (税・値引按分・ポイント・返品)、業務ルール検証 (net10.0)
│  ├─ Pos.Domain.Tests/              計算ロジックの単体テスト。依存関係の検証テスト (Pos.Domain が UI / DB / HTTP に依存しない) を含む
│  └─ Pos.Shared/                    通信データ: XxxRequest / XxxResponse (net10.0)
├─ server/                           Service-CloudManager と同じ形
│  ├─ Pos.Server.slnx                サーバ + shared/ (Domain / Shared / Domain.Tests) + tests
│  ├─ Pos.Server.sln.DotSettings
│  ├─ src/
│  │  ├─ Pos.Server.AppHost/         Aspire AppHost (template-blazor-server から)
│  │  ├─ Pos.Server.Core/            Accessors (SQL) / Models.Entity / Infrastructure
│  │  └─ Pos.Server.Host/            Minimal API + Blazor (MudBlazor) ホスト
│  └─ tests/
│     ├─ Pos.Server.UnitTests/       bUnit (NavMenu)、SqlHelper などの単体テスト
│     └─ Pos.Server.IntegrationTests/ WebApplicationFactory による API テスト (SQLite 一時ファイル)
└─ terminal/                         template-maui-keyboard と同じ形
   ├─ Pos.Terminal.slnx              端末 + shared/ (Domain / Shared)
   ├─ Pos.Terminal.sln.DotSettings / Settings.XamlStyler
   └─ Pos.Terminal/                  MAUI (net10.0-android)
```

- `server/Pos.Server.slnx` と `terminal/Pos.Terminal.slnx` は互いを含まない。どちらも `../shared/` のプロジェクトを含む (同じプロジェクトを 2 つのソリューションに入れる)
- ルートの `Directory.Build.props` / `Analyzers.ruleset` / `.editorconfig` は両側に効く (MSBuild は上位フォルダの props を拾う。ruleset は csproj から `..\..\..\Analyzers.ruleset` で参照)
- 全体を 1 度にビルドしたい場合は `dotnet build server/Pos.Server.slnx && dotnet build terminal/Pos.Terminal.slnx`。両方を含む統合ソリューションは作らない (VS で個別に動かすため)

依存関係:

```
Pos.Terminal ──────────┐
                       ├──> Pos.Shared ──> Pos.Domain
Pos.Server.Host ───────┤                      ▲
        │              │                      │
        └──> Pos.Server.Core ─────────────────┘
```

- `Pos.Domain` は何にも依存しない (`Usa.Smart.Core` 程度)。`Pos.Shared` は `Pos.Domain` の列挙型を使う
- `Pos.Server.Core` はエンティティと Accessor を持ち、`Pos.Domain` の列挙型を使う。`Pos.Shared` は参照しない (Request / Response ↔ エンティティの変換は Host の `Mappers`)
- `Pos.Terminal` は `Pos.Shared` を通信と Outbox の直列化にそのまま使う。ローカル DB のエンティティは端末側 (`Models/Entity`) に持つ
- `Pos.Domain` に型がない間 (Phase 1 まで) は `global using Pos.Domain;` を各プロジェクトに入れない (空の名前空間はコンパイルエラーになる)

---

## 3. サーバ (`Pos.Server.*`)

### 3.1 `Pos.Server.Core`

`CloudManager.Core` から Services / AWS を除いた構成。

```
Accessors/
  StoreAccessor.cs, TerminalAccessor.cs, StaffAccessor.cs, CategoryAccessor.cs, TaxRateAccessor.cs,
  ProductAccessor.cs, DiscountAccessor.cs, PaymentMethodAccessor.cs, AdjustmentReasonAccessor.cs,
  CustomerAccessor.cs, TransactionAccessor.cs, ShiftAccessor.cs, InventoryAccessor.cs, ReportAccessor.cs,
  SettingsAccessor.cs
  Sql/{Accessor}.{Method}.sql       2-way SQL。Create.sql に DDL
Models/Entity/                       {Table 単数}Entity (Smart.Data.Accessor の [Name] / [Key] / [TypeHandler])
Models/                              集計結果の record (ReportAccessor の戻り値など)
Infrastructure/
  Data/SqlHelper.cs                  並び替え列の検証 (Phase 0 で作成済み)
  Data/EnumTextConverter.cs          列挙型 ↔ TEXT の [TypeHandler]
Extensions.cs                        拡張メソッド置き場 (テンプレート既存の書き方)
```

- 複数テーブルにまたがる書き込み (取引登録・取消・精算) は、呼び出し側が `IDbProvider.UsingTxAsync` で Accessor の `DbTransaction` 付きメソッド (`InsertTransactionAsync(DbTransaction tx, ...)` など) を順に呼ぶ ([db-design.md §5](db-design.md#5-整合性と更新の単位))
- Accessor は SQL の実行だけを担い、業務ルールは `Pos.Domain` に置く
- Accessor の DI 登録は Host の `AddDataAccessors(typeof(SqlHelper).Assembly)` (Core アセンブリを走査)

### 3.2 `Pos.Server.Host`

`CloudManager.Host` と同じ層構成。Phase 0 で作成済みの骨組みは (テンプレート由来) と記す。

```
Application/ApiRoutes.cs             /api/v1 の定数 (Prefix)
Application/ApplicationExtensions.cs (テンプレート由来) + ConfigureOpenApi / MapOpenApi (開発時 /swagger, /redoc)
Application/Log.cs, NamingPolicy.cs (camelCase), Styles.cs (MudBlazor テーマ)   (テンプレート由来)
Endpoints/
  SettingsEndpoints, StoreEndpoints, TerminalEndpoints, StaffEndpoints, CategoryEndpoints, TaxRateEndpoints,
  ProductEndpoints, DiscountEndpoints, PaymentMethodEndpoints, SyncEndpoints, CustomerEndpoints,
  TransactionEndpoints, ShiftEndpoints, InventoryEndpoints, ReportEndpoints
Models/Forms/                        Blazor 用フォーム + FluentValidation (ProductForm / ProductFormValidator ...)
Mappers/                             Smart.Mapper: Entity ↔ Shared の Request / Response、Entity ↔ Form
Components/
  App.razor, Routes.razor, _Imports.razor              (テンプレート由来)
  Layout/ (MainLayout, NavMenu, EmptyLayout, ReconnectModal)   NavMenu は Phase 5 で MudNavGroup 構成にする
  Pages/  (Home, Error, NotFound + screen-design §2.3 の S-xx)
  Controls/ (ErrorBanner, ProgressOverlay)             (テンプレート由来)
  Dialogs/ (AppMessageBox + 編集ダイアログ、詳細ダイアログ)
Infrastructure/                      Components (AppComponentBase, DialogServiceExtensions, ErrorBoundaryLogger, SnackbarExtensions),
                                     ExceptionHandling (GlobalExceptionHandler), HealthChecks (DatabaseHealthCheck)   (テンプレート由来)
Infrastructure/Reports/              OysterReport の帳票: EmbeddedFontResolver (同梱 IPAex ゴシック)、ShiftReportBuilder (精算レポート)、
                                     DailySalesReportBuilder (売上日報)、ReceiptReportBuilder (レシート再発行、◎)   (D-37)
Assets/                              Fonts/ipaexg.ttf、Reports/*.xlsx (帳票テンプレート。出力ディレクトリへコピー)
Settings/                            LogSetting / ProfilerSetting   (テンプレート由来)
wwwroot/                             css/app.css, js/reconnect.js   (テンプレート由来)
```

- エンドポイントのハンドラは「Request を受ける → Domain で検証・計算 → Accessor で読み書き → Response を返す」を担う。取引登録 (`POST /transactions`) の流れは [db-design.md §5.1](db-design.md#51-取引登録-post-transactions-は-1-つの-db-トランザクション)
- Blazor ページも同じ Accessor / Domain を `[Inject]` して使う。エンドポイントとページで同じ処理が要る場合は静的ヘルパーにまとめる (層は増やさない)
- `InitializeApplicationAsync` で起動時にスキーマ作成と初期データ投入を行う (Phase 3)
- 認証は MVP では持たない。Phase 2 で `template-maui-server` を参考に追加する

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

- 純粋関数 (入力を変更しない) にし、`Pos.Domain.Tests` で [api-design.md §4.6](api-design.md#46-計算例) を含むケースを固定する (Phase 1 で 73 件)
- `SalesCalculator` の入出力は `Pos.Shared` の Request / Response に依存しない。変換は呼び出し側 (端末のカート、サーバのエンドポイント) が行う
- `TransactionRules` は DB を見ない。シフト・商品・元取引などの事実は呼び出し側が Context に詰めて渡す。検証できる入力なら再計算結果を `Expected` に返すので、エンドポイントはそれを `CALCULATION_MISMATCH` の `expected` と応答の計算項目に使う
- `Pos.Domain.Tests` の `DependencyTests` が「UI / DB / HTTP / `Pos.Shared` を参照していない」ことを検証する (Phase 0 で作成済み)

### 4.2 `Pos.Shared`

```
Masters/      SettingsResponse / SettingsUpdateRequest, StoreResponse / StoreCreateRequest / StoreUpdateRequest / StoreListResponse,
              TerminalXxx, StaffXxx, CategoryXxx, TaxRateXxx, ProductXxx, DiscountXxx, PaymentMethodXxx, AdjustmentReasonXxx,
              SyncMastersResponse
Customers/    CustomerXxx, PointHistoryResponse / PointHistoryListResponse, PointAdjustRequest
Transactions/ TransactionRequest / TransactionResponse (+ TransactionRequestLine / TransactionResponseLine,
              TransactionDiscount, TaxSummary, TransactionPayment, Delivery, VoidInfo),
              TransactionListResponse, TransactionVoidRequest, TransactionCalculateRequest
Shifts/       ShiftOpenRequest / ShiftResponse / ShiftListResponse, CashEventRequest / CashEventResponse,
              ShiftCloseRequest, ShiftSummaryResponse
Inventory/    InventoryLevelResponse / InventoryLevelListResponse, InventoryChangeRequest / InventoryChangeResultResponse,
              InventoryChangeResponse / InventoryChangeListResponse, ProductInventoryResponse
Reports/      SalesSummaryResponse, ProductSalesResponse
Common/       ErrorCodes (定数)
```

- テンプレートの `Models/Api` と同じ書き方 (プロパティ初期化子付きのクラス、または record)。camelCase への変換はシリアライザ設定で行い、属性は付けない
- 入れ子の要素はテンプレートの `DataListResponseEntry` に倣い、親の名前に要素名を続ける (`TransactionResponseLine`)

---

## 5. 端末 (`Pos.Terminal`)

`net10.0-android`。`template-maui-keyboard` の `Template.MobileApp` を `Pos.Terminal` にリネームし、サンプル画面 (Key モジュール) を除いたもの。Phase 0 で作成済みの骨組みは (テンプレート由来) と記す。

```
MauiProgram.cs                       (テンプレート由来) BunnyTail DI、Navigator (HierarchyEffectPlugin で Forward / Back のスライド、D-36)、Dialog / Popup、フォントは MaterialIcons のみ
MainPage.xaml / MainPageViewModel    (テンプレート由来) シェル (タイトル + F1〜F4)。起動時に ViewId.Menu へ
Shell/                               (テンプレート由来) ShellProperty / ShellEvent / ShellUpdateBehavior / IShellControl
Input/ Behaviors/ Extender/ Helpers/ (テンプレート由来) 物理キー・ショートカット、Entry / Label / Scroll などの動作、フォーカス制御
Modules/
  ViewId.cs, DialogId.cs, Parameters.cs, AppViewModelBase.cs, AppDialogViewModelBase.cs   (テンプレート由来)
  Main/       MenuView (T-02。Phase 0 では全ボタン無効)、SettingView (T-90)
  Startup/    SetupView (T-00), StaffSelectView (T-01)
  Register/   ShiftOpenView (T-03), CashEventView (T-50), ShiftCloseView (T-51), ShiftReportView (T-52)
  Sales/      SalesView (T-10), ScanView (T-11), ProductSearchView (T-12), CustomerSelectView (T-14),
              DeliveryView (T-16), HoldView (T-17), PaymentView (T-20), CompleteView (T-21), ReceiptView (T-22)
  Return/     ReturnView (T-40), ReturnLinesView (T-41), RefundView (T-42)
  History/    TransactionListView (T-30), TransactionDetailView (T-31)
  Inquiry/    ProductInquiryView (T-60), CustomerInquiryView (T-61), CustomerEditView (T-62)
  Stock/      StockCountView (T-70)
  Report/     SalesReportView (T-80)
  Navigation/Modal/InputNumberView   (テンプレート由来) テンキーポップアップ
  Dialogs/    LineEditView (P-13), DiscountView (P-15), ReasonSelectView, DenominationsView
Models/
  Input/      NumberInputParameter, NumberInputModel   (テンプレート由来)
  Entity/     ローカル DB のエンティティ (マスタキャッシュ、取引、Outbox)
  Cart/       会計中の状態 (Cart, CartLine ...)。Pos.Domain の SalesCalculator への入力を組み立てる
Services/
  DataAccessor.cs + Sql/            ローカル SQLite (Smart.Data.Accessor)
  HttpService.cs                     Rester による API 呼び出し (Pos.Shared の Request / Response)
  ApiContext.cs / ApiDelegatingHandler.cs  template-maui から (トークンは Phase 2)
  ReceiptFormatter.cs                レシート文字列生成
  SyncWorker.cs                      マスタ差分同期と Outbox 送信のバックグラウンド実行 (api-design §6)
  NetworkOperator.cs / NetworkInteraction.cs  template-maui から
State/
  DeviceState.cs / StartupState.cs   (テンプレート由来)
  Settings.cs                        ApiEndPoint / StoreId / TerminalId / OpenSalesAfterLogin など (IPreferences)
  Session.cs                         選択中スタッフ、開設中シフト、未送信件数
Resources/
  Fonts/      MaterialIcons のみ
  Styles/     Colors.xaml (テンプレート由来)、Styles.xaml (テンプレート由来 + POS 節: Pos 接頭辞のスタイル)
Platforms/Android/                   (テンプレート由来) MainActivity (pos.terminal.MainActivity)、KeyInputDriver、AndroidHelper
```

- ViewModel が `DataAccessor` / `HttpService` / `Pos.Domain` を直接使う (Service / Usecase の層は置かない)。販売フロー (スキャン → 明細 → 会計 → Outbox 保存) は `SalesViewModel` / `PaymentViewModel` と `Cart` に置く
- Phase 6 で `template-maui` から QR スキャン / 表示 (`BarcodeScanning.Native.Maui`, `QRCoder`)、`Settings` / `SettingParser`、`HttpService` / `NetworkOperator` (Rester)、`DataAccessor` (Microsoft.Data.Sqlite) を取り込む。パッケージもその時点で追加する
- 販売・会計画面のデザインは `template-maui` の `UIPosView` (白い行 + 区切り線、名称は太字、金額は青、MaterialIcons のアイコン) に倣い、`Styles.xaml` の POS 節を使う

---

## 6. 実行・開発

| 項目 | 内容 |
| --- | --- |
| サーバ起動 | `server/Pos.Server.slnx` を VS で開く、または `dotnet run --project server/src/Pos.Server.Host` / Aspire (`dotnet run --project server/src/Pos.Server.AppHost`、ダッシュボードは http://localhost:15000)。ポート 8080 (`appsettings.json` の `http_ports`) |
| DB | 起動時に `pos.db` (SQLite、実行ディレクトリ) を自動作成。テーブルと初期データ (店舗 / 端末 / 税率 / 支払方法 / 部門・商品サンプル) は Phase 3 で `InitializeApplicationAsync` に追加 |
| OpenAPI | 開発時 `/swagger`、`/redoc`、`/openapi/v1.json` |
| テスト | テストプロジェクトごとに `dotnet run --project` (例: `dotnet run --project server/tests/Pos.Server.UnitTests`)。`dotnet test` は使わない。[D-35](decisions.md#d-35-テストの実行方法) |
| 端末 | `terminal/Pos.Terminal.slnx` を VS で開いて Android エミュレータで実行、または `dotnet build -t:Run -f net10.0-android -p:AdbTarget="-s emulator-5554"`。エミュレータからサーバへは `10.0.2.2:8080`。設定 QR を管理画面 S-71 で表示して読み取る (Phase 5 / 6) |
| UI の言語 | 日本語固定。多言語化はしない ([D-28](decisions.md#d-28-ui-の言語-日本語固定)) |
| コーディング規約 | ルートの `AGENTS.md`: `.editorconfig` に従う、フィールドに `_` を付けない、警告ゼロ、新規テキストファイルは CRLF、「DTO」は使わない ([D-27](decisions.md#d-27-用語-dto-は使わない)) |

---

## 7. 実装計画

サーバを先に通してから端末に入る ([D-29](decisions.md#d-29-実装順序))。フェーズごとのチェックリストと完了条件は [implementation-plan.md](implementation-plan.md)。フェーズ単位で着手し、完了条件を満たしてから次へ進む。

| フェーズ | 内容 | 状態 |
| --- | --- | --- |
| Phase 0 土台 | ルート共通ファイル、`shared/` `server/` `terminal/` の骨組み、2 ソリューション | 完了 |
| Phase 1 `Pos.Domain` | 列挙型、計算 (税・値引按分・ポイント・返品)、業務ルール、単体テスト | 完了 |
| Phase 2 `Pos.Shared` | `XxxRequest` / `XxxResponse` 一式 | |
| Phase 3 サーバ DB | DDL、Entity、Accessor、起動時スキーマ作成、初期データ | |
| Phase 4 サーバ API | マスタ・同期 → 顧客 → シフト → 取引 → 在庫 → レポート → 帳票 (PDF)、統合テスト | |
| Phase 5 管理画面 | レイアウト・ダッシュボード → マスタ CRUD → 取引 / シフト / 在庫 / 顧客 / レポート → 設定・QR | |
| Phase 6 端末 | 土台・同期・Outbox → メニュー・開設 → 販売 → 会計・レシート → 精算・入出金 → 返品・履歴 → 照会・棚卸 → 設定 | |

後回しの項目は [api-design.md §7](api-design.md#7-phase-2-以降-後回し)。

---

## 8. 初期データ

起動時にテーブルが空なら投入する ([D-30](decisions.md#d-30-初期データの規模))。すべて日本語のサンプル。

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

取引・シフトのサンプルは投入しない (端末から作る)。レポート確認用に直近数日分を生成するオプションは任意。管理者アカウントは認証を入れる Phase 2 で追加する。

---

## 9. 実装時に確認する事項

設計時点で確定できず、実装の初期に小さな検証で潰すもの。

| # | 項目 | 確認方法 | 状態 / だめなときの代替 |
| --- | --- | --- | --- |
| 1 | JSON の camelCase | `Service-CloudManager` の `NamingPolicy` (camelCase) をそのまま使う。端末側は Rester の設定で camelCase にする (Phase 6) | サーバ側は Phase 0 で確認済み |
| 2 | `[TypeHandler(typeof(EnumTextConverter<T>))]` のジェネリック指定 | Phase 3 で 1 エンティティ試す | 列挙型ごとのコンバータクラス |
| 3 | `Guid` ↔ TEXT、`decimal` ↔ NUMERIC の読み書き | Phase 3 で INSERT → SELECT → `SUM` を試す | `Guid` は `[TypeHandler]` で文字列変換、金額列は TEXT + `CAST` |
| 4 | `groupBy=hour` のタイムゾーン | UTC の `TransactedAt` を店舗時刻へ。SQL (`datetime(TransactedAt, '+9 hours')`) か C# 側集計かを Phase 4 で決める | C# 側で集計 |
| 5 | MAUI ワークロード | Phase 0 で `Pos.Terminal` のビルドとエミュレータ実行を確認 | 確認済み |
| 6 | Aspire | Phase 0 で AppHost の起動 (ダッシュボード表示) を確認 | 確認済み (CLI 13.5.2 + AppHost SDK 13.5.3) |
| 7 | テストの実行 | Microsoft.Testing.Platform の実行ファイルとして `dotnet run --project` で実行する (`dotnet test` と `global.json` は使わない) | 確認済み ([D-35](decisions.md#d-35-テストの実行方法)) |
| 8 | レシート QR・電子レシート | QR の中身はレシート番号のみ、共有は画像 / テキスト、で Phase 6 に入る | — |
