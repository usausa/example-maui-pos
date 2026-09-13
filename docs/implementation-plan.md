# 実装プラン (チェックリスト)

フェーズ単位で着手し、完了条件を満たしてから次へ進む ([D-33](decisions.md#d-33-実装の進め方-フェーズ単位のチェックリスト))。設計は [architecture.md](architecture.md) / [api-design.md](api-design.md) / [db-design.md](db-design.md) / [screen-design.md](screen-design.md)。

## 進め方

- 着手するフェーズを指示してもらってから始める。完了したら完了条件の確認結果を報告し、本書のチェックを更新する
- 各フェーズの終わりは `dotnet build` 警告ゼロ・テスト緑 (テンプレートの規約)・`jb inspectcode` (ReSharper、Jenkins と同じ `--properties:Configuration=Release`) の指摘ゼロ
- 実装中に設計を変えた場合は [decisions.md](decisions.md) に追記し、該当文書を直してからフェーズを閉じる
- [architecture.md §9](architecture.md#9-実装時に確認する事項) の確認事項は該当フェーズで消化する (各項目に `§9-n` で示す)
- コミットはフェーズ (大きいものはサブフェーズ) 単位

---

## Phase 0: 土台 (完了)

サーバ・端末のソリューションがそれぞれ Visual Studio で開けて動く状態を作る。参考プロジェクトは着手時に差し替えた ([D-34](decisions.md#d-34-参考プロジェクトの差し替え-phase-0))。

### ルート

- [x] `.editorconfig` / `.gitattributes` / `.gitignore` / `Directory.Build.targets` / `Analyzers.ruleset` / `CodeCoverage.runsettings` を `Service-CloudManager` からコピー
- [x] `Directory.Build.props` をコピーし、`NoWarn` に MAUI 用の `NU1608` を含める (テンプレート間の唯一の差分)
- [x] Jenkins のパイプライン (サーバと端末を 1 つでビルド / 検査 / テスト / 公開。Jenkins 側の設定)。テストは `dotnet run --project` で実行し、`global.json` は置かない ([D-35](decisions.md#d-35-テストの実行方法))
- [x] `AGENTS.md` を作成 (テンプレートの規約 + 本プロジェクト固有: 「DTO」不使用、Service / Usecase なし、camelCase、フォルダ構成)。`CLAUDE.md` は `AGENTS.md` を参照
- [x] ルート `README.md` に構成と起動方法

### shared/

- [x] `shared/Pos.Domain` (net10.0、`Usa.Smart.Core`) の空プロジェクト
- [x] `shared/Pos.Shared` (net10.0、`Pos.Domain` 参照) の空プロジェクト
- [x] `shared/Pos.Domain.Tests` (xunit.v3 + Microsoft.Testing.Platform)。`Pos.Domain` が UI / DB / HTTP / `Pos.Shared` を参照しないことを検証する `DependencyTests`

### server/

- [x] `Service-CloudManager` の `Core` / `Host` / `UnitTests` / `IntegrationTests` を骨組みとしてコピーし、`CloudManager` → `Pos.Server` にリネーム。AWS / ジョブ / Services は含めない
- [x] Aspire AppHost (`Pos.Server.AppHost`) と OpenAPI (`Microsoft.AspNetCore.OpenApi` + NSwag の `/swagger`, `/redoc`) を `template-blazor-server` から追加
- [x] `Core`: `SqlHelper` と `Extensions` のみ (Accessor / Entity は Phase 3)
- [x] `Host`: `ApplicationExtensions` (camelCase JSON、ProblemDetails、Serilog、ヘルスチェック、MudBlazor)、レイアウト (`MainLayout` / `NavMenu` / `EmptyLayout` / `ReconnectModal`)、ページ (`Home` / `Error` / `NotFound`)、共通部品 (`ErrorBanner` / `ProgressOverlay` / `AppMessageBox`)、`GlobalExceptionHandler`、`DatabaseHealthCheck`、`LogSetting` / `ProfilerSetting`、`appsettings` (ポート 8080、`pos.db`)
- [x] `tests`: `NavMenuTests` (bUnit)、`SqlHelperTests`、`HostTests` (`/health`、`/`、`/unknown` → 404 ページ、`/api/unknown` → 404 のみ)
- [x] `server/Pos.Server.slnx`: AppHost / Core / Host / UnitTests / IntegrationTests + `../shared/` の 3 プロジェクト。Solution Items はルートのファイルを `../` で参照

### terminal/

- [x] `template-maui-keyboard` の `Template.MobileApp` をコピーし `Pos.Terminal` にリネーム (フォルダ / csproj / 名前空間 / `ApplicationId` = `pos.terminal` / `ApplicationTitle` = `POS` / `MainActivity` 名 / Android の `label`)
- [x] 不要なものを除去: サンプル画面 (`Modules/Key`)、フォント (OpenSans / FluentUI と未使用の参照。`MaterialIcons` だけ残す)、`dotnet_bot.png`
- [x] `ViewId` を `Menu` のみにし、`Modules/Main/MenuView` (T-02。販売を最上段 2 列幅、機能未実装のため全ボタン無効) を作成。起動時に `Menu` へ遷移
- [x] `Styles.xaml` に `FooterLabel` と POS 節 (`Pos` 接頭辞: 背景・行・区切り線・名称 / 金額ラベル・オプション / 数量 / 実行ボタン) を追加。配色は `Colors.xaml` の Material パレット
- [x] `terminal/Pos.Terminal.slnx`: `Pos.Terminal` + `../shared/Pos.Domain` + `../shared/Pos.Shared`。`Settings.XamlStyler` / `Pos.Terminal.sln.DotSettings` をコピー
- [x] 画面遷移の Forward / Back アニメーション: `AddHierarchyEffectPlugin` + 各画面の `[Hierarchy(n)]` ([D-36](decisions.md#d-36-端末の画面遷移アニメーション))
- [ ] Rester の JSON を camelCase に設定 → Phase 6 (通信を取り込むとき)

### 完了条件

- [x] `dotnet build server/Pos.Server.slnx` が警告ゼロで通り、3 つのテストプロジェクトが `dotnet run --project` で緑 (19 件)
- [x] `dotnet build terminal/Pos.Terminal.slnx` が警告ゼロで通る (MAUI ワークロード確認済み)
- [x] サーバ起動で `/health` = Healthy、`/` 200、`/swagger` 200、`/openapi/v1.json` に "POS API"、`/api/unknown` 404
- [x] Aspire AppHost から起動できる (ダッシュボード http://localhost:15000)
- [x] 端末をエミュレータ (Pixel 6a API 35) で起動してホームのメニューが表示される
- [ ] Visual Studio でそれぞれの `.slnx` を個別に開いて実行できる (利用者側で確認)

---

## Phase 1: `Pos.Domain` (完了)

計算ロジックと業務ルール。api-design §4 を実装し、テストで固定する。

- [x] 列挙型 (architecture §4.1 の一覧)
- [x] `global using Pos.Domain;` を `Pos.Shared` / `Pos.Server.Core` / `Pos.Server.Host` (`GlobalUsing.cs`、`_Imports.razor`) に追加 (Phase 0 では空の名前空間のため外してある)
- [x] `SalesInput` / `SalesResult` (明細・値引・支払・会社設定 → 計算項目)。Request / Response には依存しない (record)
- [x] `Allocation`: 最大剰余法の按分 (同値は順序で決定)
- [x] `Rounding`: `Floor` / `Round` (四捨五入) / `Ceiling`
- [x] `SalesCalculator`: 明細金額 → 明細値引 → 取引値引の按分 → 税 (税率 × 内税/外税グループ、`TaxCalculator`) → ポイント (利用按分・付与) → 合計・預り・釣銭
- [x] `ReturnCalculator`: 元明細 (`ReturnInput.OriginalLines`) からの返品明細導出 (api-design §4.5)
- [x] `TransactionRules`: api-design §3.12 の業務ルール (Sale / Return / Void) を検証し `ErrorCode` / `WarningCode` を返す。事実は `SaleContext` / `ReturnContext` / `VoidContext` で受け取り、再計算結果を `Expected` に返す。`SalesResultComparer` で一致判定
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
- [x] `Pos.Domain` が UI / DB / HTTP / `Pos.Shared` に依存していない (`DependencyTests`)

---

## Phase 2: `Pos.Shared` (完了)

api-design §3 の通信データ。

- [x] `Common/`: `ListResponse<T>`、`ProblemResponse`、`JsonDateTimeConverter` (エラーコード定数は `Pos.Domain` の `ErrorCode.ToCode()` で代替)
- [x] マスタ: Settings / Store / Terminal / Staff / Category / TaxRate / Product / Discount / PaymentMethod / AdjustmentReason の `XxxResponse` / `XxxCreateRequest` / `XxxUpdateRequest` / `XxxListResponse`、`SyncMastersResponse`
- [x] 顧客: `CustomerXxx`、`PointHistoryResponse` / `PointHistoryListResponse`、`PointAdjustRequest`
- [x] 取引: `TransactionRequest` / `TransactionResponse` (+ `TransactionRequestLine` / `TransactionResponseLine`、値引・税集計・支払・配送・取消情報・警告)、`TransactionListResponse`、`TransactionVoidRequest`、`TransactionCalculateRequest`、`TransactionCalculationResponse`
- [x] シフト: `ShiftOpenRequest`、`ShiftResponse` / `ShiftListResponse`、`CashEventRequest` / `CashEventResponse` / `CashEventListResponse`、`ShiftCloseRequest`、`ShiftSummaryResponse`
- [x] 在庫: `InventoryLevelResponse` / `InventoryLevelListResponse`、`ProductInventoryResponse`、`InventoryChangeRequest` / `InventoryChangeResultResponse`、`InventoryChangeResponse` / `InventoryChangeListResponse`
- [x] レポート: `SalesSummaryResponse`、`ProductSalesResponse`
- [x] 検証属性 (`Required` / `MaxLength` / `Range`) をテンプレートと同じ書き方で付与
- [x] サーバの JSON 設定に `JsonDateTimeConverter` / `JsonStringEnumConverter` を登録し、`JsonContractTests` (統合テスト) で形式を固定
- [x] CA1716 / CA1056 は `Pos.Shared` の `GlobalSuppressions.cs` で抑止 ([D-38](decisions.md#d-38-警告の抑止))

### 完了条件

- [x] 両ソリューションでビルドが通る (警告ゼロ)
- [x] api-design §3 の全フィールドと名称・型が一致している (突き合わせ済み。差分は api-design に反映: `payments[].note`、`warnings[]`、`calculate` の応答型、端末 / 商品の Request に含めない項目)

---

## Phase 3: サーバ DB (完了)

db-design の DDL・Entity・Accessor・初期データ。

- [x] `EnumTextConverter<T>` と技術検証 (§9-2 / §9-3): 列挙型 TEXT、`Guid` TEXT、`decimal` NUMERIC (INSERT → SELECT → `SUM`)、`DateOnly` / `DateTime` (UTC) を `DatabaseTests` で確認。コンバータは `DataProfile` に一括宣言
- [x] Entity 一式 (db-design §3、24 クラス)
- [x] `Create.sql` を Accessor ごとに分割して全テーブル・インデックス (部分ユニークインデックス含む) を作成。起動時に PRAGMA (WAL / busy_timeout)、外部キーは接続文字列 `Foreign Keys=True`
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

- [x] 起動で `pos.db` (実行ディレクトリ) が作られ、初期データが入る。`/health` = Healthy
- [x] 技術検証テスト (`DatabaseTests`) と Accessor のテスト (`AccessorTests`: 楽観ロック / 一覧フィルタ / 開設 → 販売 → 集計 → 返品数量 → 取消 → 精算 → レポート) が緑 (統合テスト 15 件、テンプレートと同じく `TestApplicationFactory` の一時 DB)

---

## Phase 4: サーバ API (完了)

api-design §3 のエンドポイント。順番はマスタ → 顧客 → シフト → 取引 → 在庫 → レポート。

### 4a マスタ・設定・同期

- [x] `Mappers` (Smart.Mapper の `[Mapper]`): `MasterMapper` (マスタ・顧客)、`TransactionMapper` (取引一式、`SalesInput` / `ReturnInput` への変換、計算結果 ↔ `TransactionCalculationResponse`)、`ShiftMapper` (集計付き応答、`ExpectedCash`)、`InventoryMapper`、`ReportMapper` (`GroupKey → Key` は `[MapProperty]`)
- [x] Settings / Stores / Terminals / Staff / Categories / TaxRates / Products (+ `lookup`) / Discounts / PaymentMethods / AdjustmentReasons (`Endpoints/XxxEndpoints.cs`、静的クラス + `MapGroup`)。Problem Details は `Infrastructure/Api/ApiProblems.cs` (`errorCode` / `errors` / `expected`)、`AddValidation` の 400 にも `VALIDATION_ERROR` を付ける
- [x] `GET /sync/masters?since` (変更がなければ `settings` は省略、`products` は `MaxPageSize` 超で `productsTruncated`)
- [x] 一覧の並び替え許可列 (`ApiHelper.ResolveSort`: `updatedSince` 指定時は `UpdatedAt, Id` 固定)。`GET /{id}` は論理削除済みも `isDeleted: true` で返し、更新・削除は 404

### 4b 顧客・ポイント

- [x] 検索 / lookup / 登録 / 更新 / 論理削除
- [x] ポイント履歴、手動調整 (1 トランザクションで残高更新 + `Adjust` 履歴、応答は `PointHistoryResponse`)、購入履歴

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

- [x] `summary` (groupBy 7 種。合計行は各行の合算、不正な groupBy は 400)、`products` (`sort=netSales|quantity`)。`from` / `to` 省略時は当日と 30 日前
- [x] `hour` のタイムゾーン処理 (§9-4): 店舗の `TimeZone` を `TimeZoneInfo` で解決し、SQLite の `+NNN minutes` 修飾子で SQL 側集計

### 4g 帳票 (PDF、[D-37](decisions.md#d-37-帳票出力-pdf-oysterreport))

- [x] `OysterReport` 1.9.0 の導入: パッケージ、`Assets/Fonts/ipaexg.ttf`、`EmbeddedFontResolver` (`template-blazor-server` から)、`Assets/**` を出力ディレクトリへコピー
- [x] テンプレート `Assets/Reports/ShiftReport.xlsx` (精算レポート) / `DailySalesReport.xlsx` (売上日報)。A4 縦 1 シート、明細行はプレースホルダの行を件数分に複製 (`ReportText.FillRows`)
- [x] `ShiftReportBuilder` + `GET /shifts/{id}/summary/pdf`、`DailySalesReportBuilder` + `GET /reports/sales/daily/pdf?storeId&date` (シングルトン、`byte[] Build(...)`、日時は店舗のタイムゾーン)
- [x] 統合テスト: `application/pdf` で先頭が `%PDF` のレスポンス、対象なしは 404 (生成した PDF は `TestResults/` に保存して目視確認)

### テスト

- [x] 統合テストのシナリオ (`ApiTransactionFlowTests`): 開設 → 再送 200 / 再開設 409 → calculate (api-design §4.6 の値) → 販売 → 同一 id 再送 200 / 相違 409 / 計算違い 422 (`expected`) → ポイント・在庫・連番 → 返品 (§4.5 の値) → 超過 422 → 取消 (返品済みは `HAS_RETURNS`、返品の取消で戻る) → 入出金 → 精算 → 精算後は `SHIFT_CLOSED` → summary / レポート 7 種 / PDF の整合。棚卸・調整の冪等性は `InventoryChangesAreIdempotent`
- [x] 差分同期 (`sync/masters` の全件 → 差分空)、楽観ロック 409、重複コード 409、使用中 422、入力検証 400 (`ApiMasterTests`)
- [x] OpenAPI ドキュメント (`/openapi/v1.json`) にエンドポイントが載ることを `HostTests` で確認

### 完了条件

- [x] 統合テスト緑 (24 件。単体 14 件・Domain 73 件と合わせて 111 件)、警告ゼロ、InspectCode の指摘ゼロ

---

## Phase 5: 管理画面 (完了)

screen-design §2 の ★ 画面。ページは Accessor / Domain を直接使い、API と同じ処理は静的ヘルパー (`SalesSummaryQuery` / `InventoryChangeApplier`) で共用する。状態は絵文字付きチップとバッジで示す ([D-39](decisions.md#d-39-管理画面の表現-絵文字チップバッジ))。

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

- [x] ★ 画面がすべて動く (API で開設 → 販売 → 返品 → 入出金 → 精算を作り、ブラウザでダッシュボードから各画面・ダイアログを確認)。編集ダイアログの検証・楽観ロック (`VERSION_MISMATCH`)・削除確認・使用中エラーが機能する
- [x] bUnit テスト (`NavMenu` のリンク、`StatusChip`) と `FormMapper` の単体テスト (単体 19 件、統合 24 件、Domain 73 件)。警告ゼロ、InspectCode の指摘ゼロ

---

## Phase 6: 端末

screen-design §1 の ★ 画面。サーバが動いている前提。

### 6a 土台

- [ ] ローカル DB (マスタキャッシュ・取引一式・Outbox・SyncState、db-design §6)
- [ ] `HttpService` (Pos.Shared の Request / Response で api-design の ★ を呼ぶ)。Rester の JSON を camelCase に設定
- [ ] `SyncWorker`: 差分同期、Outbox 送信 (順序・バックオフ・要確認で停止)
- [ ] `Settings` (ApiEndPoint / StoreId / TerminalId / OpenSalesAfterLogin)、`Session`
- [ ] セットアップ T-00 (設定 QR / 手入力 / 初回同期)、スタッフ選択 T-01
- [ ] 各画面の `ContentView` に `[Hierarchy(n)]` を付ける (screen-design §1.3 の深さ。[D-36](decisions.md#d-36-端末の画面遷移アニメーション))

### 6b メニュー・開設・設定

- [ ] メニュー T-02 (未送信バッジ・シフト状態・販売への誘導)
- [ ] レジ開設 T-03
- [ ] 設定・同期 T-90 (未送信一覧・再送・破棄・手動同期)

### 6c 販売

- [ ] `Cart` モデル (Pos.Domain の `SalesCalculator` への入力を組み立て、結果を保持)
- [ ] 販売 T-10、スキャン T-11 (連続読み取り・モード)、商品検索 T-12
- [ ] 明細編集 P-13、会員選択 T-14、取引値引 P-15、配送先 T-16、保留 T-17

### 6d 会計・レシート

- [ ] 会計 T-20 (埋め込みテンキー・複数支払・ポイント利用・釣銭)
- [ ] 会計完了 T-21、レシート T-22 (プレビュー・電子レシート QR・共有)

### 6e 精算・入出金

- [ ] 入出金 T-50、精算 T-51 (未送信警告・金種入力)、精算レポート T-52

### 6f 返品・履歴

- [ ] 返品 T-40 / 返品明細選択 T-41 / 返金 T-42 (`ReturnCalculator`)
- [ ] 取引履歴 T-30 / 取引詳細 T-31 (取消)

### 6g 照会・棚卸・売上

- [ ] 商品・在庫照会 T-60 (他店在庫)、会員照会 T-61 / 登録・編集 T-62
- [ ] 棚卸・在庫調整 T-70、売上照会 T-80

### 完了条件

- [ ] エミュレータで「セットアップ → 開設 → 販売 (スキャン・会員・値引) → 会計 → レシート → 返品 → 入出金 → 精算」が通り、サーバ側の取引・在庫・ポイント・シフトに反映される
- [ ] 機内モードで販売 → 復帰で Outbox が順に送信される。`422` は要確認として止まり、T-90 で確認できる
- [ ] 警告ゼロ

---

## Phase 7: 仕上げ (任意)

- [ ] ルート `README.md` に両方の起動手順とスクリーンショット
- [ ] 実装で変わった点を docs に反映 (decisions.md / 各設計)
- [ ] レポート確認用のサンプル取引生成 (任意)
