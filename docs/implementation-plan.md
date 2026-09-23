# 実装プラン (チェックリスト)

フェーズ単位で着手し、完了条件を満たしてから次へ進む ([D-33](decisions.md#d-33-実装の進め方-フェーズ単位のチェックリスト))。  
設計は [architecture.md](architecture.md) / [api-design.md](api-design.md) / [db-design.md](db-design.md) / [screen-design.md](screen-design.md)。

## 進め方

- 着手するフェーズを指示してもらってから始める。  
  完了したら完了条件の確認結果を報告し、本書のチェックを更新する
- Phase 0〜7 (MVP) は完了。  
  後回しにしていた項目は [Phase 8 以降](#phase-8-以降-後回し項目の計画) に計画した
- 各フェーズの終わりは `dotnet build` 警告ゼロ・テスト緑・`jb inspectcode` (ReSharper。Jenkins と同じくソリューション全体の解析を有効にし、`--no-swea` は付けない) の指摘ゼロ
- 実装中に設計を変えた場合は [decisions.md](decisions.md) に追記し、該当文書を直してからフェーズを閉じる
- コミットはフェーズ (大きいものはサブフェーズ) 単位

---

## Phase 0: 土台 (完了)

サーバ・端末のソリューションがそれぞれ Visual Studio で開けて動く状態を作る。

### ルート

- [x] `.editorconfig` / `.gitattributes` / `.gitignore` / `Directory.Build.targets` / `Analyzers.ruleset` / `CodeCoverage.runsettings` を `Service-CloudManager` からコピー
- [x] `Directory.Build.props` をコピーし、`NoWarn` に MAUI 用の `NU1608` を含める (テンプレート間の唯一の差分)
- [x] Jenkins のパイプライン (サーバと端末を 1 つでビルド / 検査 / テスト / 公開。Jenkins 側の設定)。  
      テストは `dotnet run --project` で実行し、`global.json` は置かない ([D-35](decisions.md#d-35-テストの実行方法))
- [x] `AGENTS.md` を作成 (テンプレートの規約 + 本プロジェクト固有: 「DTO」不使用、Service / Usecase なし、camelCase、フォルダ構成)。  
      `CLAUDE.md` は `AGENTS.md` を参照
- [x] ルート `README.md` に構成と起動方法

### shared/

- [x] `shared/Pos.Domain` (net10.0、`Usa.Smart.Core`) の空プロジェクト
- [x] `shared/Pos.Contract` (net10.0、`Pos.Domain` 参照) の空プロジェクト
- [x] `shared/Pos.Domain.Tests` (xunit.v3 + Microsoft.Testing.Platform)。  
      `Pos.Domain` が UI / DB / HTTP / `Pos.Contract` を参照しないことを検証する `DependencyTests`

### server/

- [x] `Service-CloudManager` の `Core` / `Host` / `UnitTests` / `IntegrationTests` を骨組みとしてコピーし、`CloudManager` → `Pos.Server` にリネーム。  
      AWS / ジョブ / Services は含めない
- [x] Aspire AppHost (`Pos.Server.AppHost`) と OpenAPI (`Microsoft.AspNetCore.OpenApi` + NSwag の `/swagger`, `/redoc`) を `template-blazor-server` から追加
- [x] `Core`: `SqlHelper` と `Extensions` のみ (Accessor / Entity は Phase 3)
- [x] `Host`: `ApplicationExtensions` (camelCase JSON、ProblemDetails、Serilog、ヘルスチェック、MudBlazor)、レイアウト (`MainLayout` / `NavMenu` / `EmptyLayout` / `ReconnectModal`)、ページ (`Home` / `Error` / `NotFound`)、共通部品 (`ErrorBanner` / `ProgressOverlay` / `AppMessageBox`)、`GlobalExceptionHandler`、`DatabaseHealthCheck`、`LogSetting` / `ProfilerSetting`、`appsettings` (ポート 8080、`pos.db`)
- [x] `tests`: `NavMenuTests` (bUnit)、`SqlHelperTests`、`HostTests` (`/health`、`/`、`/unknown` → 404 ページ、`/api/unknown` → 404 のみ)
- [x] `server/Pos.Server.slnx`: AppHost / Core / Host / UnitTests / IntegrationTests + `../shared/` の 3 プロジェクト。  
      Solution Items はルートのファイルを `../` で参照

### terminal/

- [x] `template-maui-keyboard` の `Template.MobileApp` をコピーし `Pos.Terminal` にリネーム (フォルダ / csproj / 名前空間 / `ApplicationId` = `pos.terminal` / `ApplicationTitle` = `POS` / `MainActivity` 名 / Android の `label`)
- [x] 不要なものを除去: サンプル画面 (`Modules/Key`)、フォント (OpenSans / FluentUI と未使用の参照。`MaterialIcons` だけ残す)、`dotnet_bot.png`
- [x] `ViewId` を `Menu` のみにし、`Modules/Main/MenuView` (T-02。販売を最上段 2 列幅、機能未実装のため全ボタン無効) を作成。  
      起動時に `Menu` へ遷移
- [x] `Styles.xaml` に `FooterLabel` と POS 節 (`Pos` 接頭辞: 背景・行・区切り線・名称 / 金額ラベル・オプション / 数量 / 実行ボタン) を追加。  
      配色は `Colors.xaml` の Material パレット
- [x] `terminal/Pos.Terminal.slnx`: `Pos.Terminal` + `../shared/Pos.Domain` + `../shared/Pos.Contract`。  
      `Settings.XamlStyler` / `Pos.Terminal.sln.DotSettings` をコピー
- [x] 画面遷移の Forward / Back アニメーション: `AddHierarchyEffectPlugin` + 各画面の `[Hierarchy(n)]` ([D-36](decisions.md#d-36-端末の画面遷移アニメーション))
- [x] 通信の JSON を camelCase に設定 → Phase 6 で `HttpService` (HttpClient + System.Text.Json、[D-40](decisions.md#d-40-端末の通信-rester-ではなく-httpclient)) に実装

### 完了条件

- [x] `dotnet build server/Pos.Server.slnx` が警告ゼロで通り、3 つのテストプロジェクトが `dotnet run --project` で緑 (19 件)
- [x] `dotnet build terminal/Pos.Terminal.slnx` が警告ゼロで通る (MAUI ワークロード確認済み)
- [x] サーバ起動で `/health` = Healthy、`/` 200、`/swagger` 200、`/openapi/v1.json` に "POS API"、`/api/unknown` 404
- [x] Aspire AppHost から起動できる (ダッシュボード http://localhost:15000)
- [x] 端末をエミュレータ (Pixel 6a API 35) で起動してホームのメニューが表示される
- [ ] Visual Studio でそれぞれの `.slnx` を個別に開いて実行できる (利用者側で確認)

---

## Phase 1: `Pos.Domain` (完了)

計算ロジックと業務ルール。  
api-design §4 を実装し、テストで固定する。

- [x] 列挙型 (architecture §4.1 の一覧)
- [x] `global using Pos.Domain;` を `Pos.Contract` / `Pos.Server.Core` / `Pos.Server.Host` (`GlobalUsing.cs`、`_Imports.razor`) に追加 (Phase 0 では空の名前空間のため外してある)
- [x] `SalesInput` / `SalesResult` (明細・値引・支払・会社設定 → 計算項目)。  
      Request / Response には依存しない (record)
- [x] `Allocation`: 最大剰余法の按分 (同値は順序で決定)
- [x] `Rounding`: `Floor` / `Round` (四捨五入) / `Ceiling`
- [x] `SalesCalculator`: 明細金額 → 明細値引 → 取引値引の按分 → 税 (税率 × 内税/外税グループ、`TaxCalculator`) → ポイント (利用按分・付与) → 合計・預り・釣銭
- [x] `ReturnCalculator`: 元明細 (`ReturnInput.OriginalLines`) からの返品明細導出 (api-design §4.5)
- [x] `TransactionRules`: api-design §3.12 の業務ルール (Sale / Return / Void) を検証し `ErrorCode` / `WarningCode` を返す。  
      事実は `SaleContext` / `ReturnContext` / `VoidContext` で受け取り、再計算結果を `Expected` に返す。  
      `SalesResultComparer` で一致判定
- [x] `Pos.Domain.Tests` (73 件):
  - [x] api-design §4.6 の例 (全数値が一致)
  - [x] 内税 / 外税の混在、税率複数
  - [x] ポイント基準 `TaxIncluded` / `TaxExcluded` (外税明細を含む)
  - [x] 返品 (一部 / 全数。全数で元取引と同額)
  - [x] 按分の端数 (最大剰余法の割り当て順)
  - [x] 丸め 3 種
  - [x] エラー: 明細値引超過、支払合計不一致、釣銭不正、売価変更不可、返品数量超過 (+ シフト・レシート番号・商品・会員・元取引・取消の各ルール、警告、エラーコード文字列)
- [x] 仕様の補足を api-design に反映: 取引値引は Σ base を超えない、付与ポイントは 0 が下限、返品の明細金額は Floor

### 完了条件

- [x] テスト緑 (Pos.Domain.Tests 73 / UnitTests 14 / IntegrationTests 4)、両ソリューション警告ゼロ
- [x] `Pos.Domain` が UI / DB / HTTP / `Pos.Contract` に依存していない (`DependencyTests`)

---

## Phase 2: `Pos.Contract` (完了)

api-design §3 の通信データ。

- [x] `Common/`: `ListResponse<T>`、`ProblemResponse`、`JsonDateTimeConverter` (エラーコード定数は `Pos.Domain` の `ErrorCode.ToCode()` で代替)
- [x] マスタ: Settings / Store / Terminal / Staff / Category / TaxRate / Product / Discount / PaymentMethod / AdjustmentReason の `XxxResponse` / `XxxCreateRequest` / `XxxUpdateRequest` / `XxxListResponse`、`SyncMastersResponse`
- [x] 顧客: `CustomerXxx`、`CustomerPointHistoryResponse` / `PointHistoryListResponse`、`CustomerPointAdjustRequest`
- [x] 取引: `TransactionCreateRequest` / `TransactionResponse` (+ `TransactionCreateRequestLine` / `TransactionResponseLine`、値引・税集計・支払・配送・取消情報・警告)、`TransactionListResponse`、`TransactionVoidRequest`、`TransactionCalculateRequest`、`TransactionCalculateResponse`
- [x] シフト: `ShiftOpenRequest`、`ShiftResponse` / `ShiftListResponse`、`ShiftCashEventRequest` / `ShiftCashEventResponse` / `CashEventListResponse`、`ShiftCloseRequest`、`ShiftSummaryResponse`
- [x] 在庫: `InventoryLevelResponse` / `InventoryLevelListResponse`、`InventoryProductResponse`、`InventoryChangeRequest` / `InventoryChangeResultResponse`、`InventoryChangeResponse` / `InventoryChangeListResponse`
- [x] レポート: `ReportSalesSummaryResponse`、`ReportProductSalesResponse`
- [x] 検証属性 (`Required` / `MaxLength` / `Range`) をテンプレートと同じ書き方で付与
- [x] サーバの JSON 設定に `JsonDateTimeConverter` / `JsonStringEnumConverter` を登録し、`JsonContractTests` (統合テスト) で形式を固定
- [x] CA1716 / CA1056 は `Pos.Contract` の `GlobalSuppressions.cs` で抑止 ([D-38](decisions.md#d-38-警告の抑止))

### 完了条件

- [x] 両ソリューションでビルドが通る (警告ゼロ)
- [x] api-design §3 の全フィールドと名称・型が一致している (突き合わせ済み。差分は api-design に反映: `payments[].note`、`warnings[]`、`calculate` の応答型、端末 / 商品の Request に含めない項目)

---

## Phase 3: サーバ DB (完了)

db-design の DDL・Entity・Accessor・初期データ。

- [x] `EnumTextConverter<T>` と技術検証: 列挙型 TEXT、`Guid` TEXT、`decimal` NUMERIC (INSERT → SELECT → `SUM`)、`DateOnly` / `DateTime` (UTC) を `DatabaseTests` で確認。  
      コンバータは `DataProfile` に一括宣言
- [x] Entity 一式 (db-design §3、24 クラス)
- [x] `Create.sql` を Accessor ごとに分割して全テーブル・インデックス (部分ユニークインデックス含む) を作成。  
      起動時に PRAGMA (WAL / busy_timeout)、外部キーは接続文字列 `Foreign Keys=True`
- [x] Accessor (16 クラス、SQL 103 件):
  - [x] マスタ各種: 一覧 (updatedSince / includeDeleted / page / size / sort)、取得、登録、更新 (version 楽観ロック)、論理削除、`lookup` (商品: barcode / code)、削除可否の件数
  - [x] 顧客: 検索、lookup、CRUD、ポイント履歴、残高更新 (`RETURNING` で処理後残高)
  - [x] 取引: 一式の INSERT (`DbTransaction` 付き)、取得 (明細・値引・税・支払・配送・シリアル)、一覧、lookup、取消、`ReturnedQuantity` 更新
  - [x] シフト: 開設、current、一覧、取得、精算、入出金、集計 (支払方法別・税率別・部門別・ポイント)
  - [x] 在庫: 現在庫一覧、商品別全店、UPSERT 加減算 (`RETURNING`)、変動履歴
  - [x] レポート: 売上集計 (day / terminal / staff は生 SQL の GROUP BY、hour / paymentMethod / taxRate / category は専用クエリ)、商品別
  - [x] 設定: 取得・更新
- [x] 起動時: PRAGMA → スキーマ作成 → 会社設定がなければ初期データ投入 (`Host/Infrastructure/Data/InitialData.cs`、architecture §8。固定 ID)
- [x] `DatabaseHealthCheck` を `SELECT COUNT(*) FROM Settings` に変更

### 完了条件

- [x] 起動で `pos.db` (実行ディレクトリ) が作られ、初期データが入る。  
      `/health` = Healthy
- [x] 技術検証テスト (`DatabaseTests`) と Accessor のテスト (`AccessorTests`: 楽観ロック / 一覧フィルタ / 開設 → 販売 → 集計 → 返品数量 → 取消 → 精算 → レポート) が緑 (統合テスト 15 件、テンプレートと同じく `TestApplicationFactory` の一時 DB)

---

## Phase 4: サーバ API (完了)

api-design §3 のエンドポイント。  
順番はマスタ → 顧客 → シフト → 取引 → 在庫 → レポート。

### 4a マスタ・設定・同期

- [x] `Mappers` (Smart.Mapper の `[Mapper]`): `MasterMapper` (マスタ・顧客)、`TransactionMapper` (取引一式、`SalesInput` / `ReturnInput` への変換、計算結果 ↔ `TransactionCalculateResponse`)、`ShiftMapper` (集計付き応答、`ExpectedCash`)、`InventoryMapper`、`ReportMapper` (`GroupKey → Key` は `[MapProperty]`)
- [x] Settings / Stores / Terminals / Staff / Categories / TaxRates / Products (+ `lookup`) / Discounts / PaymentMethods / AdjustmentReasons (`Endpoints/XxxEndpoints.cs`、静的クラス + `MapGroup`)。  
      Problem Details は `Infrastructure/Api/ApiProblems.cs` (`errorCode` / `errors` / `expected`)、`AddValidation` の 400 にも `VALIDATION_ERROR` を付ける
- [x] `GET /sync/masters?since` (変更がなければ `settings` は省略、`products` は `MaxPageSize` 超で `productsTruncated`)
- [x] 一覧の並び替え許可列 (`ApiHelper.ResolveSort`: `updatedSince` 指定時は `UpdatedAt, Id` 固定)。  
      `GET /{id}` は論理削除済みも `isDeleted: true` で返し、更新・削除は 404

### 4b 顧客・ポイント

- [x] 検索 / lookup / 登録 / 更新 / 論理削除
- [x] ポイント履歴、手動調整 (1 トランザクションで残高更新 + `Adjust` 履歴、応答は `CustomerPointHistoryResponse`)、購入履歴

### 4c シフト

- [x] 開設 (`TERMINAL_HAS_OPEN_SHIFT` は部分ユニークインデックスの違反で判定)、current、一覧、詳細 (Open は都度集計 + `expectedCash`、Closed は確定値)
- [x] 入出金 (Open のみ、同一 id は 200)、精算 (集計確定 + 金種、同じ実査額の再送は 200)、summary

### 4d 取引

- [x] `POST /transactions/calculate` (`TransactionRules.ValidateInput` で入力を検証してから計算。登録しない)
- [x] `POST /transactions` (Sale): 冪等 (同一 id → 200 / 相違 → 409)、`TransactionRules.ValidateSale` による検証、1 トランザクションでの副作用 (在庫 `trackInventory` 分の加減算 + 変動履歴、ポイント Redeem → Earn と `pointsBalanceAfter`、`LastReceiptSeq`)
- [x] `POST /transactions` (Return): 元取引検証、`ReturnCalculator` との一致、`ReturnedQuantity` (超過は `RETURN_QUANTITY_EXCEEDED` でロールバック)、ポイント Refund → Revoke
- [x] `POST /transactions/{id}/void` (`ValidateVoid`、在庫の逆方向履歴、ポイント `Void` 履歴、返品取消は元明細の返品数量を戻す)
- [x] 一覧 / 詳細 / lookup
- [x] 警告 (`warnings[]`) と ProblemDetails の `errorCode` / `expected`

### 4e 在庫

- [x] 現在庫一覧 (`updatedSince` 指定時は `updatedAt` 順、通常は商品コード順)、商品別全店在庫
- [x] `POST /inventory/changes` (PhysicalCount / Adjustment、要素ごとに 1 トランザクション、同一 id は Duplicate)、変動履歴、調整理由の CRUD

### 4f レポート

- [x] `summary` (groupBy 7 種。合計行は各行の合算、不正な groupBy は 400)、`products` (`sort=netSales|quantity`)。  
      `from` / `to` 省略時は当日と 30 日前
- [x] `hour` のタイムゾーン処理: 店舗の `TimeZone` を `TimeZoneInfo` で解決し、SQLite の `+NNN minutes` 修飾子で SQL 側集計

### 4g 帳票 (PDF、[D-37](decisions.md#d-37-帳票出力-pdf-oysterreport))

- [x] `OysterReport` 1.9.0 の導入: パッケージ、`Assets/Fonts/ipaexg.ttf`、`EmbeddedFontResolver` (`template-blazor-server` から)、`Assets/**` を出力ディレクトリへコピー
- [x] テンプレート `Assets/Reports/ShiftReport.xlsx` (精算レポート) / `DailySalesReport.xlsx` (売上日報)。  
      A4 縦 1 シート、明細行はプレースホルダの行を件数分に複製 (`ReportText.FillRows`)
- [x] `ShiftReportBuilder` + `GET /shifts/{id}/summary/pdf`、`DailySalesReportBuilder` + `GET /reports/sales/daily/pdf?storeId&date` (シングルトン、`byte[] Build(...)`、日時は店舗のタイムゾーン)
- [x] 統合テスト: `application/pdf` で先頭が `%PDF` のレスポンス、対象なしは 404 (生成した PDF は `TestResults/` に保存して目視確認)

### テスト

- [x] 統合テストのシナリオ (`ApiTransactionFlowTests`): 開設 → 再送 200 / 再開設 409 → calculate (api-design §4.6 の値) → 販売 → 同一 id 再送 200 / 相違 409 / 計算違い 422 (`expected`) → ポイント・在庫・連番 → 返品 (§4.5 の値) → 超過 422 → 取消 (返品済みは `HAS_RETURNS`、返品の取消で戻る) → 入出金 → 精算 → 精算後は `SHIFT_CLOSED` → summary / レポート 7 種 / PDF の整合。  
      棚卸・調整の冪等性は `InventoryChangesAreIdempotent`
- [x] 差分同期 (`sync/masters` の全件 → 差分空)、楽観ロック 409、重複コード 409、使用中 422、入力検証 400 (`ApiMasterTests`)
- [x] OpenAPI ドキュメント (`/openapi/v1.json`) にエンドポイントが載ることを `HostTests` で確認

### 完了条件

- [x] 統合テスト緑 (24 件。単体 14 件・Domain 73 件と合わせて 111 件)、警告ゼロ、InspectCode の指摘ゼロ

---

## Phase 5: 管理画面 (完了)

screen-design §2 の ★ 画面。  
ページは Accessor / Domain を直接使い、API と同じ処理は静的ヘルパー (`SalesSummaryQuery` / `InventoryChangeApplier`) で共用する。  
状態は絵文字付きチップとバッジで示す ([D-39](decisions.md#d-39-管理画面の表現-絵文字チップバッジ))。

### 5a 骨組み

- [x] `MainLayout` / `NavMenu` (`MudNavGroup` のグループ構成。現在の URL のグループを開く)
- [x] 店舗フィルタの共有状態 (`StoreFilterState`、scoped) と `StoreSelect` コントロール
- [x] ダッシュボード S-01 (本日の KPI カード、店舗別売上 (`groupBy=store` を追加)、開設中シフト、端末の通信状態 (5 分以内は 🟢)、在庫マイナス・残高マイナスの警告)
- [x] 共通基盤: `PageComponentBase` (読み込み / 実行 / エラー表示 / 確認 / 編集ダイアログ呼び出し、コード重複は `IDialect.IsDuplicate` で判定)、`EditDialogBase<TForm>`、`FormValidator<T>`、`FormMapper` (Entity ↔ Form)、`DisplayText` / `ChipText` / `StatusChip`、`NameLookup` (ID → 名称)

### 5b マスタ

- [x] 商品 S-50 / S-51 (`MudDataGrid` の `ServerData`、部門 / キーワード / 販売状態 / 削除済みの絞り込み、CSV は `GET /products/csv`)
- [x] 部門 S-53 (`MudTreeView` 2 階層、商品数表示)、税率 S-54 (既定は 1 件)、値引 S-55、支払方法 S-56 (`Points` は有効 1 件の制約)、調整理由 S-44
- [x] 店舗 S-70、レジ端末 S-71 (設定 QR は `TerminalQrDialog`、QRCoder で `ApiEndPoint` / `StoreId` / `TerminalId`)、スタッフ S-72
- [x] 会社設定 S-80

### 5c 取引・精算

- [x] 取引一覧 S-20 (期間・店舗・端末・種別・状態、レシート番号は完全一致。`?id=` で詳細を開き、`?shiftId=` で絞り込み) / 詳細 S-21 (明細・シリアル・値引・税率別・支払・配送先・取消情報・関連取引の切り替え)
- [x] シフト一覧 S-30 (Open は取引から都度集計、過不足はチップ) / 詳細 S-31 (精算レポート + 入出金 + 金種、[取引一覧] [精算レポート PDF])

### 5d 在庫

- [x] 現在庫 S-40 (`InventoryLevelDetail` の一覧、マイナスのみ) / 商品別全店 S-41、変動履歴 S-42 (`?productId=`)、棚卸・調整登録 S-43 (`MudAutocomplete` で商品検索、適用は `InventoryChangeApplier` を API と共用)

### 5e 顧客

- [x] 顧客一覧 S-60 (行クリックで詳細ページ) / 詳細 S-61 (タブにバッジ、ポイント履歴・購入履歴、編集・削除) / ポイント調整 S-62 (API と同じ `Adjust` 履歴 + 残高更新)

### 5f レポート

- [x] 売上集計 S-10 (`MudChart` 棒グラフ、合計行、CSV `GET /reports/sales/summary/csv`、[売上日報 PDF] は店舗と営業日を選んで開く)、商品別売上 S-11 (上位 3 位はメダル、CSV `GET /reports/sales/products/csv`)

### 完了条件

- [x] ★ 画面がすべて動く (API で開設 → 販売 → 返品 → 入出金 → 精算を作り、ブラウザでダッシュボードから各画面・ダイアログを確認)。  
      編集ダイアログの検証・楽観ロック (`VERSION_MISMATCH`)・削除確認・使用中エラーが機能する
- [x] bUnit テスト (`NavMenu` のリンク、`StatusChip`) と `FormMapper` の単体テスト (単体 19 件、統合 24 件、Domain 73 件)。  
      警告ゼロ、InspectCode の指摘ゼロ

---

## Phase 6: 端末 (完了)

screen-design §1 の ★ 画面。  
サーバが動いている前提。  
ViewModel は `DataAccessor` / `HttpService` / `Pos.Domain` を直接使い、画面をまたぐ処理は静的ヘルパー (`TransactionBuilder` / `TransactionUsecase` / `ShiftSummaryBuilder` / `ReceiptTextBuilder` / `ReceiptImageBuilder`) に置く。

### 6a 土台

- [x] ローカル DB (`Services/DataAccessor.cs` + `Sql/*.sql`、db-design §6): マスタは `Pos.Contract` の Response をそのままエンティティにし Id で削除 → 挿入、取引は `LocalTransactionEntity` (検索列 + `TransactionResponse` の JSON)、`Outbox` / `SyncState` / `HoldCarts`。  
      日時は ticks、列挙型は文字列 (`Services/DataProfile`)
- [x] `HttpService` (HttpClient + `System.Text.Json`。camelCase / null 省略 / 列挙型は文字列 / `JsonDateTimeConverter`。失敗時は Problem Details を `ApiResult<T>` で返す、[D-40](decisions.md#d-40-端末の通信-rester-ではなく-httpclient))、`NetworkService` (接続確認・インジケータ・通知)
- [x] `SyncService`: 15 秒周期 + `Trigger()`、マスタ差分同期 (5 分ごと、商品が省かれたらページ取得、自店在庫は `updatedSince`)、Outbox を発生順に送信 (4xx は要確認 `Failed` で停止、5xx / 通信不可は指数バックオフ)、レシート番号の採番 (`SyncState` の連番、サーバの `LastReceiptSeq` と合わせる)
- [x] `Settings` (IPreferences: ApiEndPoint / StoreId / TerminalId / OpenSalesAfterLogin)、`Session` (会社設定・店舗・端末・担当・シフト・未送信件数。タイトルバーの `店舗-端末 担当` と未送信バッジは `MainPageViewModel` が `Session` を写す)、`SalesState` (カート・支払・完了取引・返品元)、`StockState`
- [x] セットアップ T-00 (設定 QR は `SettingParser` 互換、手入力、店舗・端末をサーバで確認してから全件同期)、スタッフ選択 T-01 (役割チップ)
- [x] 各画面の `[Hierarchy(n)]` (screen-design §1.3 の深さ)。  
      シェルの更新は表示中の View だけが行う (`ShellProperty.Active`。遷移で外れた View のバインディング解除で上書きされないように)

### 6b メニュー・開設・設定

- [x] メニュー T-02 (シフト状態チップ、未開設なら販売 / 返品 / 入出金は開設へ誘導し開設後に元の画面へ、開設中は「レジ開設」が「精算」に変わる)
- [x] レジ開設 T-03 (サーバに開設中のシフトが残っていれば引き継ぐ)
- [x] 設定・同期 T-90 (端末情報、最終同期、未送信一覧: 要確認は再送 / 破棄 / 詳細、手動同期、「ログイン後に販売画面を開く」、スタッフ切替、接続設定のやり直し)

### 6c 販売

- [x] `Cart` (`Models/Cart`): 同じ商品は数量 +1、`TransactionBuilder.ToSalesInput` で `SalesCalculator` の入力へ。  
      保留は JSON で `HoldCarts` に保存
- [x] 販売 T-10 (会員チップ、明細タップで P-13、左スワイプで削除、[⋯] = 取引値引 / 配送先 / 保留 / 呼出 / クリア)、スキャン T-11 (商品モードは連続読み取り、同一コードは 2 秒抑制、他モードは 1 件で呼び出し元へ。手入力あり)、商品検索 T-12 (キーワード + 部門 2 階層)
- [x] 明細編集 P-13 (数量・単価は `IDialog` の Prompt、明細値引は `DiscountChooser`、シリアル番号・備考・削除)、会員選択 T-14 (検索 / スキャン / 新規 / 解除)、取引値引 P-15 (定義済み + 任意額 / 任意率 + 理由、承認が必要な値引は承認者を選ぶ)、配送先 T-16 (会員住所の転記)、保留 T-17

### 6d 会計・レシート

- [x] 会計 T-20 (埋め込みテンキー + 金額ショートカット + ちょうど、支払方法ボタン、複数支払、`RequiresReference` は伝票番号、ポイント利用は残高と残りまで、お釣り表示、確定で `TransactionUsecase` がローカル取引 + Outbox + 自店在庫を 1 トランザクションで書く)
- [x] 会計完了 T-21 (お釣り / 返金額、ポイント、次へ)、レシート T-22 (`ReceiptTextBuilder` の 32 桁テキストを `ReceiptImageBuilder` (SkiaSharp) で桁位置描画した画像、電子レシート QR (レシート番号)、共有は PNG)

### 6e 精算・入出金

- [x] 入出金 T-50 (入金 / 出金 / ドロワ開)、精算 T-51 (`ShiftSummaryBuilder` でローカルの取引・入出金から予想現金、未送信警告、実査は `InputNumber` または金種別入力ポップアップ、過不足)、精算レポート T-52 (未送信がなければ `GET /shifts/{id}/summary`、あれば端末の集計。共有はテキスト)

### 6f 返品・履歴

- [x] 返品 T-40 (レシート QR / 番号入力 / 履歴、オンラインならサーバの最新を使う) / 返品明細選択 T-41 (残数量まで、全数、理由は `ReasonSelect`) / 返金 T-42 (ポイント返還は自動、残りは元の支払方法を先頭に選択。`ReturnCalculator` → `TransactionBuilder.ToReturnRequest`)
- [x] 取引履歴 T-30 (期間: 本シフト / 本日 / 昨日 / すべて、種別、送信状態チップ) / 取引詳細 T-31 (明細・金額・支払・ポイント・配送先・取消情報。取消は同一シフト内、返品は完了した販売のみ)

### 6g 照会・棚卸・売上

- [x] 商品・在庫照会 T-60 (スキャン / 検索、自店在庫、他店在庫はオンライン)、会員照会 T-61 (基本情報・ポイント履歴・購入履歴) / 登録・編集 T-62 (スキャン中の入力内容は `NavigationParameter` の state で引き継ぐ)
- [x] 棚卸・在庫調整 T-70 (モード切替、調整は増減 + 理由 (調整理由マスタ + 任意)、送信で `InventoryChangeRequest` を Outbox へ、ローカル在庫も更新)、売上照会 T-80 (期間 / 範囲 (自端末 / 自店 / 全店) / 集計軸。合計は日別集計の Total)

### 完了条件

- [x] エミュレータで「セットアップ → 開設 → 販売 (検索・手入力スキャン・会員・明細値引 (承認者) ・取引値引) → 会計 (ポイント + 現金) → レシート → 返品 → 入出金 → 棚卸 → 精算 (金種入力) → 精算レポート」が通り、サーバ側の取引・在庫・ポイント・シフトに反映されることを確認。  
      取消・保留 / 呼出・配送先・会員編集も確認
- [x] 機内モードで販売 → 復帰で Outbox が送信される。  
      サーバ側で `AllowsPriceOverride` を外して `422 PRICE_OVERRIDE_NOT_ALLOWED` を起こすと要確認 (赤バッジ) で止まり、T-90 で理由を確認して再送 / 破棄できる
- [x] 警告ゼロ、InspectCode の指摘ゼロ

---

## Phase 7: 仕上げ (完了)

- [x] ルート `README.md` に両方の起動手順とスクリーンショット (`docs/images/`: 端末 8 枚、管理画面 3 枚)
- [x] 実装で変わった点を docs に反映 (decisions.md / 各設計)。  
      各フェーズの完了時に反映済み。  
      Phase 7 では architecture §2 / §6 (サンプル取引ツール)、[D-41](decisions.md#d-41-サンプル取引の生成-api-経由のコンソールツール)
- [x] レポート確認用のサンプル取引生成: `server/tools/Pos.Server.SampleData` (コンソール。起動中のサーバに対して API で直近 N 日分のシフト・販売・返品・取消・入出金・精算を登録する。`--base` / `--days` / `--per-day` / `--seed`)
- [x] 端末の細部: 会計完了の文言を 1 行に、精算・精算レポート・取引詳細の差し引き項目は 0 のとき符号を付けない (`DisplayText.MinusYen`)

### 完了条件

- [x] 空の DB に対してツールを実行し、ダッシュボード・売上集計・取引一覧・精算一覧・在庫 (マイナス在庫の要確認を含む) に反映されることを確認
- [x] 警告ゼロ、InspectCode の指摘ゼロ、テスト green

---

## ソースの見直し (完了)

実装後のソースレビュー (利用者指摘) への対応。  
判断は [D-45](decisions.md#d-45-サーバの-service-層) / [D-46](decisions.md#d-46-端末の-service--usecase-とナビゲーションのコンテキスト) / [D-47](decisions.md#d-47-通信データと名前空間の命名)。

- [x] 共有: `Pos.Shared` → `Pos.Contract`、一覧 `XxxResponse` / 要素 `XxxResponseItem`、`ListResponse` は直下、`JsonDateTimeConverter` / `ProblemResponse` は各側で定義、`Pos.Domain` は `Enums` / `Logic` 名前空間
- [x] サーバ: `Services/` (Service 層、`DataWriteStatus`)、`MasterAccessor`、`Models/Views` / `Models/Parameters`、`DataProfile` / `SqlHelper` を `Accessors` へ、Endpoints は `[Mapper]` + Service 呼び出しだけ、`ViewHelper` / `ViewExtensions`、`Application` / `Infrastructure` の整理
- [x] 端末: `Input` 名前空間の削除、`SalesContext` / `ReturnContext` / `StockContext`、`Services/` の Usecase / Service / Builder、`Models/Cart`、`Modules/Dialogs`、Converter への置き換え、`PostForwardAsync` / `PostActionAsync`、`DataAccessor` の組み込み属性と SQL の整形
- [x] 2 回目のレビュー ([D-48](decisions.md#d-48-サーバの見直し-並び順の列挙型returning初期データの-sql)、[D-49](decisions.md#d-49-端末の見直し-scope-プラグイン入力の種類ごとの電卓ヘルパーの置き場所)):
      共有は `Pos.Domain.Length`、Request / Response をエンドポイント名に合わせて改名。  
      サーバは並び順の列挙型と SQL 側の展開、`[Name]`、`RETURNING`、`Assets/Data/InitialData.sql` (`[DirectSql]` で実行)、`XxxView`、Host の置き場所の整理、`EnumHelper` / `ReportPeriodQuery` / `ExportUrls`。  
      端末は Scope プラグイン、`Usecases/`、`ViewHelper`、入力の種類ごとの電卓、理由の定型選択
- [x] 全体: ソースから `§` と設計文書への参照を除く
- [x] サーバの SQL ファイルも端末と同じ書き方 (`SELECT` / `FROM` / `WHERE` / `ORDER BY` を行頭) に揃える (長い `OR` 条件の分割、精算集計のスカラー副問い合わせを派生表の結合に)

---

## Phase 8 以降: 後回し項目の計画

MVP (Phase 0〜7) で後回しにした項目を機能単位のフェーズに分けた。  
設計文書には実装済みの内容だけを書き、後回しの項目は本書で管理する。  
順序の判断は [D-42](decisions.md#d-42-後回し項目の実装順序-phase-8-以降)。

| フェーズ | 内容 | 主な対象 | 規模 |
| --- | --- | --- | --- |
| Phase 8 | 認証・端末登録 (管理画面ログイン、端末ペアリング、PIN ログイン、役割による認可) | サーバ + 管理画面 + 端末 | 大 |
| Phase 9 | 日次締め | サーバ + 管理画面 | 小 |
| Phase 10 | 受注 (取り寄せ・取り置き) | サーバ + 管理画面 + 端末 | 中 |
| Phase 11 | 商品画像・CSV 取込 | サーバ + 管理画面 (+ 端末の画像表示) | 小 |
| Phase 12 | レシート・帳票・検索の拡張 (レシート PDF、端末の印刷、シリアル検索、一括送信) | サーバ + 管理画面 + 端末 | 小 |
| Phase 13 | 通知 (SignalR によるマスタ更新通知、管理画面の自動更新) | サーバ + 管理画面 + 端末 | 中 |
| Phase 14 | 在庫移動・入荷 (仕入先、入荷、店舗間移動) | サーバ + 管理画面 + 端末 | 中 |

各フェーズ共通の進め方 (Phase 0〜7 と同じ):

- 着手時にまず設計を詳細化する: api-design (エンドポイント・Request / Response・エラーコード)、db-design (テーブル定義・DDL)、screen-design (画面・遷移) の該当節を追加し、判断は decisions.md に `D-4x` として記録する。  
  本書のチェックリストは詳細化で変わりうる
- 既存テーブルへの列追加は避け、新しいテーブルで持つ (起動時の `CREATE TABLE IF NOT EXISTS` だけで済ませる)。  
  避けられない場合は `DatabaseAccessor` に `PRAGMA table_info` で列を確認して `ALTER TABLE ADD COLUMN` する仕組みを入れる。  
  端末のローカル DB は `SyncState` にスキーマ版を持ち、違えばマスタ表を作り直して全件同期する
- 完了条件は共通: 該当画面 / 端末の流れを実機 (エミュレータ) とブラウザで確認、統合テストの追加、`dotnet build` 警告ゼロ、テスト緑、`jb inspectcode` の指摘ゼロ、docs (api / db / screen / architecture / README) を実装に合わせる
- 引き続き後回し (本計画の対象外): 外部向け Webhook、Bluetooth レシートプリンタ、受注の前受金 (内金)、発注 (仕入先への注文)、管理画面の MFA / パスキー

---

## Phase 8: 認証・端末登録

[D-09](decisions.md#d-09-認証端末登録-後回し) で後回しにした認証を入れる。  
`template-maui-server` の 2 スキーム構成 (管理画面 = Cookie、API = Bearer) を土台にする。  
方針 (着手時に D-43 として記録):

- **管理画面**: `Accounts` テーブル (テンプレートの `AccountEntity` + `IPasswordProvider`) による Cookie ログイン。  
  役割は `Administrator` (すべて) / `Operator` (参照と、取引・在庫・顧客の操作。マスタ・設定・ユーザー・端末登録は不可)
- **端末**: 管理画面で発行するペアリングコードで `POST /terminals/pair` を呼び、**端末トークン** (ランダム値、サーバはハッシュを `TerminalTokens` に保存) を受け取って `SecureStorage` に持つ。  
  以後の API は `Authorization: Bearer` で呼ぶ。  
  JWT ではなく DB 照合にする (管理画面からの即時失効、署名鍵の運用が不要、端末は数か月単位で動き続ける)
- **スタッフ**: PIN は端末でローカル検証する (オフラインでもログインできるように)。  
  `Staff.PinHash` (PBKDF2 + salt、`Pos.Domain` の `PinHasher` を端末とサーバで共用) を端末向けの同期応答にだけ含める。  
  サーバ向けの `POST /auth/login` (スタッフ JWT) は置かず、端末の要求は端末トークンで認証し、担当は本文の `staffId` (サーバは有効・所属・役割を検証する)
- **認可**: 端末の要求は本文 / クエリの `storeId` / `terminalId` がトークンのクレームと一致すること (`TERMINAL_MISMATCH` 403)。  
  役割: 承認が必要な値引の承認者と取消の承認者は Manager 以上 (`APPROVAL_REQUIRED` 422)。  
  管理画面のマスタ・設定・ユーザー・端末登録は Administrator
- **開発・デモ**: `Auth:Enabled` (既定 `true`)。  
  `false` なら認可ポリシーを素通しにし、端末の一致検証も省く (`template-web-mvc` の「認証なしで使える」と同じ考え。テストと SampleData ツールは両方で通す)

### 8a 管理画面ログイン (サーバ)

- [ ] `Core`: `AccountEntity` / `AccountAccessor` (`Create` / 一覧 / 名前検索 / 追加 / 更新 / 削除)、`Infrastructure/Security` の `IPasswordProvider` + `DefaultPasswordProvider` (PBKDF2) を `template-maui-server` から移植。  
      `Accounts` は db-design §3 に追加 (§7 の「テンプレート既存」は誤り。ベースの `Service-CloudManager` に認証はない)
- [ ] `Host`: `AuthSetting` (`Enabled` / `ExpireMinutes` / `InitialName` / `InitialPassword`)、`ConfigureAuthentication` (Cookie: `LoginPath=/login`、`/api` 配下は 401 / 403 を返す)、`/auth/login` `/auth/logout` (form POST)、`Login.razor` + `LoginLayout` + `RedirectToLogin`、`Routes.razor` を `AuthorizeRouteView` に、`NavMenu` にユーザー名とログアウト。  
      起動時に `Accounts` が空なら初期アカウント (`appsettings` の `Auth:InitialName` / `InitialPassword`、既定 `admin` / `admin`) を投入
- [ ] ユーザー S-92 (`/accounts`、ナビ「設定 › ユーザー」): 一覧、追加、パスワード変更、役割、無効化、削除 (自分自身は不可)
- [ ] 全ページに `[Authorize]`。  
      マスタ (商品 / 部門 / 税率 / 値引 / 支払方法 / 調整理由 / 店舗 / 端末 / スタッフ) の編集・削除、会社設定、ユーザー、端末登録は `Policies.Administrator` (Operator にはボタンを出さない + `AuthorizeView`)

### 8b 端末登録 (サーバ)

- [ ] `TerminalTokens` テーブル (`Id`, `TerminalId`, `PairingCode`, `PairingExpiresAt`, `TokenHash`, `DeviceName`, `PairedAt`, `RevokedAt`, `CreatedAt`)。  
      端末ごとに有効なトークンは 1 つ (再ペアリングで旧トークンは失効)
- [ ] S-71 レジ端末: [ペアリングコード発行] (6 桁、10 分有効。ダイアログにコードと設定 QR (`ApiEndPoint` + `PairingCode`、[D-24](decisions.md#d-24-端末セットアップ-qr-テンプレート互換フォーマット) の形式に項目追加) を表示)、[登録の解除]、一覧に登録状態チップ (未登録 / 登録済み (端末名・日時) / 解除)
- [ ] `POST /terminals/pair` (匿名): `{ pairingCode, deviceName, appVersion }` → `{ token, terminal, store }`。  
      不一致・期限切れ・使用済みは 422 `PAIRING_CODE_INVALID`。  
      成功でコードを消費し `LastSeenAt` / `AppVersion` を更新
- [ ] `POST /terminals/me/heartbeat` (端末): `{ appVersion }` → `LastSeenAt` / `AppVersion`。  
      現状は取引登録時にしか `LastSeenAt` が動かないため、ダッシュボードの通信状態をこれで出す
- [ ] `TerminalTokenAuthenticationHandler` (スキーム `Terminal`): Bearer → SHA-256 → `TerminalTokens` 照合 → クレーム `terminalId` / `storeId` / 役割 `Terminal`。  
      失効済み・不明は 401。  
      照合結果は短時間 (1 分) キャッシュ
- [ ] OpenAPI に Bearer のセキュリティスキームを載せ、Swagger UI から試せるようにする (管理向けは Cookie のままブラウザで通る)

### 8c 認可 (サーバ)

- [ ] ポリシー: `Api` (Cookie または Terminal)、`Admin` (Cookie)、`Administrator` (Cookie + 役割)。  
      `/api/v1` 全体に `Api`、api-design の用途が「管理」だけの endpoint は `Admin`、マスタ・会社設定の書き込みは `Administrator`。  
      api-design §2 に「認証」節を追加し、各表に列を足す
- [ ] 端末クレームとの一致検証 (`TERMINAL_MISMATCH` 403): `sync/*` (自店在庫)、`shifts` (開設・入出金・精算・current)、`transactions` (登録・取消)、`inventory/changes`、`terminals/me/*`。  
      Cookie の要求には適用しない
- [ ] 役割検証: `requiresApproval` の値引は `approvedByStaffId` が必須で Manager 以上 (現状は保存するだけで検証していない)。  
      取消は `TransactionVoidRequest` に `approvedByStaffId?` を追加し、`staffId` が Cashier なら承認者が必須。  
      担当・承認者は有効で、その店舗 (または本部) に所属していること
- [ ] `Auth:Enabled=false`: 全ポリシーを `RequireAssertion(true)` にし、一致検証と役割検証のうちクレームに依存する部分を省く (役割検証は残す)

### 8d 端末

- [ ] `HttpService`: `SecureStorage` の端末トークンを Bearer で付与。  
      401 は「端末登録が無効です」の通知 → トークンを破棄して T-00 へ (ローカル DB と Outbox は保持し、再登録後に送信を続ける)
- [ ] T-00 初期設定: 入力を「サーバ URL + ペアリングコード」に変更 (店舗 ID / 端末 ID の手入力は廃止)。  
      設定 QR (`ApiEndPoint` + `PairingCode`) の読取 → `POST /terminals/pair` → トークン・店舗・端末を保存 → 全件同期。  
      `SettingParser` は `PairingCode` を読む
- [ ] T-91 PIN ログイン: T-01 で担当を選んだら PIN 入力 (`InputNumber` のマスク表示、4〜6 桁) → `PinHasher.Verify`。  
      PIN 未設定のスタッフはチップ「PIN 未設定」で選べない。  
      3 回失敗で担当選択へ戻る。  
      ログイン中の担当の役割を `Session` に持つ
- [ ] 承認: 明細値引 / 取引値引の承認者選択 (P-13 / P-15) に承認者の PIN 入力を追加。  
      取消 (T-31) は担当が Cashier なら承認者選択 + PIN (`approvedByStaffId`)、Manager 以上はそのまま
- [ ] T-90 設定・同期: 端末登録の状態 (端末名・登録日時)、[登録の解除] (トークン破棄 → T-00)。  
      `SyncService` の周期で heartbeat (5 分に 1 回)
- [ ] ローカル DB: `Staff.PinHash` 列 (端末向け `StaffResponse.PinHash`)、`SyncState` のスキーマ版 (違えばマスタ表を作り直して全件同期)
- [ ] スタッフ S-72 に PIN 設定 (Administrator。`PinHasher` でハッシュ化)。  
      初期データのスタッフに PIN (`0000` 〜) を入れる

### 8e ツール・テスト・docs

- [ ] `Pos.Server.SampleData`: `--user` / `--password` (既定 `admin` / `admin`) で `/auth/login` し、Cookie で呼ぶ。  
      `Auth:Enabled=false` のサーバではそのまま通る
- [ ] 統合テスト: `TestApplicationFactory` に管理者ログイン (Cookie) と端末ペアリング (Bearer) のヘルパーを足し、既存テストは管理者クライアントに切り替える。  
      追加: 匿名 401、Operator のマスタ更新 403、端末の店舗不一致 403、期限切れペアリングコード 422、承認なし値引 422、Cashier の取消 422、失効トークン 401、`Auth:Enabled=false` で匿名が通ること
- [ ] 単体テスト: `PinHasher` (`Pos.Domain.Tests`)、`Login` ページ (bUnit)
- [ ] docs: api-design §2 に認証、§3.3 にペアリング / heartbeat、§5 にエラーコード。  
      db-design §3 に `Accounts` / `TerminalTokens`。  
      screen-design の T-00 / T-01 / T-91 / S-71 / S-72 / S-92。  
      architecture §3 / §5 / §6。  
      D-09 の「当面の扱い」を更新し D-43 を追加。  
      README の起動手順 (ログイン、ペアリング)

### 完了条件

- [ ] 未ログインで管理画面を開くと `/login` へ。  
      `admin` でログインしてユーザーを追加し、Operator でログインするとマスタ編集の操作が出ず、API も 403
- [ ] 初期化した端末からペアリングコードで登録 → PIN ログイン → 販売 → 送信が通る。  
      管理画面で登録を解除すると次の通信で 401 になり T-00 に戻り、再登録後に Outbox の送信が続く。  
      機内モードでも PIN ログインできる
- [ ] Cashier で承認が必要な値引・取消を行うと承認者の PIN が求められ、承認なしの要求はサーバでも 422 になる
- [ ] 統合テスト緑、警告ゼロ、InspectCode の指摘ゼロ

---

## Phase 9: 日次締め

[D-16](decisions.md#d-16-日次締めは-phase-2)。  
店舗 × 営業日の締めをサーバで確定し、締め後の変更を制御する。

### 9a DB / API

- [ ] `DailyClosings` (`Id`, `StoreId`, `BusinessDate` (店舗と組で UQ), `ClosedAt`, `ClosedBy` (管理画面のアカウント名), `ShiftCount`, `SalesCount`, `ReturnCount`, `VoidCount`, `CustomerCount`, `SalesTotal`, `ReturnsTotal`, `DiscountTotal`, `TaxTotal`, `NetSales`, `PointsEarned`, `PointsRedeemed`, `Version`)、`DailyClosingPayments` (支払方法別)、`DailyClosingTaxes` (税率別)。  
      集計は `SalesSummaryQuery` を共用
- [ ] `POST /daily-closings` (管理): `{ storeId, businessDate }`。  
      その日のシフトに Open が残っていれば 422 `SHIFT_STILL_OPEN`、締め済みは 409 `ALREADY_CLOSED`。  
      `GET /daily-closings?storeId&from&to`、`GET /daily-closings/{id}` (内訳とシフト一覧)、`DELETE /daily-closings/{id}` (締め解除、Administrator)
- [ ] 取引側の制約: 締め済みの店舗 × 営業日の取引は取消不可 (422 `DAY_CLOSED`)。  
      締め後に届いた同日の取引 (端末のオフライン分) は受け付けて `warnings[]` に `DAY_ALREADY_CLOSED` を付け、`DailyClosings.HasLateTransactions` を立てる (再締めで取り込む)

### 9b 管理画面

- [ ] S-90 日次締め (`/daily-closings`、ナビ「精算 › 日次締め」): 店舗 × 営業日の一覧 (直近 30 日。状態チップ: 未締め / 締め済み / 締め後の取引あり、未精算シフト数)、[締め] ダイアログ (未精算シフトの警告、日計プレビュー)、詳細ダイアログ (日計、支払方法別、税率別、シフト一覧)、[売上日報 PDF]、[締め解除] (Administrator)
- [ ] ダッシュボードに「前日までの未締め」警告と件数

### 9c 端末

- [ ] 変更なし (締め済みの日の取消はサーバが 422 で拒否し、Outbox の要確認として表示される)。  
      T-31 の取消確認に「営業日が締め済みなら取消できない」旨を出すかは着手時に判断

### 完了条件

- [ ] 統合テスト: 未精算で 422 → 精算後に締め → 取消 422 → 締め後の再送取引に warning → 解除 → 再締めで取り込み
- [ ] 画面: 一覧・締め・詳細・PDF・解除。  
      警告ゼロ、InspectCode ゼロ、テスト緑

---

## Phase 10: 受注 (取り寄せ・取り置き)

[D-01](decisions.md#d-01-取引モデル-一体型-vs-分離型) の「受注は将来」。  
会計はこれまでどおり取引 1 回で行い、受注は会計前の約束 (取り寄せ / 取り置き) を管理するリソースにする。  
前受金 (内金) は対象外。

### 10a DB / API

- [ ] `Orders` (`Id` (端末採番), `OrderNo` (`{店舗コード}-O-{連番}`), `StoreId`, `TerminalId?`, `StaffId`, `CustomerId?`, `CustomerName`, `Phone`, `Type` (`BackOrder` 取り寄せ / `Hold` 取り置き), `Status` (`Ordered` → `Arrived` → `Completed` / `Cancelled`), `RequestedDate?`, `Note`, `TransactionId?`, `OrderedAt`, `ArrivedAt?`, `CompletedAt?`, `CancelledAt?`, `Version`)、`OrderLines` (`ProductId`, `Quantity`, `UnitPrice`, `Note`)。  
      取り置きは登録時点で `Arrived`
- [ ] `POST /orders` (端末 / 管理、同一 id は 200)、`GET /orders?storeId&status&customerId&keyword&from&to`、`GET /orders/{id}`、`PUT /orders/{id}` (`Ordered` のみ)、`POST /orders/{id}/arrive`、`POST /orders/{id}/cancel`
- [ ] 会計との紐付け: `TransactionCreateRequest.OrderId?`。  
      登録時に受注が `Arrived` でなければ 422 `ORDER_NOT_READY`、成功で `Orders.TransactionId` + `Completed`。  
      取引の取消で `Arrived` に戻す。  
      `TransactionResponse` に `orderId` / `orderNo`
- [ ] 在庫は会計時に減る (受注時の引当はしない)。  
      取り寄せの入荷は Phase 14 の入荷とは独立 (状態だけ)

### 10b 管理画面

- [ ] S-91 受注 (`/orders`、ナビ「取引 › 受注」): 一覧 (店舗・状態・種別・期間・キーワード)、詳細 / 編集ダイアログ (連絡先・明細・希望日・備考)、[入荷] [キャンセル]、完了した受注から取引詳細 S-21 へ
- [ ] 会員詳細 S-61 に受注タブ、ダッシュボードに「入荷待ち / 引き渡し待ち」件数

### 10c 端末

- [ ] ホーム T-02 に「受注」タイル (最下段を「受注 / 設定・同期」の 2 列に)
- [ ] T-92 受注一覧 (自店、状態フィルタ、検索。オンライン限定) → 詳細 → [会計へ] (明細をカートに展開し会員を設定、`SalesContext.OrderId` を持って T-10 へ。会計で `orderId` を送る)、[入荷] [キャンセル]
- [ ] T-10 販売の [⋯] に「受注にする」: 会員 (または宛名・電話)、種別、希望日、備考 → `POST /orders` (オンライン限定) → カートをクリア。  
      会計時に受注から来た取引はレシートに受注番号を印字

### 完了条件

- [ ] 端末で受注 → 管理画面で入荷 → 端末で会計 → `Completed`、取消で `Arrived` に戻る。  
      `Ordered` のまま会計すると 422
- [ ] 統合テスト (登録 / 状態遷移 / 会計 / 取消)、警告ゼロ、InspectCode ゼロ

---

## Phase 11: 商品画像・CSV 取込

### 11a 商品画像

- [ ] `PUT /products/{id}/image` (multipart、JPEG / PNG、2 MB まで) → `Storage:ImageDirectory` (既定 `App_Data/images/products`) に `{id}.{ext}` で保存し `ImageUrl` = `/api/v1/products/{id}/image?v={version}`。  
      `GET /products/{id}/image` (`Api`)、`DELETE /products/{id}/image`。  
      縮小はしない (サイズ制限のみ)
- [ ] S-51 商品編集に画像 (プレビュー、アップロード、削除)、S-50 一覧にサムネイル列
- [ ] 端末: 商品照会 T-60 と検索 T-12 に画像。  
      オンラインで取得して `FileSystem.CacheDirectory` にキャッシュ (`v` が変わったら取り直す)、オフラインはキャッシュのみ

### 11b CSV 取込

- [ ] `POST /products/import` (multipart CSV。列は `GET /products/csv` と同じ。`dryRun=true` でプレビュー): 行ごとに `Insert` / `Update` (コード一致) / `Error` (メッセージ) を返す。  
      部門・税率はコードで参照。  
      エラーが 1 行でもあれば取り込まず 422 で行結果を返す (全件成功のときだけ 1 トランザクションで反映)
- [ ] S-52 取込ダイアログ (`MudFileUpload` → プレビュー表 (結果チップ) → [取込])。  
      初期データと同じ 33 商品の CSV を `docs/samples/products.csv` に置く

### 完了条件

- [ ] 画像をアップロードして端末に表示される (オフラインではキャッシュ)。  
      CSV で新規 / 更新 / エラーの各行が期待どおりになる
- [ ] 統合テスト (画像の PUT / GET / DELETE、取込の dryRun / 成功 / エラー)、警告ゼロ、InspectCode ゼロ

---

## Phase 12: レシート・帳票・検索の拡張

### 12a レシート PDF (サーバ)

- [ ] `GET /transactions/{id}/receipt/pdf` ([D-37](decisions.md#d-37-帳票出力-pdf-oysterreport))。  
      テンプレート `Assets/Reports/Receipt.xlsx` (レシート幅相当の 1 列)、`ReceiptReportBuilder`。  
      内容は端末の `ReceiptTextBuilder` と同じ項目 (`Pos.Domain` に共通の行生成を寄せるかは着手時に判断)
- [ ] S-21 取引詳細に [レシート PDF]

### 12b 端末の印刷

- [ ] T-22 レシート / T-52 精算レポートの [印刷] を有効化: Android の印刷フレームワーク (`PrintManager` + `PrintDocumentAdapter`) にレシート画像 / レポートのテキストを渡す (「PDF に保存」や対応プリンタで出力)。  
      Bluetooth レシートプリンタ (ESC/POS) は対象外として記録

### 12c 検索・送信

- [ ] シリアル番号検索: `GET /transactions?serialNumber=` (完全一致)、S-20 のフィルタ、T-30 取引履歴の検索欄 (オンライン限定)
- [ ] `POST /transactions/batch` (要素ごとの結果)。  
      `SyncService` は Outbox に取引が連続して 10 件以上溜まっているときだけ使う (任意。着手時に効果を見て省いてもよい)

### 完了条件

- [ ] レシート PDF が端末表示と同じ内容で出る。  
      エミュレータで [印刷] → 「PDF に保存」が動く。  
      シリアルで取引が引ける
- [ ] 統合テスト、警告ゼロ、InspectCode ゼロ

---

## Phase 13: 通知 (SignalR)

### 13a 端末向けハブ

- [ ] `/hubs/terminal` (端末トークンで認証、`Groups` は店舗単位): `MasterUpdated(kind)` (マスタ保存時に管理画面 / API から `IHubContext` で送る)、`TerminalRevoked` (登録解除)
- [ ] 端末: `SyncService` が `HubConnection` を保持し、接続中は 5 分周期の差分同期を通知駆動にする (切断時は従来の周期に戻る)。  
      接続状態を通信インジケータと T-90 に表示。  
      heartbeat はハブ接続中は不要

### 13b 管理画面の自動更新

- [ ] プロセス内の `ChangeNotifier` (シングルトン、種別ごとのイベント): 取引・シフト・在庫変動の登録時に発火し、ダッシュボードと開設中シフト・端末の通信状態・要確認を自動更新する (Blazor Server 内なので SignalR クライアントは使わない)

### 完了条件

- [ ] 管理画面で商品を保存すると端末が数秒で再同期する。  
      端末を解除すると即座に T-00 へ。  
      ダッシュボードが端末の販売で更新される
- [ ] 統合テスト (ハブ接続の認証)、警告ゼロ、InspectCode ゼロ

---

## Phase 14: 在庫移動・入荷

### 14a DB / API

- [ ] `Suppliers` (仕入先マスタ: コード・名称・連絡先)、`InventoryReceipts` (入荷: 店舗・仕入先・伝票番号・入荷日・状態 `Draft` → `Received`) + 明細 (商品・数量・原価)。  
      受領で `InventoryChangeType.Receive` (+数量) を記録
- [ ] `InventoryTransfers` (店舗間移動: 出荷店・入荷店・状態 `Requested` → `Shipped` → `Received` / `Cancelled`) + 明細。  
      出荷で `TransferOut` (−)、受領で `TransferIn` (+)
- [ ] API: `/inventory/suppliers`、`/inventory/receipts` (登録・一覧・詳細・受領)、`/inventory/transfers` (登録・一覧・詳細・出荷・受領・キャンセル)。  
      変動履歴の `type` フィルタに新種別を追加

### 14b 管理画面

- [ ] S-45 入荷、S-46 移動、S-57 仕入先 (一覧・編集・状態遷移)。  
      在庫変動履歴 S-42 の種別に追加。  
      ダッシュボードに「未受領の移動」件数

### 14c 端末

- [ ] T-71 入荷検品 / T-72 移動受領: 自店宛の未受領一覧 → スキャンで数量を確認 → 受領 (オンライン限定)。  
      `SampleData` の「サンプル入荷」を入荷 (`Receive`) に置き換える

### 完了条件

- [ ] 入荷と店舗間移動が在庫と変動履歴に反映される (他店在庫照会で確認)。  
      統合テスト、警告ゼロ、InspectCode ゼロ
