# POS サーバ API 設計

MAUI レジ端末アプリと Blazor 管理画面が利用する POS サーバ (ASP.NET Core) の API 設計。  
設計判断は [decisions.md](decisions.md)、DB は [db-design.md](db-design.md)、プロジェクト構成は [architecture.md](architecture.md) を参照。

- [1. 前提](#1-前提)
- [2. 共通仕様](#2-共通仕様)
- [3. リソース別 API](#3-リソース別-api)
- [4. 金額・税・ポイント計算仕様 (共有ライブラリ)](#4-金額税ポイント計算仕様-共有ライブラリ)
- [5. エラーコード](#5-エラーコード)
- [6. 端末側の同期フロー](#6-端末側の同期フロー)

---

## 1. 前提

| 項目 | 内容 |
| --- | --- |
| 業種 | 家電・カメラ・ホームセンター (物販) |
| 利用者 | **端末** (MAUI レジアプリ) と **管理** (Blazor 管理画面)。表中の「用途」列で区別する。管理画面は HTTP を経由せず Accessor と Domain を直接呼ぶが、機能は API と 1 対 1 に対応させる |
| 実装基盤 | Minimal API + Blazor Server (MudBlazor) + SQLite、Aspire、OpenAPI ([D-19](decisions.md#d-19-技術スタックプロジェクト構成-テンプレート準拠)) |
| 取引モデル | 一体型。会計完了後に取引を 1 回で送信 ([D-01](decisions.md#d-01-取引モデル-一体型-vs-分離型)) |
| 金額計算 | 端末が計算し、サーバは同じ `Pos.Domain` で再計算して検証 ([D-02](decisions.md#d-02-金額計算の主体-端末計算--サーバ検証-vs-サーバ計算のみ)) |
| 認証 | なし ([D-09](decisions.md#d-09-認証端末登録-後回し))。端末発の要求は `storeId` / `terminalId` / `staffId` を本文またはクエリで明示する |
| テナント | 単一 ([D-06](decisions.md#d-06-テナント構成)) |

---

## 2. 共通仕様

### 2.1 URL・形式

| 項目 | 仕様 |
| --- | --- |
| ベースパス | `/api/v1` (URL パスでバージョニング)。ルート定数は `ApiRoutes` にまとめる |
| JSON | **camelCase** ([D-20](decisions.md#d-20-json-契約-camelcase))。`null` プロパティは省略。列挙型は文字列 (`"Sale"`) |
| クエリパラメータ | camelCase (`?storeId=&updatedSince=`) |
| 日時 | `yyyy-MM-ddTHH:mm:ss.fffZ` (UTC)。営業日などの日付は `yyyy-MM-dd` |
| 金額 (`money`) | `decimal`。通貨は会社設定 (`JPY`)。JPY では整数値のみ ([D-13](decisions.md#d-13-金額数量率の表現-decimal)) |
| 率 (`rate`) | `decimal`、`0.10` = 10% |
| 数量 (`qty`) | `decimal(9,2)` 相当。ホームセンターの切り売り (m 単位) を想定 |
| ポイント | 整数 (`int`)。1 pt = 1 円 |
| ID | GUID。端末発の書き込みは端末が GUID v7 を採番 ([D-10](decisions.md#d-10-冪等性-クライアント採番-id)) |
| 通信データ | `XxxRequest` / `XxxResponse` (一覧は `XxxResponse`、要素は `XxxResponseItem`) を `Pos.Contract` に置き、サーバと端末の両方で使う ([D-22](decisions.md#d-22-共有プロジェクト-通信データとドメインロジックは別プロジェクト), [D-27](decisions.md#d-27-用語-dto-は使わない))。端末は HttpClient + System.Text.Json の `HttpService` (手書き、[D-40](decisions.md#d-40-端末の通信-rester-ではなく-httpclient)) |
| OpenAPI | `Microsoft.AspNetCore.OpenApi` + 開発時 NSwag UI (`/swagger`, `/redoc`) |

本書のフィールド名は JSON (camelCase) で書く。  
C# のプロパティ名は PascalCase (`receiptNo` → `ReceiptNo`)。

### 2.2 一覧取得 (ページング・フィルタ)

- 一覧は `page` (0 始まり) / `size` (既定 20、最大 1000) のページ方式 ([D-21](decisions.md#d-21-ページング-page--size--総件数))。  
  応答は `XxxResponse` (要素は `XxxResponseItem`)。  
  ページングしない一覧 (税率など) も同じ形で返す (`page` = 0、`size` = 件数)

```jsonc
{ "total": 1234, "page": 0, "size": 20, "items": [ /* XxxResponseItem */ ] }
```

- 並び替えは `sort` (列名) / `desc` (bool)。  
  許可する列はリソースごとの列挙型 (`StoreSort` など) で決め、一覧の `sort` が不正なら既定の列 (レポートの `sort` / `groupBy` が不正なら 400)。  
  `sort` / `groupBy` の値は大文字小文字を区別しない
- マスタ系一覧は **差分同期**用に `updatedSince` (datetime) と `includeDeleted` (bool) を受け付ける。  
  `updatedSince` 指定時は `updatedAt > updatedSince` のレコードを `updatedAt, id` 昇順で返し、論理削除済みも `isDeleted: true` で含める
- 日付範囲は営業日 `from` / `to` (両端含む)。  
  レポートは指定がなければ `to` は当日、`from` は `to` の 30 日前。  
  一覧 (取引・シフト・在庫変動) は指定がなければ絞らない

### 2.3 書き込み

| 項目 | 仕様 |
| --- | --- |
| 作成 | `POST /resources` (本文 `XxxCreateRequest` または端末発の `XxxRequest`) → `201 Created` + `XxxResponseItem`。端末発 (取引・シフト・入出金・在庫変動) は本文の `id` を必須とし、**同じ `id` が既に存在すれば `200 OK` で既存を返す**。本文が既存と一致しない場合は `409 Conflict` (`DUPLICATE_ID_MISMATCH`) |
| 更新 | 管理系は `PUT /resources/{id}` (`XxxUpdateRequest`、全体置換)。本文の `version` で楽観ロック。不一致なら `409 Conflict` (`VERSION_MISMATCH`) |
| 削除 | 管理系は `DELETE /resources/{id}` で論理削除 (`isDeleted = true`)。取引など履歴は削除しない。削除後も `GET /resources/{id}` は `isDeleted: true` で返し、更新・再削除は `404` |
| 検証 | 入力エラーは `400` (`AddValidation` + DataAnnotations。`errorCode` = `VALIDATION_ERROR`、`errors` にフィールド別)、業務ルール違反は `422` |
| 重複 | コード・バーコード等の一意制約違反は `409` (`DUPLICATE_CODE`)。`IDialect.IsDuplicate` で SQLite の制約違反を判定する |

### 2.4 エラー応答

RFC 9457 Problem Details (`AddProblemDetails`。`traceId` 拡張付き) に `errorCode` を追加する。  
コードは [§5](#5-エラーコード)。

```jsonc
{
  "type": "https://example.com/errors/calculation-mismatch",
  "title": "計算結果が一致しません",
  "status": 422,
  "detail": "total: 送信 80100, サーバ 80200",
  "traceId": "00-...",
  "errorCode": "CALCULATION_MISMATCH",
  "errors": { "total": ["expected 80200"] },   // 任意: フィールド別
  "expected": { /* サーバ計算結果 (取引検証時のみ、TransactionCalculateResponse) */ }
}
```

### 2.5 共通フィールド

| フィールド | 型 | 説明 |
| --- | --- | --- |
| `id` | guid | 主キー |
| `isDeleted` | bool | 論理削除 (マスタ系) |
| `createdAt` / `updatedAt` | datetime | サーバ付与 |
| `version` | int | 楽観ロック用 (マスタ系)。更新のたびに +1 |

---

## 3. リソース別 API

各表の「用途」: **端末** = MAUI レジアプリが使う / **管理** = Blazor 管理画面が使う。  
フィールド表は `XxxResponseItem` の項目。  
`XxxCreateRequest` / `XxxUpdateRequest` はそこからサーバ付与項目 (`id`, `createdAt`, `updatedAt`) を除いたもの (`version` は Update のみ)。

### 3.1 会社設定 (Settings)

単一レコード。  
計算ルールを持つので端末も同期する。

| フィールド | 型 | 説明 |
| --- | --- | --- |
| `companyName` | string(100) | 会社名 (レシート用) |
| `currency` | string(3) | `JPY` |
| `taxRounding` | enum | `Floor` / `Round` / `Ceiling`。税額の端数処理 (既定 `Floor`) |
| `pointBasis` | enum | `TaxIncluded` / `TaxExcluded`。ポイント付与の基準額 (既定 `TaxIncluded`) |
| `businessDayStartTime` | string | `"05:00"`。この時刻より前は前営業日として扱う |
| `updatedAt`, `version` | | |

| Method | Path | 用途 | 概要 |
| --- | --- | --- | --- |
| GET | `/settings` | 端末 / 管理 | 会社設定取得 (`SettingsResponse`) |
| PUT | `/settings` | 管理 | 会社設定更新 (`SettingsUpdateRequest`) |

### 3.2 店舗 (Stores)

| フィールド | 型 | 説明 |
| --- | --- | --- |
| `id` | guid | |
| `code` | string(10) | 店舗コード (一意)。レシート番号の一部になる |
| `name` | string(100) | |
| `postalCode`, `address`, `phone` | string | レシート印字用 |
| `registrationNo` | string(14) | 適格請求書発行事業者登録番号 (`T` + 13 桁) |
| `receiptHeader`, `receiptFooter` | string(500) | レシート上下の文言 |
| `timeZone` | string(50) | `Asia/Tokyo` |
| `isActive`, `isDeleted`, `createdAt`, `updatedAt`, `version` | | |

| Method | Path | 用途 | 概要 |
| --- | --- | --- | --- |
| GET | `/stores?updatedSince&includeDeleted&page&size` | 端末 / 管理 | 店舗一覧 (`StoreResponse`) |
| GET | `/stores/{id}` | 端末 / 管理 | 店舗詳細 (`StoreResponse`) |
| POST | `/stores` | 管理 | 登録 (`StoreCreateRequest`) |
| PUT | `/stores/{id}` | 管理 | 更新 (`StoreUpdateRequest`) |
| DELETE | `/stores/{id}` | 管理 | 論理削除 (端末・在庫が残っていれば 422) |

### 3.3 レジ端末 (Terminals)

| フィールド | 型 | 説明 |
| --- | --- | --- |
| `id` | guid | |
| `storeId` | guid | |
| `terminalNo` | int | 店舗内の端末番号 (店舗内で一意)。レシート番号の一部 |
| `name` | string(50) | |
| `lastReceiptSeq` | int | サーバが把握している最終レシート連番。端末再セットアップ時の復元用 |
| `lastSeenAt` | datetime? | 最終通信時刻 (サーバ付与) |
| `appVersion` | string(20)? | 端末アプリのバージョン (端末が送信) |
| `isActive`, `isDeleted`, `createdAt`, `updatedAt`, `version` | | |

| Method | Path | 用途 | 概要 |
| --- | --- | --- | --- |
| GET | `/terminals?storeId&updatedSince&includeDeleted&page&size` | 端末 / 管理 | 端末一覧 |
| GET | `/terminals/{id}` | 端末 / 管理 | 端末詳細 |
| POST | `/terminals` | 管理 | 登録 |
| PUT | `/terminals/{id}` | 管理 | 更新 |
| DELETE | `/terminals/{id}` | 管理 | 論理削除 (開設中シフトがあれば 422) |

`TerminalCreateRequest` / `TerminalUpdateRequest` は `storeId` / `terminalNo` / `name` / `isActive` (+ `version`)。  
`lastReceiptSeq` は取引登録で、`lastSeenAt` / `appVersion` は端末の通信でサーバが更新する。

端末のセットアップは管理画面が表示する **設定 QR** (`ApiEndPoint` / `StoreId` / `TerminalId`、[D-24](decisions.md#d-24-端末セットアップ-qr-テンプレート互換フォーマット)) を端末で読み取る。

### 3.4 スタッフ (Staff)

| フィールド | 型 | 説明 |
| --- | --- | --- |
| `id` | guid | |
| `code` | string(20) | スタッフコード (一意)。ログイン・レシート印字用 |
| `name` | string(50) | |
| `role` | enum | `Cashier` / `Manager` / `Admin`。表示と承認者の選択に使う (サーバは認可に使わない) |
| `storeId` | guid? | 所属店舗。`null` = 本部 (全店) |
| `isActive`, `isDeleted`, `createdAt`, `updatedAt`, `version` | | |

| Method | Path | 用途 | 概要 |
| --- | --- | --- | --- |
| GET | `/staff?storeId&updatedSince&includeDeleted&page&size` | 端末 / 管理 | スタッフ一覧 (端末のログイン画面用。PIN は含めない) |
| GET | `/staff/{id}` | 端末 / 管理 | スタッフ詳細 |
| POST | `/staff` | 管理 | 登録 |
| PUT | `/staff/{id}` | 管理 | 更新 |
| DELETE | `/staff/{id}` | 管理 | 論理削除 |

### 3.5 部門 (Categories)

家電量販店・ホームセンターの分類 (例: 「テレビ・レコーダー > テレビ」) を 2 階層まで想定。

| フィールド | 型 | 説明 |
| --- | --- | --- |
| `id` | guid | |
| `code` | string(20) | 部門コード (一意) |
| `name` | string(100) | |
| `parentId` | guid? | 親部門。`null` = 最上位 |
| `sortOrder` | int | |
| `isDeleted`, `createdAt`, `updatedAt`, `version` | | |

| Method | Path | 用途 | 概要 |
| --- | --- | --- | --- |
| GET | `/categories?updatedSince&includeDeleted&page&size` | 端末 / 管理 | 部門一覧 (階層は `parentId` で組み立てる) |
| GET | `/categories/{id}` | 管理 | 部門詳細 |
| POST | `/categories` | 管理 | 登録 |
| PUT | `/categories/{id}` | 管理 | 更新 |
| DELETE | `/categories/{id}` | 管理 | 論理削除 (所属商品があれば 422) |

### 3.6 税率 (TaxRates)

| フィールド | 型 | 説明 |
| --- | --- | --- |
| `id` | guid | |
| `code` | string(10) | `STD` / `RED` / `EXEMPT` |
| `name` | string(50) | 「標準税率 10%」など |
| `rate` | rate | `0.10` / `0.08` / `0` |
| `kind` | enum | `Standard` / `Reduced` (軽減) / `Exempt` (非課税・不課税) |
| `isDefault` | bool | 商品登録時の既定 |
| `sortOrder`, `isDeleted`, `createdAt`, `updatedAt`, `version` | | |

| Method | Path | 用途 | 概要 |
| --- | --- | --- | --- |
| GET | `/tax-rates?updatedSince&includeDeleted` | 端末 / 管理 | 税率一覧 (少数なのでページングなし) |
| POST | `/tax-rates` | 管理 | 登録 |
| PUT | `/tax-rates/{id}` | 管理 | 更新 (取引には税率のスナップショットが残るので過去取引は影響しない) |
| DELETE | `/tax-rates/{id}` | 管理 | 論理削除 (使用中商品があれば 422) |

### 3.7 商品 (Products)

1 商品 = 1 SKU = 1 JAN。  
バリエーション (色・サイズの親子) は持たない ([D-00](decisions.md#d-00-業種前提))。

| フィールド | 型 | 説明 |
| --- | --- | --- |
| `id` | guid | |
| `code` | string(20) | 自社商品コード (一意) |
| `barcode` | string(20)? | JAN / EAN (一意、`null` 可) |
| `name` | string(100) | |
| `kana` | string(100)? | 検索用よみ |
| `brand` | string(50)? | メーカー |
| `modelNo` | string(50)? | 型番 |
| `categoryId` | guid | 部門 |
| `kind` | enum | `Goods` (物品) / `Service` (配送料・延長保証・設置工事など、在庫を持たない) |
| `price` | money | 販売価格 |
| `taxIncluded` | bool | `price` が税込か (内税 = true / 外税 = false) |
| `taxRateId` | guid | 税率 |
| `cost` | money? | 原価 (粗利レポート用) |
| `pointRate` | rate | ポイント還元率 (`0.10` = 10%、`0` = 対象外) |
| `requiresSerial` | bool | 販売時にシリアル番号入力を求める ([D-04](decisions.md#d-04-シリアル番号-製造番号)) |
| `trackInventory` | bool | 在庫管理対象 (`Service` は false) |
| `allowsPriceOverride` | bool | 売価変更可 (オープン価格・配送料など) |
| `unit` | string(10)? | 単位 (個 / 本 / m) |
| `imageUrl` | string? | 画像 URL (未使用。`ProductCreateRequest` / `ProductUpdateRequest` には含めない) |
| `isActive` | bool | 販売可否 (false = 販売停止だがマスタは残す) |
| `isDeleted`, `createdAt`, `updatedAt`, `version` | | |

| Method | Path | 用途 | 概要 |
| --- | --- | --- | --- |
| GET | `/products?categoryId&keyword&isActive&updatedSince&includeDeleted&sort&desc&page&size` | 端末 / 管理 | 商品一覧。`keyword` は code / barcode / name / kana / modelNo の部分一致。`sort` は code / name / price / updatedAt |
| GET | `/products/{id}` | 端末 / 管理 | 商品詳細 |
| GET | `/products/lookup?barcode=` または `?code=` | 端末 | スキャン用 1 件取得。見つからなければ 404 |
| POST | `/products` | 管理 | 登録 |
| PUT | `/products/{id}` | 管理 | 更新 |
| DELETE | `/products/{id}` | 管理 | 論理削除 |
| GET | `/products/csv` | 管理 | CSV 出力 (BOM 付き UTF-8、削除済みを除く全件) |

### 3.8 値引 (Discounts)

定義済み値引。  
任意額の値引 (家電の値引き交渉) は取引側で `discountId = null` として登録できる。

| フィールド | 型 | 説明 |
| --- | --- | --- |
| `id` | guid | |
| `code` | string(20) | 一意 |
| `name` | string(50) | 「社員割引 10%」など |
| `type` | enum | `Amount` (定額) / `Percent` (定率) |
| `value` | decimal | 金額 (円) または率 (`0.10`) |
| `scope` | enum | `Line` (明細) / `Transaction` (取引全体) |
| `requiresApproval` | bool | 承認要 (端末が承認者を選んで `approvedByStaffId` に入れる。サーバは検証しない) |
| `isActive`, `sortOrder`, `isDeleted`, `createdAt`, `updatedAt`, `version` | | |

| Method | Path | 用途 | 概要 |
| --- | --- | --- | --- |
| GET | `/discounts?updatedSince&includeDeleted` | 端末 / 管理 | 値引一覧 |
| POST / PUT / DELETE | `/discounts`, `/discounts/{id}` | 管理 | 登録 / 更新 / 論理削除 |

### 3.9 支払方法 (PaymentMethods)

| フィールド | 型 | 説明 |
| --- | --- | --- |
| `id` | guid | |
| `code` | string(20) | 一意 |
| `name` | string(50) | |
| `shortName` | string(10)? | 端末の支払ボタンに出す短い名前 (「クレカ」など。省略時は `name`) |
| `kind` | enum | `Cash` / `Card` / `Qr` / `EMoney` / `Voucher` (商品券) / `Points` / `Credit` (掛売) / `Other` |
| `allowsChange` | bool | 釣銭あり (預り金 > 充当額 を許可)。通常 `Cash` のみ true |
| `requiresReference` | bool | 伝票番号など参照の入力を求める (カードなど) |
| `isActive`, `sortOrder`, `isDeleted`, `createdAt`, `updatedAt`, `version` | | |

`kind = Points` の支払方法をちょうど 1 件持つ (ポイント充当用、[D-07](decisions.md#d-07-ポイント制度))。

| Method | Path | 用途 | 概要 |
| --- | --- | --- | --- |
| GET | `/payment-methods?updatedSince&includeDeleted` | 端末 / 管理 | 支払方法一覧 |
| POST / PUT / DELETE | `/payment-methods`, `/payment-methods/{id}` | 管理 | 登録 / 更新 / 論理削除 |

### 3.10 マスタ同期 (Sync)

端末が起動時・定期的に呼ぶ。  
個別の一覧 API でも同じことはできるが、往復を 1 回にする。

| Method | Path | 用途 | 概要 |
| --- | --- | --- | --- |
| GET | `/sync/masters?since=` | 端末 | `since` 以降に更新されたマスタをまとめて返す (`SyncMastersResponse`)。省略時は全件 |

```jsonc
{
  "serverTime": "2026-09-11T03:00:00.000Z",   // 次回の since に使う
  "settings": { /* 3.1 */ },                  // 変更があるときのみ
  "stores": [ /* 3.2 */ ],
  "terminals": [ /* 3.3 */ ],
  "staff": [ /* 3.4 */ ],
  "categories": [ /* 3.5 */ ],
  "taxRates": [ /* 3.6 */ ],
  "products": [ /* 3.7 */ ],                  // 論理削除済みも isDeleted: true で含む
  "discounts": [ /* 3.8 */ ],
  "paymentMethods": [ /* 3.9 */ ],
  "adjustmentReasons": [ /* 3.14 */ ]
}
```

> 商品件数が多い場合 (数万件) は `products` を空にして `GET /products?updatedSince&page&size` で分割取得する運用にできるよう、応答に `productsTruncated: true` を持たせる (実装は任意)。

### 3.11 顧客・ポイント (Customers)

| フィールド | 型 | 説明 |
| --- | --- | --- |
| `id` | guid | |
| `code` | string(20) | 会員番号 (一意)。会員証バーコード |
| `name`, `kana` | string(100) | |
| `phone`, `email` | string? | |
| `postalCode`, `address` | string? | 配送先の既定値にも使う |
| `birthDate` | date? | |
| `pointBalance` | int | ポイント残高 (サーバ計算。更新 API では変更不可) |
| `note` | string(500)? | |
| `isDeleted`, `createdAt`, `updatedAt`, `version` | | |

`CustomerPointHistoryResponse` (ポイント履歴):

| フィールド | 型 | 説明 |
| --- | --- | --- |
| `id` | guid | |
| `customerId` | guid | |
| `type` | enum | `Earn` (付与) / `Redeem` (利用) / `Revoke` (返品による付与取消) / `Refund` (返品による利用分返還) / `Void` (取引取消による戻し) / `Adjust` (手動調整) |
| `points` | int | 符号付き増減 |
| `balanceAfter` | int | 処理後残高 |
| `transactionId` | guid? | 関連取引 |
| `reason` | string? | 手動調整の理由 |
| `staffId` | guid? | |
| `occurredAt` | datetime | |

| Method | Path | 用途 | 概要 |
| --- | --- | --- | --- |
| GET | `/customers?keyword&code&phone&updatedSince&sort&desc&page&size` | 端末 / 管理 | 顧客検索。`keyword` は code / name / kana / phone の部分一致 |
| GET | `/customers/lookup?code=` | 端末 | 会員証スキャン用 1 件取得 |
| GET | `/customers/{id}` | 端末 / 管理 | 顧客詳細 (残高含む) |
| POST | `/customers` | 端末 / 管理 | 登録 (店頭での新規入会も想定) |
| PUT | `/customers/{id}` | 端末 / 管理 | 更新 |
| DELETE | `/customers/{id}` | 管理 | 論理削除 |
| GET | `/customers/{id}/points/history?page&size` | 端末 / 管理 | ポイント履歴 (新しい順、`CustomerPointHistoryResponse`) |
| POST | `/customers/{id}/points/adjust` | 管理 | 手動調整 `CustomerPointAdjustRequest { points, reason, staffId }`。`Adjust` 履歴を作る |
| GET | `/customers/{id}/transactions?page&size` | 端末 / 管理 | 購入履歴 (新しい順) |

### 3.12 取引 (Transactions)

販売 (`Sale`) と返品 (`Return`) を同じ形で扱う。  
端末が計算した結果をそのまま送り (`TransactionCreateRequest`)、サーバは [§4](#4-金額税ポイント計算仕様-共有ライブラリ) の仕様で再計算して検証し、`TransactionResponse` を返す。

#### 取引の項目

「入力」= 端末が決める値、「計算」= 端末が計算しサーバが検証する値、「サーバ」= サーバ付与 (`TransactionResponse` にのみ含まれる)。

| フィールド | 型 | 区分 | 説明 |
| --- | --- | --- | --- |
| `id` | guid | 入力 | 端末採番 |
| `type` | enum | 入力 | `Sale` / `Return` |
| `status` | enum | 入力 | `Completed` / `Voided`。オフライン中に取消した取引は `Voided` + `void` 付きで送れる |
| `storeId`, `terminalId`, `staffId`, `shiftId` | guid | 入力 | シフトは `Open` で端末が一致すること |
| `customerId` | guid? | 入力 | ポイント付与・利用時は必須 |
| `receiptNo` | string(20) | 入力 | `{店舗コード}-{端末番号:00}-{連番:000000}`。全体で一意 ([D-15](decisions.md#d-15-レシート番号-端末採番)) |
| `businessDate` | date | 入力 | 営業日 (シフトと同じ) |
| `transactedAt` | datetime | 入力 | 会計完了時刻 |
| `originalTransactionId` | guid? | 入力 | `Return` のとき必須。元取引 (`Sale`、`Completed`) |
| `lines[]` | | | 明細 (下記) |
| `discounts[]` | | | 値引 (下記)。`lineId = null` が取引値引 |
| `taxSummaries[]` | | 計算 | 税率ごとの集計 (下記) |
| `subtotal` | money | 計算 | Σ `lines.amount` (値引前) |
| `discountTotal` | money | 計算 | Σ 明細値引 + Σ 取引値引 |
| `netSubtotal` | money | 計算 | `subtotal − discountTotal` = Σ `lines.netAmount` |
| `taxTotal` | money | 計算 | Σ `taxSummaries.taxAmount` (内税分も含む) |
| `total` | money | 計算 | `netSubtotal` + 外税分の税額。お会計金額 |
| `payments[]` | | | 支払 (下記)。`Return` では返金方法 |
| `tenderedTotal` | money | 計算 | Σ `payments.tenderedAmount` |
| `changeAmount` | money | 計算 | `tenderedTotal − total` (現金でのみ発生) |
| `pointsEarned` | int | 計算 | 付与ポイント。`Return` では取消分を負で持つ |
| `pointsRedeemed` | int | 計算 | 利用ポイント (= `Points` 支払の合計)。`Return` では返還分を負で持つ |
| `pointsBalanceAfter` | int? | サーバ | 処理後残高 |
| `delivery` | object? | 入力 | 配送情報 (下記、[D-08](decisions.md#d-08-配送情報)) |
| `note` | string(500)? | 入力 | |
| `void` | object? | 入力 / サーバ | `{ voidedAt, voidedByStaffId, reason }` |
| `warnings[]` | `{ code, message, lineId? }[]` | サーバ | 受理したが確認が必要な事項 ([§5](#5-エラーコード) の警告コード) |
| `createdAt`, `updatedAt` | datetime | サーバ | |

`lines[]` (`TransactionCreateRequestLine` / `TransactionResponseLine`):

| フィールド | 型 | 区分 | 説明 |
| --- | --- | --- | --- |
| `id` | guid | 入力 | 端末採番 |
| `lineNo` | int | 入力 | 1 始まり |
| `productId` | guid | 入力 | |
| `productCode`, `productName`, `categoryId`, `kind` | | 入力 | 販売時点のスナップショット |
| `listPrice` | money | 入力 | 販売時点のマスタ価格 |
| `unitPrice` | money | 入力 | 適用単価。`allowsPriceOverride` の商品以外は `listPrice` と一致すること |
| `quantity` | qty | 入力 | 正の数 |
| `taxRateId`, `taxRate`, `taxIncluded` | | 入力 | 販売時点のスナップショット |
| `pointRate` | rate | 入力 | 販売時点のスナップショット |
| `amount` | money | 計算 | `unitPrice × quantity` |
| `discountAmount` | money | 計算 | この明細を対象とする値引の合計 |
| `allocatedDiscountAmount` | money | 計算 | 取引値引の按分額 |
| `netAmount` | money | 計算 | `amount − discountAmount − allocatedDiscountAmount` |
| `pointsRedeemed` | int | 計算 | 利用ポイントの按分 (付与計算の控除用) |
| `pointsEarned` | int | 計算 | 付与ポイント |
| `serialNumbers[]` | string[] | 入力 | シリアル番号 (`requiresSerial` の商品では端末が入力を求める) |
| `originalLineId` | guid? | 入力 | `Return` のとき必須。元取引の明細 |
| `returnedQuantity` | qty | サーバ | 返品済み数量 (元取引側で更新) |
| `note` | string? | 入力 | |

`discounts[]`:

| フィールド | 型 | 区分 | 説明 |
| --- | --- | --- | --- |
| `id` | guid | 入力 | |
| `lineId` | guid? | 入力 | 対象明細。`null` = 取引値引 |
| `discountId` | guid? | 入力 | 定義済み値引。`null` = 任意値引 |
| `name` | string(50) | 入力 | |
| `type` | enum | 入力 | `Amount` / `Percent` |
| `value` | decimal | 入力 | |
| `amount` | money | 計算 | 値引額 |
| `reason` | string? | 入力 | 任意値引の理由 |
| `approvedByStaffId` | guid? | 入力 | 承認者 (`requiresApproval` の値引のとき端末が設定する) |

`taxSummaries[]` (キー: `taxRateId` + `taxIncluded`):

| フィールド | 型 | 説明 |
| --- | --- | --- |
| `taxRateId`, `rate`, `taxIncluded` | | |
| `taxableAmount` | money | 対象明細の `netAmount` 合計 (内税なら税込額、外税なら税抜額) |
| `taxAmount` | money | 税額 |

`payments[]`:

| フィールド | 型 | 区分 | 説明 |
| --- | --- | --- | --- |
| `id` | guid | 入力 | |
| `seqNo` | int | 入力 | |
| `paymentMethodId`, `kind` | | 入力 | スナップショット |
| `amount` | money | 入力 | 充当額。Σ = `total` |
| `tenderedAmount` | money | 入力 | 預り額。`allowsChange` の方法以外は `amount` と同じ |
| `reference` | string(50)? | 入力 | カード伝票番号など |
| `note` | string(200)? | 入力 | |

`delivery`:

| フィールド | 型 | 説明 |
| --- | --- | --- |
| `recipientName`, `phone`, `postalCode`, `address` | string | 配送先 |
| `requestedDate` | date? | 希望日 |
| `timeSlot` | string(20)? | 時間帯 |
| `note` | string? | |

#### 例: 販売取引

会員 (残高 6,000 pt) に、デジカメ 80,000 円 (還元 10%、5% 値引) + SD カード 2,000 円 × 2 (還元 1%) + 配送料 1,100 円。  
取引値引 1,000 円。  
5,000 pt 利用、クレジット 50,000 円、残りを現金 30,000 円で。  
すべて内税 10%、税は切り捨て。  
計算過程は [§4.6](#46-計算例)。

```jsonc
POST /api/v1/transactions      (TransactionCreateRequest)
{
  "id": "0192a1b2-...", "type": "Sale", "status": "Completed",
  "storeId": "...", "terminalId": "...", "staffId": "...", "shiftId": "...",
  "customerId": "...", "receiptNo": "S001-02-000123",
  "businessDate": "2026-09-11", "transactedAt": "2026-09-11T03:15:00.000Z",
  "lines": [
    { "id": "...", "lineNo": 1, "productId": "...", "productCode": "4901234567894", "productName": "デジタルカメラ X-100",
      "categoryId": "...", "kind": "Goods", "listPrice": 80000, "unitPrice": 80000, "quantity": 1,
      "taxRateId": "...", "taxRate": 0.10, "taxIncluded": true, "pointRate": 0.10,
      "amount": 80000, "discountAmount": 4000, "allocatedDiscountAmount": 937, "netAmount": 75063,
      "pointsRedeemed": 4685, "pointsEarned": 7037, "serialNumbers": ["SN-0001234"] },
    { "id": "...", "lineNo": 2, "productId": "...", "productCode": "4901234567900", "productName": "SD カード 64GB",
      "categoryId": "...", "kind": "Goods", "listPrice": 2000, "unitPrice": 2000, "quantity": 2,
      "taxRateId": "...", "taxRate": 0.10, "taxIncluded": true, "pointRate": 0.01,
      "amount": 4000, "discountAmount": 0, "allocatedDiscountAmount": 49, "netAmount": 3951,
      "pointsRedeemed": 247, "pointsEarned": 37, "serialNumbers": [] },
    { "id": "...", "lineNo": 3, "productId": "...", "productCode": "SVC-DELIVERY", "productName": "配送料",
      "categoryId": "...", "kind": "Service", "listPrice": 1100, "unitPrice": 1100, "quantity": 1,
      "taxRateId": "...", "taxRate": 0.10, "taxIncluded": true, "pointRate": 0,
      "amount": 1100, "discountAmount": 0, "allocatedDiscountAmount": 14, "netAmount": 1086,
      "pointsRedeemed": 68, "pointsEarned": 0, "serialNumbers": [] }
  ],
  "discounts": [
    { "id": "...", "lineId": "<line1>", "discountId": "...", "name": "展示品 5%", "type": "Percent", "value": 0.05, "amount": 4000 },
    { "id": "...", "lineId": null, "discountId": null, "name": "端数値引", "type": "Amount", "value": 1000, "amount": 1000, "reason": "交渉" }
  ],
  "taxSummaries": [
    { "taxRateId": "...", "rate": 0.10, "taxIncluded": true, "taxableAmount": 80100, "taxAmount": 7281 }
  ],
  "subtotal": 85100, "discountTotal": 5000, "netSubtotal": 80100, "taxTotal": 7281, "total": 80100,
  "payments": [
    { "id": "...", "seqNo": 1, "paymentMethodId": "...", "kind": "Points", "amount": 5000, "tenderedAmount": 5000 },
    { "id": "...", "seqNo": 2, "paymentMethodId": "...", "kind": "Card",   "amount": 50000, "tenderedAmount": 50000, "reference": "1234-5678" },
    { "id": "...", "seqNo": 3, "paymentMethodId": "...", "kind": "Cash",   "amount": 25100, "tenderedAmount": 30000 }
  ],
  "tenderedTotal": 85000, "changeAmount": 4900,
  "pointsEarned": 7074, "pointsRedeemed": 5000,
  "delivery": { "recipientName": "山田 太郎", "phone": "090-0000-0000", "postalCode": "100-0001",
                "address": "東京都千代田区...", "requestedDate": "2026-09-14", "timeSlot": "14-16" }
}
```

応答 `201 Created` (`TransactionResponse`) は同じ形に `pointsBalanceAfter: 8074`、`createdAt`、`updatedAt` が付く。

#### エンドポイント

| Method | Path | 用途 | 概要 |
| --- | --- | --- | --- |
| POST | `/transactions` | 端末 | 取引登録 (`TransactionCreateRequest`)。`201` 新規 / `200` 同一 `id` 既存 / `409` 同一 `id` で内容相違 / `422` 検証エラー |
| GET | `/transactions?storeId&terminalId&staffId&shiftId&customerId&from&to&type&status&page&size` | 端末 / 管理 | 取引検索 (`transactedAt` 降順、`TransactionResponse`) |
| GET | `/transactions/{id}` | 端末 / 管理 | 取引詳細 (`TransactionResponse`) |
| GET | `/transactions/lookup?receiptNo=` | 端末 | 返品時のレシート番号検索 |
| POST | `/transactions/{id}/void` | 端末 | 取消 `TransactionVoidRequest { staffId, reason, voidedAt }` → `200` 取引 |
| POST | `/transactions/calculate` | 端末 / 管理 | 入力項目 (`type`, `originalTransactionId`, `lines[]`, `discounts[]`, `payments[]`) を送り (`TransactionCalculateRequest`)、計算項目 (`TransactionCalculateResponse`) を返す (登録しない)。共有ライブラリの検証用 |

#### 業務ルール

**販売 (`type = Sale`)**

1. `shiftId` のシフトが存在し、`Open` で、`terminalId` が一致すること (`SHIFT_NOT_FOUND` / `SHIFT_CLOSED` / `SHIFT_TERMINAL_MISMATCH`)
2. `receiptNo` が一意であること (`DUPLICATE_RECEIPT_NO`)
3. 各明細の `productId` が存在すること (`PRODUCT_NOT_FOUND`)。  
   `isActive = false` はオフライン同期の遅れがあり得るので受理する
4. `allowsPriceOverride = false` の商品は `unitPrice = listPrice` であること (`PRICE_OVERRIDE_NOT_ALLOWED`)
5. [§4](#4-金額税ポイント計算仕様-共有ライブラリ) で再計算した結果と、送信された計算項目が一致すること (`CALCULATION_MISMATCH`。応答の `expected` にサーバ計算結果)
6. Σ `payments.amount = total`、`changeAmount = tenderedTotal − total ≥ 0`、釣銭が出るのは `allowsChange` の支払方法のみ (`PAYMENT_MISMATCH`)
7. `pointsEarned > 0` または `pointsRedeemed > 0` のとき `customerId` が必須 (`CUSTOMER_REQUIRED`)
8. ポイント残高不足は**受理して警告**にとどめる (取引は店頭で成立済み)。  
   残高は負になり得るので管理画面で確認できるようにする
9. 登録時の副作用: `trackInventory` の明細ごとに在庫変動 (`Sale`, −数量)、ポイント履歴 (`Redeem` → `Earn` の順)、`terminals.lastReceiptSeq` 更新

**返品 (`type = Return`)**

1. `originalTransactionId` の取引が `Sale` かつ `Completed` であること (`ORIGINAL_NOT_FOUND` / `ORIGINAL_NOT_RETURNABLE`)
2. 各明細の `originalLineId` が元取引の明細であり、`quantity ≤ 元数量 − returnedQuantity` (`RETURN_QUANTITY_EXCEEDED`)
3. 明細金額・値引・ポイントは元明細から [§4.5](#45-返品) の式で導出した値と一致すること
4. `payments` は返金方法。  
   Σ `amount = total`、`tenderedAmount = amount`、`changeAmount = 0`
5. 副作用: 在庫変動 (`Return`, +数量)、ポイント履歴 (`Revoke` / `Refund`)、元明細の `returnedQuantity` 加算

**取消 (`POST /transactions/{id}/void`)**

1. 対象が `Completed` で、そのシフトが `Open` であること (精算後は取消不可、返品で対応) (`SHIFT_CLOSED`)
2. `Sale` に返品が紐付いていれば取消不可 (`HAS_RETURNS`)
3. 副作用: 在庫変動 (`Void`, 逆方向)、ポイント履歴 (`Void`)、`Return` の取消なら元明細の `returnedQuantity` を戻す。  
   取消済み取引は集計から除外

### 3.13 レジ開閉・現金管理 (Shifts)

端末ごとの「開設 → 販売 → 入出金 → 精算」の単位。

#### シフトの項目 (`ShiftResponse`)

| フィールド | 型 | 区分 | 説明 |
| --- | --- | --- | --- |
| `id` | guid | 入力 | 端末採番 |
| `storeId`, `terminalId` | guid | 入力 | |
| `status` | enum | サーバ | `Open` / `Closed` |
| `businessDate` | date | 入力 | 営業日 (`businessDayStartTime` で端末が判定) |
| `openedAt`, `openedByStaffId` | | 入力 | |
| `openingCash` | money | 入力 | 釣銭準備金 |
| `closedAt`, `closedByStaffId` | | 入力 (精算時) | |
| `actualCash` | money? | 入力 (精算時) | 実査金額 |
| `denominations[]` | `{ denomination, count }[]` | 入力 (精算時) | 金種別枚数 (任意) |
| `expectedCash` | money? | サーバ | `openingCash + cashSales − cashReturns + paidIn − paidOut` |
| `difference` | money? | サーバ | `actualCash − expectedCash` |
| `totals` | object | サーバ | `{ cashSales, cashReturns, paidIn, paidOut, salesCount, returnCount, voidCount, salesTotal, returnsTotal }` (取消済みを除く) |
| `note` | string? | 入力 | |
| `createdAt`, `updatedAt` | | サーバ | |

`ShiftCashEventResponse` (入出金):

| フィールド | 型 | 説明 |
| --- | --- | --- |
| `id` | guid | 端末採番 |
| `shiftId` | guid | |
| `type` | enum | `PaidIn` (入金) / `PaidOut` (出金: 両替・銀行入金など) / `NoSale` (ドロワ開、`amount = 0`) |
| `amount` | money | |
| `reason` | string(100)? | |
| `staffId` | guid | |
| `occurredAt` | datetime | |

#### エンドポイント

| Method | Path | 用途 | 概要 |
| --- | --- | --- | --- |
| POST | `/shifts` | 端末 | 開設 (`ShiftOpenRequest`)。端末に `Open` のシフトがあれば `409` (`TERMINAL_HAS_OPEN_SHIFT`) |
| GET | `/shifts/current?terminalId=` | 端末 | 端末の開設中シフト (なければ 404) |
| GET | `/shifts?storeId&terminalId&status&from&to&page&size` | 管理 | シフト一覧 |
| GET | `/shifts/{id}` | 端末 / 管理 | シフト詳細 (集計付き) |
| POST | `/shifts/{id}/cash-events` | 端末 | 入出金登録 (`ShiftCashEventRequest`、`Open` のみ) |
| GET | `/shifts/{id}/cash-events` | 端末 / 管理 | 入出金一覧 |
| POST | `/shifts/{id}/close` | 端末 | 精算 `ShiftCloseRequest { closedAt, closedByStaffId, actualCash, denominations, note }` → `200` シフト (`expectedCash` / `difference` 確定) |
| GET | `/shifts/{id}/summary` | 端末 / 管理 | 精算レポート (`ShiftSummaryResponse`、下記) |
| GET | `/shifts/{id}/summary/pdf` | 管理 | 精算レポートの PDF ([D-37](decisions.md#d-37-帳票出力-pdf-oysterreport)) |

`ShiftSummaryResponse` (精算レポート):

```jsonc
{
  "shift": { /* ShiftResponse */ },
  "byPaymentMethod": [ { "paymentMethodId": "...", "name": "現金", "kind": "Cash",
                         "salesAmount": 125000, "salesCount": 40, "returnAmount": 2000, "returnCount": 1 } ],
  "byTaxRate":       [ { "taxRateId": "...", "rate": 0.10, "taxIncluded": true, "taxableAmount": 300000, "taxAmount": 27272 } ],
  "byCategory":      [ { "categoryId": "...", "name": "カメラ", "quantity": 12, "netAmount": 180000 } ],
  "points":          { "earned": 15000, "redeemed": 4000 },
  "cash":            { "openingCash": 30000, "cashSales": 125000, "cashReturns": 2000, "paidIn": 0, "paidOut": 10000,
                       "expectedCash": 143000, "actualCash": 142900, "difference": -100 }
}
```

#### 業務ルール

- 端末につき `Open` のシフトは同時に 1 つ
- 取引・入出金は `Open` のシフトにのみ登録できる。  
  **端末は精算要求の前に、そのシフトの取引・入出金をすべて送信し終えていること** ([§6](#6-端末側の同期フロー))
- 精算後の再開はしない (翌営業日は新しいシフト)。  
  精算後の訂正は返品または手動調整で行う

### 3.14 在庫 (Inventory)

現在庫 (`InventoryLevelResponse`) と変動履歴 (`InventoryChangeResponse`)。  
取引による変動はサーバが自動生成し、端末からは棚卸・調整だけを送る ([D-12](decisions.md#d-12-在庫-変動履歴ベース))。

| `InventoryLevelResponse` | 型 | 説明 |
| --- | --- | --- |
| `storeId`, `productId` | guid | 複合キー |
| `quantity` | qty | 現在庫 (負も許容し、要確認として扱う) |
| `updatedAt` | datetime | |

| `InventoryChangeResponse` | 型 | 説明 |
| --- | --- | --- |
| `id` | guid | 端末採番 (棚卸・調整) / サーバ採番 (取引由来) |
| `storeId`, `productId` | guid | |
| `type` | enum | `Sale` / `Return` / `Void` (取引由来) / `PhysicalCount` (棚卸) / `Adjustment` (調整) |
| `quantityDelta` | qty | 増減 (符号付き) |
| `quantityAfter` | qty | 処理後在庫 (サーバ計算) |
| `reasonId` | guid? | 調整理由 (`adjustmentReasons`) |
| `reason` | string? | 自由記述 |
| `referenceType`, `referenceId`, `referenceLineId` | | 取引由来なら `Transaction` + 取引 ID + 明細 ID |
| `staffId` | guid? | |
| `occurredAt`, `createdAt` | datetime | |

| Method | Path | 用途 | 概要 |
| --- | --- | --- | --- |
| GET | `/inventory?storeId&productId&categoryId&negativeOnly&updatedSince&page&size` | 端末 / 管理 | 現在庫一覧 (端末は自店分を差分同期) |
| GET | `/inventory/{productId}` | 端末 / 管理 | 商品の**全店舗**在庫 (他店在庫照会) `{ productId, levels: [{ storeId, storeName, quantity, updatedAt }] }` |
| POST | `/inventory/changes` | 端末 / 管理 | 棚卸・調整の一括登録 (`InventoryChangeRequest`、下記) |
| GET | `/inventory/changes?storeId&productId&type&from&to&page&size` | 端末 / 管理 | 変動履歴 |
| GET | `/inventory/adjustment-reasons?updatedSince&includeDeleted` | 端末 / 管理 | 調整理由一覧 (破損 / 廃棄 / 万引き / 自家消費 / 棚卸差異 ...) |
| POST / PUT / DELETE | `/inventory/adjustment-reasons`, `.../{id}` | 管理 | 登録 / 更新 / 論理削除 |

`POST /inventory/changes`:

```jsonc
// 要求 (InventoryChangeRequest)
{ "changes": [
    { "id": "...", "storeId": "...", "productId": "...", "type": "PhysicalCount", "quantity": 12,     // 実数
      "staffId": "...", "occurredAt": "..." },
    { "id": "...", "storeId": "...", "productId": "...", "type": "Adjustment",    "quantity": -1,     // 増減
      "reasonId": "...", "reason": "展示品破損", "staffId": "...", "occurredAt": "..." } ] }
// 応答 200 (InventoryChangeResultResponse。要素ごとに結果)
{ "results": [ { "id": "...", "status": "Created",   "quantityDelta": 2, "quantityAfter": 12 },
               { "id": "...", "status": "Duplicate", "quantityDelta": -1, "quantityAfter": 11 } ] }
```

- `PhysicalCount` は絶対数量。  
  `quantityDelta = quantity − 現在庫` をサーバが計算する
- 同じ `id` は `Duplicate` として既存の結果を返す

### 3.15 レポート (Reports)

取引テーブルからの集計。  
取消済みは除外し、返品は負として扱う。  
管理画面用だが端末の売上照会にも使える。

| Method | Path | 用途 | 概要 |
| --- | --- | --- | --- |
| GET | `/reports/sales/summary?storeId&from&to&groupBy=` | 管理 / 端末 | 売上集計 (`ReportSalesSummaryResponse`)。`groupBy` = `day` / `store` / `hour` / `terminal` / `staff` / `paymentMethod` / `taxRate` / `category` (不正なら 400) |
| GET | `/reports/sales/products?storeId&from&to&categoryId&sort=netSales\|quantity&size` | 管理 | 商品別売上 (`ReportProductSalesResponse`) |
| GET | `/reports/sales/summary/csv`, `/reports/sales/products/csv` | 管理 | CSV 出力 (同じクエリ、CsvHelper、BOM 付き UTF-8。summary は合計行付き) |
| GET | `/reports/sales/daily/pdf?storeId&date` | 管理 | 売上日報の PDF (店舗 × 営業日、[D-37](decisions.md#d-37-帳票出力-pdf-oysterreport)) |

```jsonc
// GET /reports/sales/summary?storeId=...&from=2026-09-01&to=2026-09-11&groupBy=day
{ "from": "2026-09-01", "to": "2026-09-11", "groupBy": "day",
  "rows": [ { "key": "2026-09-01", "label": "2026-09-01",
              "transactionCount": 120, "returnCount": 2, "customerCount": 80,
              "salesTotal": 1500000, "returnsTotal": 30000, "netSales": 1470000,
              "discountTotal": 25000, "taxTotal": 133636, "pointsEarned": 90000, "pointsRedeemed": 20000 } ],
  "total": { /* rows と同じ形 */ } }

// GET /reports/sales/products
{ "rows": [ { "productId": "...", "productCode": "...", "productName": "...", "categoryId": "...", "categoryName": "...",
              "quantitySold": 15, "quantityReturned": 1, "netQuantity": 14,
              "salesTotal": 1200000, "returnsTotal": 80000, "netSales": 1120000, "discountTotal": 5000,
              "grossProfit": 210000 } ] }   // grossProfit は cost がある商品のみ
```

- `groupBy = paymentMethod` の行は支払額ベース (`salesTotal` = 支払充当額、`returnsTotal` = 返金額)
- `groupBy = taxRate` の行は `taxableAmount` / `taxAmount` を追加で持つ

---

## 4. 金額・税・ポイント計算仕様 (共有ライブラリ)

端末とサーバが共有する `Pos.Domain` の計算仕様。  
入力 (単価・数量・値引・支払) から計算項目を決定的に導出する。  
すべて `decimal` で計算し、丸めは以下で明示した箇所のみ行う。

### 4.1 明細

```
amount_i          = unitPrice_i × quantity_i                    (JPY: 結果は Floor で整数化)
discountAmount_i  = Σ 明細値引 (Amount: value / Percent: Floor(amount_i × value))
                    ※ 明細値引の合計は amount_i を超えない (超えれば検証エラー)
```

### 4.2 取引値引の按分

取引値引 (`lineId = null`) の合計 `D` を、明細値引後の金額 `base_i = amount_i − discountAmount_i` に比例して各明細へ按分する。  
**最大剰余法** (Largest remainder) で端数を配る。

```
D                 = Σ 取引値引 (Amount: value / Percent: Floor(Σ base_i × value))
raw_i             = D × base_i / Σ base_i
alloc_i           = Floor(raw_i)
残り (D − Σ alloc_i) を、raw_i − alloc_i の大きい明細から 1 ずつ加算 (同値なら lineNo の小さい順)
allocatedDiscountAmount_i = alloc_i
netAmount_i       = base_i − alloc_i
```

取引値引の合計 `D` は Σ base_i を超えない (超えれば検証エラー)。

### 4.3 税

税率 × 内税/外税 のグループごとに合計してから税額を計算する ([D-11](decisions.md#d-11-税計算-税率ごと一括計算))。  
丸めは会社設定 `taxRounding` (既定 `Floor`)。

```
グループ g = (taxRateId, taxIncluded)
taxableAmount_g   = Σ netAmount_i  (i ∈ g)
taxAmount_g       = 内税: Round(taxableAmount_g × rate / (1 + rate))
                    外税: Round(taxableAmount_g × rate)
subtotal          = Σ amount_i
discountTotal     = Σ discountAmount_i + D
netSubtotal       = Σ netAmount_i
taxTotal          = Σ taxAmount_g
total             = netSubtotal + Σ taxAmount_g (外税グループのみ)
```

明細ごとの税額が必要な場合 (ポイントの税抜基準など) は、グループ税額を `netAmount_i` 比で最大剰余法により按分した `allocatedTax_i` を使う (保存はしない)。

### 4.4 ポイント

1 pt = 1 円。  
利用ポイント `R` = `kind = Points` の支払額合計。  
充当分にはポイントを付けない ([D-07](decisions.md#d-07-ポイント制度))。

```
pointsRedeemed_i  = R を netAmount_i 比で最大剰余法により按分
pointBase_i       = pointBasis = TaxIncluded: 内税明細 netAmount_i / 外税明細 netAmount_i + allocatedTax_i
                    pointBasis = TaxExcluded: 内税明細 netAmount_i − allocatedTax_i / 外税明細 netAmount_i
pointsEarned_i    = Floor((pointBase_i − pointsRedeemed_i) × pointRate_i)   (負になるときは 0)
pointsEarned      = Σ pointsEarned_i
pointsRedeemed    = R
pointsBalanceAfter = 残高 − R + pointsEarned   (サーバ確定)
```

### 4.5 返品

元明細 `o` から数量 `q` (≤ `o.quantity − o.returnedQuantity`) を返品する明細の値。

```
unitPrice                 = o.unitPrice
amount                    = Floor(o.unitPrice × q)
discountAmount            = Floor(o.discountAmount × q / o.quantity)
allocatedDiscountAmount   = Floor(o.allocatedDiscountAmount × q / o.quantity)
netAmount                 = amount − discountAmount − allocatedDiscountAmount
pointsEarned              = −Floor(o.pointsEarned × q / o.quantity)        (付与取消)
pointsRedeemed            = −Floor(o.pointsRedeemed × q / o.quantity)      (利用分返還)
```

税は返品明細から §4.3 と同じ方法で再計算する。  
返品取引の `discounts[]` は持たず (元取引から導出済み)、`payments[]` は返金方法。  
`kind = Points` の返金額 = −`pointsRedeemed`。  
全数量返品なら元取引と同額になる。

### 4.6 計算例

§3.12 の例 (税 `Floor`、ポイント `TaxIncluded`):

| 明細 | amount | 明細値引 | base | 取引値引按分 (1,000) | netAmount |
| --- | ---: | ---: | ---: | ---: | ---: |
| デジカメ | 80,000 | 4,000 (5%) | 76,000 | 937 (raw 937.11) | 75,063 |
| SD カード × 2 | 4,000 | 0 | 4,000 | 49 (raw 49.32) | 3,951 |
| 配送料 | 1,100 | 0 | 1,100 | 14 (raw 13.56 + 剰余 1) | 1,086 |
| 合計 | 85,100 | 4,000 | 81,100 | 1,000 | **80,100** |

- 税 (内税 10%): `Floor(80,100 × 10 / 110)` = **7,281**。  
  `total` = 80,100
- ポイント利用 5,000 の按分: 4,685 (raw 4,685.58) / 247 (raw 246.63 + 1) / 68 (raw 67.79 + 1)
- 付与: デジカメ `Floor((75,063 − 4,685) × 0.10)` = 7,037、SD `Floor((3,951 − 247) × 0.01)` = 37、配送料 0 → **7,074**
- 支払: ポイント 5,000 + カード 50,000 + 現金 25,100 (預り 30,000) → `tenderedTotal` 85,000、`changeAmount` **4,900**
- 残高: 6,000 − 5,000 + 7,074 = **8,074**

この例は `Pos.Domain.Tests` のケースにする。

---

## 5. エラーコード

| HTTP | `errorCode` | 発生箇所 |
| --- | --- | --- |
| 400 | `VALIDATION_ERROR` | 入力形式・必須項目 (詳細は `errors`) |
| 404 | `NOT_FOUND` | 対象なし |
| 409 | `DUPLICATE_ID_MISMATCH` | 同一 `id` で内容が異なる再送 |
| 409 | `VERSION_MISMATCH` | 楽観ロック失敗 |
| 409 | `DUPLICATE_CODE` | コード・バーコード・会員番号の重複 |
| 409 | `TERMINAL_HAS_OPEN_SHIFT` | 開設中シフトがある端末で再開設 |
| 422 | `SHIFT_NOT_FOUND` / `SHIFT_CLOSED` / `SHIFT_TERMINAL_MISMATCH` | 取引・入出金・取消 |
| 422 | `DUPLICATE_RECEIPT_NO` | レシート番号重複 |
| 422 | `PRODUCT_NOT_FOUND` | 取引明細 |
| 422 | `PRICE_OVERRIDE_NOT_ALLOWED` | 売価変更不可商品の単価相違 |
| 422 | `CALCULATION_MISMATCH` | 計算項目不一致 (`expected` にサーバ計算) |
| 422 | `PAYMENT_MISMATCH` | 支払合計・釣銭の不整合 |
| 422 | `CUSTOMER_REQUIRED` | ポイント付与・利用に顧客なし |
| 422 | `ORIGINAL_NOT_FOUND` / `ORIGINAL_NOT_RETURNABLE` | 返品の元取引 |
| 422 | `RETURN_QUANTITY_EXCEEDED` | 返品数量超過 |
| 422 | `HAS_RETURNS` | 返品済み取引の取消 |
| 422 | `IN_USE` | 使用中マスタの削除 |

警告 (受理するが応答の `warnings[]` に含める): `POINT_BALANCE_NEGATIVE`、`PRODUCT_INACTIVE`、`INVENTORY_NEGATIVE`。

---

## 6. 端末側の同期フロー

API 設計が前提にしている MAUI 側の動き。  
通信は `HttpService` (HttpClient + System.Text.Json。失敗時は Problem Details を `ApiResult<T>` で返す) + `NetworkService` (接続確認・インジケータ・エラー通知) を使う ([D-40](decisions.md#d-40-端末の通信-rester-ではなく-httpclient))。

1. **初回**: 設定 QR (`ApiEndPoint` / `StoreId` / `TerminalId`) を読み取り → `GET /sync/masters` (全件) と `GET /inventory?storeId=` をローカル DB (SQLite) に保存。  
   顧客は都度 `lookup` (オンライン) を基本とし、必要なら `GET /customers?updatedSince` でキャッシュ
2. **定期**: `GET /sync/masters?since={前回の serverTime}` で差分適用
3. **書き込みは Outbox**: 端末で発生した書き込み (`XxxRequest`) を発生順にローカル DB の `Outbox` テーブルへ JSON で保存し、バックグラウンドで順に送信する
   - 順序: シフト開設 → 取引 / 入出金 (発生順) → 精算
   - `200` / `201` で完了。  
     `409` / `422` は「要確認」として止め、後続を送らない (シフトの整合のため)。  
     5xx / 通信エラーは指数バックオフで再送
   - `id` を端末が採番しているので、応答を受け取る前に切断しても再送で重複しない
4. **オンライン限定の操作**: 会員照会、他店在庫照会、レシート番号検索 (返品)。  
   ローカルにある取引の返品はオフラインでも可
