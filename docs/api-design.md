# POS サーバ API 設計

MAUI レジ端末アプリと Blazor 管理画面が利用する POS サーバ (ASP.NET Core) の API 設計。  
設計方針は [decisions.md](decisions.md)、DB は [db-design.md](db-design.md)、プロジェクト構成は [architecture.md](architecture.md) を参照。

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
| 利用者 | **端末** (MAUI レジアプリ) と **管理** (Blazor 管理画面)。表中の「用途」列で区別する。管理画面は HTTP を経由せず、API と同じ Core の Service を直接呼ぶ (アカウント、ペアリングコードの発行と登録の解除、スタッフの PIN の設定は管理画面だけにある) |
| 実装基盤 | Minimal API + Blazor Server (MudBlazor) + SQLite、Aspire、OpenAPI ([D-26](decisions.md#d-26-サーバ端末共有のプロジェクトに分ける)) |
| 取引モデル | 一体型。会計完了後に取引を 1 回で送信 ([D-02](decisions.md#d-02-取引は会計を終えてから-1-回で送る)) |
| 金額計算 | 端末が計算し、サーバは同じ `Pos.Domain` で再計算して検証 ([D-03](decisions.md#d-03-金額は端末が計算しサーバが検証する)) |
| 認証 | 管理画面はログイン (Cookie)、端末はペアリングで受け取るトークン (Bearer) ([§2.6](#26-認証認可)、[D-35](decisions.md#d-35-認証は管理画面のログイン端末のトークンスタッフの-pin-にする))。端末発の要求は `storeId` / `terminalId` / `staffId` を本文またはクエリで明示し、サーバは店舗・端末がトークンと一致すること、担当がその店舗 (または本部) の有効なスタッフであることを確かめる |
| テナント | 単一 ([D-01](decisions.md#d-01-家電カメラホームセンターの物販-pos-にする)) |

---

## 2. 共通仕様

### 2.1 URL・形式

| 項目 | 仕様 |
| --- | --- |
| ベースパス | `/api/v1` (URL パスでバージョニング)。ルート定数は `ApiRoutes` にまとめる |
| JSON | **camelCase** ([D-31](decisions.md#d-31-api-は-camelcase-の-json-と-problem-details-にする))。`null` プロパティは省略。列挙型は文字列 (`"Sale"`) |
| クエリパラメータ | camelCase (`?storeId=&updatedSince=`) |
| 日時 | `yyyy-MM-ddTHH:mm:ss.fffZ` (UTC)。営業日などの日付は `yyyy-MM-dd` |
| 金額 (`money`) | `decimal`。通貨は会社設定 (`JPY`)。JPY では整数値のみ ([D-30](decisions.md#d-30-金額は-decimalid-は-guid-v7日時は-utc-にする)) |
| 率 (`rate`) | `decimal`、`0.10` = 10% |
| 数量 (`qty`) | `decimal(9,2)` 相当。ホームセンターの切り売り (m 単位) を想定 |
| ポイント | 整数 (`int`)。1 pt = 1 円 |
| ID | GUID。端末発の書き込みは端末が GUID v7 を採番 ([D-32](decisions.md#d-32-端末発の書き込みは端末が-id-を採番する)) |
| 通信データ | `XxxRequest` / `XxxResponse` (一覧は `XxxResponse`、要素は `XxxResponseItem`) を `Pos.Contract` に置き、サーバと端末の両方で使う ([D-26](decisions.md#d-26-サーバ端末共有のプロジェクトに分ける), [D-31](decisions.md#d-31-api-は-camelcase-の-json-と-problem-details-にする)) |
| OpenAPI | `Microsoft.AspNetCore.OpenApi` + 開発時 NSwag UI (`/swagger`, `/redoc`) |

本書のフィールド名は JSON (camelCase) で書く。  
C# のプロパティ名は PascalCase (`receiptNo` → `ReceiptNo`)。

### 2.2 一覧取得 (ページング・フィルタ)

- 一覧は `page` (0 始まり) / `size` (既定 20、最大 1000) のページ方式で、次の形で返す ([D-31](decisions.md#d-31-api-は-camelcase-の-json-と-problem-details-にする))。  
  ページングしない一覧 (税率など) も同じ形で返す (`page` = 0、`size` = 件数)

```jsonc
{ "total": 1234, "page": 0, "size": 20, "items": [ /* XxxResponseItem */ ] }
```

- 並び替えは `sort` (列名) / `desc` (bool)。  
  許可する列はリソースごとの列挙型 (`StoreSort` など) で決め、一覧の `sort` が不正なら既定の列 (レポートの `sort` / `groupBy` が不正なら 400)。  
  `sort` / `groupBy` の値は大文字小文字を区別しない
- マスタ系一覧は **差分同期**用に `updatedSince` (datetime) と `includeDeleted` (bool) を受け付ける。  
  `updatedSince` 指定時は `updatedAt > updatedSince` のレコードを返し、ページングする一覧は `updatedAt, id` 昇順にする。  
  論理削除済みは `includeDeleted=true` のときだけ `isDeleted: true` で含める (差分同期では付ける)
- 日付範囲 `from` / `to` は日付で両端を含む (取引・シフト・日次締め・レポートは営業日、受注は受注日、入荷は入荷予定日、発注は希望納期)。  
  在庫変動の `from` / `to` だけは UTC の日時で、`to` を含まない。  
  レポートは指定がなければ `to` は当日、`from` は `to` の 30 日前にし、`from` が `to` より後なら `400`。  
  一覧は指定がなければ絞らない

### 2.3 書き込み

| 項目 | 仕様 |
| --- | --- |
| 作成 | `POST /resources` (本文 `XxxCreateRequest` または端末発の `XxxRequest`) → `201 Created` + `XxxResponseItem`。`id` を送る登録 (取引、シフトの開設、入出金、受注、前受金の受取と返金) は、**同じ `id` が既に存在すれば `200 OK` で既存を返し**、主な項目が既存と違えば `409 Conflict` (`DUPLICATE_ID_MISMATCH`)。棚卸・調整は要素ごとに `Duplicate` を返し ([§3.16](#316-在庫-inventory))、精算は実査金額が同じ再送を `200` で返す ([§3.13](#313-レジ開閉現金管理-shifts)) |
| 更新 | 管理系は `PUT /resources/{id}` (`XxxUpdateRequest`、全体置換)。本文の `version` で楽観ロック。不一致なら `409 Conflict` (`VERSION_MISMATCH`) |
| 削除 | 管理系は `DELETE /resources/{id}` で論理削除 (`isDeleted = true`)。取引など履歴は削除しない。削除後も `GET /resources/{id}` は `isDeleted: true` で返し、更新・再削除は `404` |
| 検証 | 入力エラーは `400` (`AddValidation` + DataAnnotations。`errorCode` = `VALIDATION_ERROR`、`errors` にフィールド別)、業務ルール違反は `422`。本文の JSON や引数の型が読めない要求も `400` (`VALIDATION_ERROR`、[D-31](decisions.md#d-31-api-は-camelcase-の-json-と-problem-details-にする)) |
| 重複 | コード・バーコード等の一意制約違反は `409` (`DUPLICATE_CODE`)。`IDialect.IsDuplicate` で SQLite の制約違反を判定する |

### 2.4 エラー応答

RFC 9457 Problem Details (`AddProblemDetails`。`traceId` 拡張付き) に `errorCode` を追加する。  
コードは [§5](#5-エラーコード)。  
取引の検証で違反が複数あるときは、先頭の違反を `title` / `errorCode` にし、すべての文言を `errors` に入れる。  
`errors` のキーは、`400` の入力エラーでは項目名、取引の検証では明細 ID (取引全体は `transaction`)、CSV 取込では行番号にする。

```jsonc
{
  "type": "https://tools.ietf.org/html/rfc4918#section-11.2",
  "title": "計算結果が一致しません",
  "status": 422,
  "traceId": "00-...",
  "errorCode": "CALCULATION_MISMATCH",
  "errors": { "transaction": ["計算結果が一致しません"] },   // 明細 ID (取引全体は transaction) ごとの文言
  "expected": { /* サーバ計算結果 (取引の検証のときだけ、TransactionCalculateResponse) */ }
}
```

### 2.5 共通フィールド

| フィールド | 型 | 説明 |
| --- | --- | --- |
| `id` | guid | 主キー |
| `isDeleted` | bool | 論理削除 (マスタ系) |
| `createdAt` / `updatedAt` | datetime | サーバ付与 |
| `version` | int | 楽観ロック用 (マスタ系)。更新のたびに +1 |

### 2.6 認証・認可

| 利用者 | 方式 | 取得 |
| --- | --- | --- |
| 管理画面 (ブラウザ) | Cookie (`/login` の画面から `POST /auth/login` のフォーム。API ではない) | アカウント (`Accounts`) の ID とパスワード。役割は `Administrator` / `Operator` |
| 端末 | `Authorization: Bearer {token}` | 管理画面で発行したペアリングコードで `POST /terminals/pair` ([§3.3](#33-レジ端末-terminals)) |

| ポリシー | 対象 | 通る要求 |
| --- | --- | --- |
| `Api` | `/api/v1` 全体の既定 | 管理画面のログインか端末のトークン |
| `Admin` | 用途が「管理」だけの API | 管理画面のログイン (役割は問わない) |
| `Administrator` | 用途が「管理 (管理者)」の API (マスタ・会社設定の書き込み、商品の取込と画像、締めの解除) | 管理画面の管理者 |
| `Terminal` | `/terminals/me/*` | 端末のトークン |
| なし | `POST /terminals/pair` | 匿名 (接続元ごとに 1 分 10 回まで。超えると `429`) |

- ログインもトークンもない (トークンが解除済み・不明、端末が無効・削除済みを含む) 要求は `401`、役割が足りない要求は `403` (本文なし)。  
  端末は `401` を受けたら登録が解除されたとして初期設定に戻る
- 端末のトークンで認証された要求は、本文・クエリの店舗・端末がトークンと一致すること (違えば `403` `TERMINAL_MISMATCH`)。  
  確かめるのはシフトの開設・入出金・精算・`current`、取引の登録と取消 (取引の店舗・端末)、受注の登録 (受注の店舗と端末)、受注の入荷とキャンセル (受注の店舗)、前受金の受取と返金 (受注の店舗と端末)、在庫の変更、入荷と移動の受領 (伝票の入荷店)。  
  管理画面のログインの要求と、`current` 以外の読み取りは確かめない ([D-36](decisions.md#d-36-認可は要件で分け端末は自店自端末に限る))
- 担当 (`staffId`) は、その店舗 (または本部) の有効なスタッフであること (`422` `STAFF_INVALID`)。  
  承認が必要な値引は `approvedByStaffId` に店長以上、レジ係の取消も `approvedByStaffId` に店長以上が要る (`422` `APPROVAL_REQUIRED`)。  
  担当は取引の登録・取消と前受金の受取・返金で、承認は取引の登録・取消で確かめる
- 開発・デモ用に `Auth:Enabled = false` で認可を素通しにできる (端末の一致も確かめない。担当と承認者の検証は残す)

---

## 3. リソース別 API

各表の「用途」: **端末** = MAUI レジアプリが使う / **管理** = Blazor 管理画面が使う / **管理 (管理者)** = 管理画面の管理者だけ ([§2.6](#26-認証認可))。  
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
| PUT | `/settings` | 管理 (管理者) | 会社設定更新 (`SettingsUpdateRequest`) |

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
| GET | `/stores?updatedSince&includeDeleted&sort&desc&page&size` | 端末 / 管理 | 店舗一覧 (`StoreResponse`)。`sort` = `code` / `name` / `updatedAt` |
| GET | `/stores/{id}` | 端末 / 管理 | 店舗詳細 (`StoreResponseItem`) |
| POST | `/stores` | 管理 (管理者) | 登録 (`StoreCreateRequest`) |
| PUT | `/stores/{id}` | 管理 (管理者) | 更新 (`StoreUpdateRequest`) |
| DELETE | `/stores/{id}` | 管理 (管理者) | 論理削除 (端末・在庫が残っていれば 422) |

### 3.3 レジ端末 (Terminals)

| フィールド | 型 | 説明 |
| --- | --- | --- |
| `id` | guid | |
| `storeId` | guid | |
| `terminalNo` | int | 店舗内の端末番号 (店舗内で一意)。レシート番号の一部 |
| `name` | string(50) | |
| `lastReceiptSeq` | int | サーバが把握している最終レシート連番。端末再セットアップ時の復元用 |
| `lastSeenAt` | datetime? | 最終通信時刻 (サーバ付与) |
| `appVersion` | string(50)? | 端末アプリのバージョン (端末が送信) |
| `isActive`, `isDeleted`, `createdAt`, `updatedAt`, `version` | | |

| Method | Path | 用途 | 概要 |
| --- | --- | --- | --- |
| GET | `/terminals?storeId&updatedSince&includeDeleted&sort&desc&page&size` | 端末 / 管理 | 端末一覧。`sort` = `terminalNo` / `name` / `updatedAt` |
| GET | `/terminals/{id}` | 端末 / 管理 | 端末詳細 |
| POST | `/terminals` | 管理 (管理者) | 登録 |
| PUT | `/terminals/{id}` | 管理 (管理者) | 更新 |
| DELETE | `/terminals/{id}` | 管理 (管理者) | 論理削除 (開設中シフトがあれば 422) |
| POST | `/terminals/pair` | 端末 (匿名) | 端末の登録 `TerminalPairRequest { pairingCode, deviceName, appVersion? }` → `200` `TerminalPairResponse { token, terminal, store }`。コードの不一致・期限切れ・使用済み、無効・削除済みの端末は `422` (`PAIRING_CODE_INVALID`) |
| POST | `/terminals/me/heartbeat` | 端末 | `TerminalHeartbeatRequest { appVersion? }` → `204`。トークンの端末の `lastSeenAt` / `appVersion` を更新する |

`TerminalCreateRequest` / `TerminalUpdateRequest` は `storeId` / `terminalNo` / `name` / `isActive` (+ `version`)。  
`lastReceiptSeq` は取引登録で、`lastSeenAt` はペアリング・heartbeat・取引登録で、`appVersion` はペアリングと heartbeat でサーバが更新する。

端末の登録は、管理画面で端末ごとにペアリングコード (6 桁、10 分、一度だけ) を発行し、端末で **設定 QR** (`ApiEndPoint` / `PairingCode`) を読み取るか、URL とコードを入力して `POST /terminals/pair` を呼ぶ ([D-35](decisions.md#d-35-認証は管理画面のログイン端末のトークンスタッフの-pin-にする))。  
トークンは応答でだけ返し、サーバは SHA-256 だけを持つ。  
同じ端末を登録し直すと古いトークンは失効する。  
管理画面で登録を解除するか、端末を無効・削除すると、その端末の次の要求は `401` になる。

### 3.4 スタッフ (Staff)

| フィールド | 型 | 説明 |
| --- | --- | --- |
| `id` | guid | |
| `code` | string(20) | スタッフコード (一意)。ログイン・レシート印字用 |
| `name` | string(50) | |
| `role` | enum | `Cashier` / `Manager` / `Admin`。承認 (承認が必要な値引、レジ係の取消) は `Manager` 以上 |
| `storeId` | guid? | 所属店舗。`null` = 本部 (全店) |
| `pinHash` | base64? | PIN (4〜6 桁) のハッシュ (`PinHasher`: PBKDF2 のソルト + ハッシュ)。端末向けの同期 (`/sync/masters`) にだけ載せ、端末がオフラインでも照合する。設定は管理画面だけ |
| `isActive`, `isDeleted`, `createdAt`, `updatedAt`, `version` | | |

| Method | Path | 用途 | 概要 |
| --- | --- | --- | --- |
| GET | `/staff?storeId&updatedSince&includeDeleted&sort&desc&page&size` | 端末 / 管理 | スタッフ一覧 (PIN のハッシュは含めない。端末のログインは `/sync/masters` で受け取った一覧で行う)。`sort` = `code` / `name` / `updatedAt` |
| GET | `/staff/{id}` | 端末 / 管理 | スタッフ詳細 |
| POST | `/staff` | 管理 (管理者) | 登録 |
| PUT | `/staff/{id}` | 管理 (管理者) | 更新 |
| DELETE | `/staff/{id}` | 管理 (管理者) | 論理削除 |

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
| GET | `/categories?updatedSince&includeDeleted&sort&desc&page&size` | 端末 / 管理 | 部門一覧 (階層は `parentId` で組み立てる)。`sort` = `sortOrder` / `code` / `name` / `updatedAt`、`size` は既定 1000 |
| GET | `/categories/{id}` | 管理 | 部門詳細 |
| POST | `/categories` | 管理 (管理者) | 登録 |
| PUT | `/categories/{id}` | 管理 (管理者) | 更新 (親部門に自身を指定すると `422` `VALIDATION_ERROR`) |
| DELETE | `/categories/{id}` | 管理 (管理者) | 論理削除 (所属商品か子部門があれば `422` `IN_USE`) |

### 3.6 税率 (TaxRates)

| フィールド | 型 | 説明 |
| --- | --- | --- |
| `id` | guid | |
| `code` | string(10) | `STD` / `RED` / `EXEMPT` |
| `name` | string(50) | 「標準税率 10%」など |
| `rate` | rate | `0.10` / `0.08` / `0` |
| `kind` | enum | `Standard` / `Reduced` (軽減) / `Exempt` (非課税・不課税) |
| `isDefault` | bool | 商品登録時の既定 (1 件だけ。既定にすると他の既定を外す) |
| `sortOrder`, `isDeleted`, `createdAt`, `updatedAt`, `version` | | |

| Method | Path | 用途 | 概要 |
| --- | --- | --- | --- |
| GET | `/tax-rates?updatedSince&includeDeleted` | 端末 / 管理 | 税率一覧 (少数なのでページングなし) |
| GET | `/tax-rates/{id}` | 端末 / 管理 | 税率詳細 |
| POST | `/tax-rates` | 管理 (管理者) | 登録 |
| PUT | `/tax-rates/{id}` | 管理 (管理者) | 更新 (取引には税率のスナップショットが残るので過去取引は影響しない) |
| DELETE | `/tax-rates/{id}` | 管理 (管理者) | 論理削除 (使用中商品があれば 422) |

### 3.7 商品 (Products)

1 商品 = 1 SKU = 1 JAN。  
バリエーション (色・サイズの親子) は持たない ([D-01](decisions.md#d-01-家電カメラホームセンターの物販-pos-にする))。

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
| `requiresSerial` | bool | 販売時にシリアル番号入力を求める ([D-07](decisions.md#d-07-シリアル番号は明細に持ち会計の前に入力を求める)) |
| `trackInventory` | bool | 在庫管理対象 (`Service` は false) |
| `allowsPriceOverride` | bool | 売価変更可 (オープン価格・配送料など) |
| `unit` | string(10)? | 単位 (個 / 本 / m) |
| `imageUrl` | string? | 画像の URL (`/api/v1/products/{id}/image?v={内容のハッシュ}`。画像がなければ `null`)。`ProductCreateRequest` / `ProductUpdateRequest` には含めず、画像の API でだけ変わる ([D-18](decisions.md#d-18-商品画像は-db-に持ちハッシュ付きの-url-で配る)) |
| `isActive` | bool | 販売可否 (false = 販売停止だがマスタは残す) |
| `isDeleted`, `createdAt`, `updatedAt`, `version` | | |

| Method | Path | 用途 | 概要 |
| --- | --- | --- | --- |
| GET | `/products?categoryId&keyword&isActive&updatedSince&includeDeleted&sort&desc&page&size` | 端末 / 管理 | 商品一覧。`keyword` は code / barcode / name / kana / modelNo の部分一致。`sort` は code / name / price / updatedAt |
| GET | `/products/{id}` | 端末 / 管理 | 商品詳細 |
| GET | `/products/lookup?barcode=` または `?code=` | 端末 | スキャン用 1 件取得。見つからなければ 404 |
| POST | `/products` | 管理 (管理者) | 登録 |
| PUT | `/products/{id}` | 管理 (管理者) | 更新 |
| DELETE | `/products/{id}` | 管理 (管理者) | 論理削除 |
| GET | `/products/csv` | 管理 | CSV 出力 (BOM 付き UTF-8、削除済みを除く全件) |
| POST | `/products/import?dryRun` | 管理 (管理者) | CSV 取込 (本文に CSV、`Content-Type: text/csv`)。後述 |
| GET | `/products/{id}/image?v=` | 端末 / 管理 | 画像 (JPEG / PNG)。`v` が今の画像と一致すれば `Cache-Control: private, max-age=31536000, immutable`、それ以外は `no-cache`。`ETag` で再検証できる (`304`)。画像がなければ `404` |
| PUT | `/products/{id}/image` | 管理 (管理者) | 画像の登録・置き換え。本文に画像そのもの (`Content-Type: image/jpeg` / `image/png`、2 MB まで。形式は先頭のバイトでも確かめる)。応答は商品 (`imageUrl` が変わり、`updatedAt` / `version` が進むので端末の差分同期に載る)。Content-Type が違えば `415`、2 MB を超えれば `413`、画像でなければ `422` |
| DELETE | `/products/{id}/image` | 管理 (管理者) | 画像の削除 (`204`。画像がなければ何もしない) |

#### CSV 取込 (`POST /products/import`)

列は CSV 出力と同じ (出力して編集して戻せる。参照用の「部門」は読まない)。  
文字コードは UTF-8 (BOM の有無を問わない) と Shift_JIS を判別する。  
コードが一致すれば更新、なければ登録し、変更のない行は書き込まない。  
CSV にない商品は削除しない ([D-19](decisions.md#d-19-商品の-csv-取込は全行を検証してから反映する))。

| 項目 | 仕様 |
| --- | --- |
| 値 | 部門・税率はコード、種別は `Goods` / `Service`、真偽は `True` / `False` (`1` / `0` も可)、金額は 0 以上 (桁区切り可)、還元率は 0〜1 |
| 誤り | 必須・長さ・値の形式、部門・税率のコードがない、ファイルの中のコード・JAN の重複、他の商品 (削除済みを含む) の JAN・削除済みの商品のコード |
| `dryRun=true` | 検証だけ。誤りのある行も `200` で `ProductImportResponse` を返す |
| 反映 | 1 行でも誤りがあれば何も反映せず `422` (`VALIDATION_ERROR`、`errors` は行番号ごとの文言)。全行が正しければ 1 トランザクションで反映して `200`。検証の後に他で変わっていれば `409` (`VERSION_MISMATCH`) |
| 形式の誤り | 列が足りない・行がない `400`、`text/csv` 以外 `415`、5 MB 超 `413` |

`ProductImportResponse`: `{ dryRun, insertCount, updateCount, unchangedCount, errorCount, items: [{ lineNo, code, name, action (Insert / Update / Unchanged / Error), errors[] }] }`。  
`lineNo` はファイルの行番号 (見出しが 1 行目)。

### 3.8 値引 (Discounts)

定義済み値引。  
任意額の値引 (家電の値引き交渉) は取引側で `discountId = null` として登録でき、承認は求めない。

| フィールド | 型 | 説明 |
| --- | --- | --- |
| `id` | guid | |
| `code` | string(20) | 一意 |
| `name` | string(50) | 「社員割引 10%」など |
| `type` | enum | `Amount` (定額) / `Percent` (定率) |
| `value` | decimal | 金額 (円) または率 (`0.10`) |
| `scope` | enum | `Line` (明細) / `Transaction` (取引全体) |
| `requiresApproval` | bool | 承認要 (端末が承認者を選んで `approvedByStaffId` に入れ、サーバはその店舗 (または本部) の有効な店長以上であることを確かめる。`422` `APPROVAL_REQUIRED`) |
| `isActive`, `sortOrder`, `isDeleted`, `createdAt`, `updatedAt`, `version` | | |

| Method | Path | 用途 | 概要 |
| --- | --- | --- | --- |
| GET | `/discounts?updatedSince&includeDeleted` | 端末 / 管理 | 値引一覧 |
| GET | `/discounts/{id}` | 端末 / 管理 | 値引詳細 |
| POST / PUT / DELETE | `/discounts`, `/discounts/{id}` | 管理 (管理者) | 登録 / 更新 / 論理削除 |

### 3.9 支払方法 (PaymentMethods)

| フィールド | 型 | 説明 |
| --- | --- | --- |
| `id` | guid | |
| `code` | string(20) | 一意 |
| `name` | string(50) | |
| `shortName` | string(10)? | 端末の支払ボタンに出す短い名前 (「クレカ」など。省略時は `name`) |
| `kind` | enum | `Cash` / `Card` / `Qr` / `EMoney` / `Voucher` (商品券) / `Points` / `Credit` (掛売) / `Other` / `Deposit` (受注の前受金を会計で充てる) |
| `allowsChange` | bool | 釣銭あり (預り金 > 充当額 を許可)。通常 `Cash` のみ true |
| `requiresReference` | bool | 伝票番号など参照の入力を求める (カードなど) |
| `isActive`, `sortOrder`, `isDeleted`, `createdAt`, `updatedAt`, `version` | | |

有効な `kind = Points` (ポイントの充当用、[D-04](decisions.md#d-04-ポイントは商品ごとの還元率で付け1-円として使う)) と `kind = Deposit` (受注の前受金の充当用、[D-14](decisions.md#d-14-前受金はシフトで受け取り会計で全額を充てる)) の支払方法は、それぞれ 1 件だけ持つ。  
2 件目を有効にする登録・変更は `422` `VALIDATION_ERROR`。

| Method | Path | 用途 | 概要 |
| --- | --- | --- | --- |
| GET | `/payment-methods?updatedSince&includeDeleted` | 端末 / 管理 | 支払方法一覧 |
| GET | `/payment-methods/{id}` | 端末 / 管理 | 支払方法詳細 |
| POST / PUT / DELETE | `/payment-methods`, `/payment-methods/{id}` | 管理 (管理者) | 登録 / 更新 / 論理削除 |

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
  "products": [ /* 3.7 */ ],
  "discounts": [ /* 3.8 */ ],
  "paymentMethods": [ /* 3.9 */ ],
  "adjustmentReasons": [ /* 3.16 */ ],
  "productsTruncated": false                  // 常に false
}
```

各一覧は論理削除済みも `isDeleted: true` で含む。  
`productsTruncated` は常に `false` で、サーバは `products` を省かない。  
端末は `true` を受けたときに `GET /products?updatedSince&includeDeleted=true&page&size` で分割して取り込む。

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

`CustomerPointHistoryResponseItem` (ポイント履歴の要素):

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
| GET | `/customers?keyword&code&phone&updatedSince&includeDeleted&sort&desc&page&size` | 端末 / 管理 | 顧客検索。`keyword` は code / name / kana / phone の部分一致。`sort` は code / name / kana / pointBalance / createdAt / updatedAt |
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
端末が計算した結果をそのまま送り (`TransactionCreateRequest`)、サーバは [§4](#4-金額税ポイント計算仕様-共有ライブラリ) の仕様で再計算して検証し、`TransactionResponseItem` を返す。

#### 取引の項目

「入力」= 端末が決める値、「計算」= 端末が計算しサーバが検証する値、「サーバ」= サーバ付与 (`TransactionResponseItem` にのみ含まれる)。

| フィールド | 型 | 区分 | 説明 |
| --- | --- | --- | --- |
| `id` | guid | 入力 | 端末採番 |
| `type` | enum | 入力 | `Sale` / `Return` |
| `status` | enum | 入力 | `Completed` / `Voided`。`Voided` + `void` で登録した取引は在庫・ポイントを動かさず、取消の承認は確かめず、締め済みの営業日でも受理する (端末は取消を `POST /transactions/{id}/void` で送る) |
| `storeId`, `terminalId`, `staffId`, `shiftId` | guid | 入力 | シフトは `Open` で端末が一致すること |
| `customerId` | guid? | 入力 | ポイント付与・利用時は必須 |
| `receiptNo` | string(20) | 入力 | `{店舗コード}-{端末番号:00}-{連番:000000}`。全体で一意 ([D-09](decisions.md#d-09-レシート番号は端末が採番する)) |
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
| `changeAmount` | money | 計算 | `tenderedTotal − total` (`allowsChange` の支払方法でのみ発生) |
| `pointsEarned` | int | 計算 | 付与ポイント。`Return` では取消分を負で持つ |
| `pointsRedeemed` | int | 計算 | 利用ポイント (= `Points` 支払の合計)。`Return` では返還分を負で持つ |
| `pointsBalanceAfter` | int? | サーバ | 処理後残高 |
| `delivery` | object? | 入力 | 配送情報 (下記、[D-08](decisions.md#d-08-配送は取引に配送先を付けるだけにする)) |
| `note` | string(500)? | 入力 | |
| `void` | object? | 入力 / サーバ | `{ voidedAt, voidedByStaffId, reason }` |
| `orderId` | guid? | 入力 | 受注から会計したとき (`Sale` のみ。[§3.15](#315-受注-orders)) |
| `orderNo` | string? | サーバ | 受注番号 (受注から会計した取引) |
| `warnings[]` | `{ code, message, lineId? }[]` | サーバ | 受理したが確認が必要な事項 ([§5](#5-エラーコード) の警告コード。登録した `201` の応答だけに付く) |
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
| `note` | string(200)? | 入力 | |

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
| `reason` | string(200)? | 入力 | 任意値引の理由 |
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
| `note` | string(200)? | |

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

応答 `201 Created` (`TransactionResponseItem`) は同じ形に `pointsBalanceAfter: 8074`、`createdAt`、`updatedAt` が付く。

#### エンドポイント

| Method | Path | 用途 | 概要 |
| --- | --- | --- | --- |
| POST | `/transactions` | 端末 | 取引登録 (`TransactionCreateRequest`)。`201` 新規 / `200` 同一 `id` 既存 / `409` 同一 `id` で内容相違 / `422` 検証エラー |
| GET | `/transactions?storeId&terminalId&staffId&shiftId&customerId&from&to&type&status&serialNumber&sort&desc&page&size` | 端末 / 管理 | 取引検索 (`TransactionResponse`)。`sort` = `transactedAt` / `receiptNo` / `total` / `businessDate` (既定は `transactedAt` の降順)。`serialNumber` は明細のシリアル番号の完全一致 (端末は全店の取引を探す) |
| GET | `/transactions/{id}` | 端末 / 管理 | 取引詳細 (`TransactionResponseItem`) |
| GET | `/transactions/{id}/receipt/pdf` | 管理 | レシートの控え (再発行) の PDF。項目は端末のレシートと同じ ([D-21](decisions.md#d-21-帳票はサーバが-pdf-にする)) |
| GET | `/transactions/lookup?receiptNo=` | 端末 | 返品時のレシート番号検索 |
| POST | `/transactions/{id}/void` | 端末 | 取消 `TransactionVoidRequest { staffId, approvedByStaffId?, reason, voidedAt }` → `200` 取引。レジ係の取消は店長以上の `approvedByStaffId` が要る |
| POST | `/transactions/calculate` | 端末 / 管理 | 入力項目 (`type`, `originalTransactionId`, `lines[]`, `discounts[]`, `payments[]`) を送り (`TransactionCalculateRequest`)、計算項目 (`TransactionCalculateResponse`) を返す (登録しない)。共有ライブラリの検証用 |

#### 業務ルール

**販売 (`type = Sale`)**

1. `shiftId` のシフトが存在し、`Open` で、`terminalId` が一致すること (`SHIFT_NOT_FOUND` / `SHIFT_CLOSED` / `SHIFT_TERMINAL_MISMATCH`)
2. `receiptNo` が一意であること (`DUPLICATE_RECEIPT_NO`)
3. 各明細の `productId` が存在すること (`PRODUCT_NOT_FOUND`)。  
   `isActive = false` はオフライン同期の遅れがあり得るので受理し、警告 (`PRODUCT_INACTIVE`) を付ける
4. `allowsPriceOverride = false` の商品は `unitPrice = listPrice` であること (`PRICE_OVERRIDE_NOT_ALLOWED`)
5. [§4](#4-金額税ポイント計算仕様-共有ライブラリ) で計算できる入力であること (明細が 1 つ以上、明細 ID が重ならない、数量は正、単価は 0 以上、値引の値は 0 以上で率は 1 以下、値引の対象明細がある、値引が値引前の金額を超えない。違えば `VALIDATION_ERROR`)。  
   再計算した結果と、送信された計算項目が一致すること (`CALCULATION_MISMATCH`。応答の `expected` にサーバ計算結果)
6. Σ `payments.amount = total`、`changeAmount = tenderedTotal − total ≥ 0`、釣銭が出るのは `allowsChange` の支払方法のみ (`PAYMENT_MISMATCH`)
7. `pointsEarned > 0` または `pointsRedeemed > 0` のとき `customerId` が必須 (`CUSTOMER_REQUIRED`)
8. ポイント残高不足は**受理して警告** (`POINT_BALANCE_NEGATIVE`) にとどめる (取引は店頭で成立済み)。  
   残高は負になり得るので、管理画面のダッシュボードに残高が負の会員を出す
9. 店舗 × 営業日が締め済みでも**受理して警告** (`DAY_ALREADY_CLOSED`) にとどめ、日次締めに締め後の取引の印を付ける ([§3.14](#314-日次締め-dailyclosings))
10. `orderId` があれば、その受注が自店の引き渡し待ちであること (`ORDER_NOT_FOUND` / `ORDER_NOT_READY`)。  
    `kind = Deposit` の支払の合計は、受注の前受金 (`depositAmount`) と同じであること (`PAYMENT_MISMATCH`。受注のない会計では 0)。  
    登録と同じトランザクションで受注を完了にし、その間に受注の状態か前受金が変わっていれば `ORDER_NOT_READY` ([§3.15](#315-受注-orders))
11. 担当と、承認が必要な値引の承認者は [§2.6](#26-認証認可) のとおり (`STAFF_INVALID` / `APPROVAL_REQUIRED`)
12. 登録時の副作用: `trackInventory` の明細ごとに在庫変動 (`Sale`, −数量)、ポイント履歴 (`Redeem` → `Earn` の順)、`terminals.lastReceiptSeq` 更新

**返品 (`type = Return`)**

1. シフト・レシート番号・担当は販売と同じく確かめる (`SHIFT_NOT_FOUND` / `SHIFT_CLOSED` / `SHIFT_TERMINAL_MISMATCH`、`DUPLICATE_RECEIPT_NO`、`STAFF_INVALID`)
2. `originalTransactionId` の取引が `Sale` かつ `Completed` であること (`ORIGINAL_NOT_FOUND` / `ORIGINAL_NOT_RETURNABLE`)
3. 明細が 1 つ以上あり、各明細の `originalLineId` が元取引の明細で、数量が正であること (`VALIDATION_ERROR`)。  
   `quantity ≤ 元数量 − returnedQuantity` であること (`RETURN_QUANTITY_EXCEEDED`)
4. 明細金額・値引・ポイントは元明細から [§4.5](#45-返品) の式で導出した値と一致すること (`CALCULATION_MISMATCH`)
5. ポイントの取消・返還があるときは `customerId` が必須 (`CUSTOMER_REQUIRED`)
6. `payments` は返金方法。  
   Σ `amount = total`、`tenderedAmount = amount`、`changeAmount = 0`。  
   `kind = Deposit` は使えない (`PAYMENT_MISMATCH`)
7. 締め済みの営業日は販売と同じく受理して警告 (`DAY_ALREADY_CLOSED`)
8. 副作用: 在庫変動 (`Return`, +数量)、ポイント履歴 (`Refund` → `Revoke` の順)、元明細の `returnedQuantity` 加算

**取消 (`POST /transactions/{id}/void`)**

1. 対象が `Completed` で (取消済みは `VALIDATION_ERROR`)、そのシフトが `Open` であること (`SHIFT_NOT_FOUND` / `SHIFT_CLOSED`。精算後は返品で対応)
2. `Sale` に返品が紐付いていれば取消不可 (`HAS_RETURNS`)
3. 取引の店舗 × 営業日が締め済みなら取消不可 (`DAY_CLOSED`。返品で対応)
4. 担当は取引の店舗 (または本部) の有効なスタッフ (`STAFF_INVALID`)。  
   レジ係なら、その店舗 (または本部) の店長以上の承認者が要る (`APPROVAL_REQUIRED`)
5. 副作用: 在庫変動 (`Void`, 逆方向)、ポイント履歴 (`Void`)、`Return` の取消なら元明細の `returnedQuantity` を戻し、受注から会計した `Sale` の取消なら受注を引き渡し待ちに戻す。  
   取消済み取引は集計から除外

### 3.13 レジ開閉・現金管理 (Shifts)

端末ごとの「開設 → 販売 → 入出金 → 精算」の単位。

#### シフトの項目 (`ShiftResponseItem`)

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
| `expectedCash` | money? | サーバ | `openingCash + cashSales − cashReturns + paidIn − paidOut + depositCashIn − depositCashOut` |
| `difference` | money? | サーバ | `actualCash − expectedCash` |
| `totals` | object | サーバ | `{ cashSales, cashReturns, paidIn, paidOut, depositCashIn, depositCashOut, salesCount, returnCount, voidCount, salesTotal, returnsTotal }` (取消済みを除く。`depositCashIn` / `depositCashOut` はシフトで現金で受け取った・返した前受金) |
| `note` | string(500)? | 入力 | |
| `createdAt`, `updatedAt` | | サーバ | |

`ShiftCashEventResponseItem` (入出金):

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
| GET | `/shifts?storeId&terminalId&status&from&to&sort&desc&page&size` | 管理 | シフト一覧。`sort` = `openedAt` / `businessDate` / `closedAt` (既定は `openedAt` の降順) |
| GET | `/shifts/{id}` | 端末 / 管理 | シフト詳細 (集計付き) |
| POST | `/shifts/{id}/cash-events` | 端末 | 入出金登録 (`ShiftCashEventRequest`、`Open` のみ) |
| GET | `/shifts/{id}/cash-events?page&size` | 端末 / 管理 | 入出金一覧 |
| POST | `/shifts/{id}/close` | 端末 | 精算 `ShiftCloseRequest { closedAt, closedByStaffId, actualCash, denominations, note }` → `200` シフト (`expectedCash` / `difference` 確定)。精算済みのシフトへの再送は、実査金額が同じなら `200` で精算済みのシフトを返し、違えば `422` (`SHIFT_CLOSED`) |
| GET | `/shifts/{id}/summary` | 端末 / 管理 | 精算レポート (`ShiftSummaryResponse`、下記) |
| GET | `/shifts/{id}/summary/pdf` | 管理 | 精算レポートの PDF ([D-21](decisions.md#d-21-帳票はサーバが-pdf-にする)) |

`ShiftSummaryResponse` (精算レポート):

```jsonc
{
  "shift": { /* ShiftResponseItem */ },
  "byPaymentMethod": [ { "paymentMethodId": "...", "name": "現金", "kind": "Cash",
                         "salesAmount": 125000, "salesCount": 40, "returnAmount": 2000, "returnCount": 1 } ],
  "byTaxRate":       [ { "taxRateId": "...", "rate": 0.10, "taxIncluded": true, "taxableAmount": 300000, "taxAmount": 27272 } ],
  "byCategory":      [ { "categoryId": "...", "name": "カメラ", "quantity": 12, "netAmount": 180000 } ],
  "points":          { "earned": 15000, "redeemed": 4000 },
  "cash":            { "openingCash": 30000, "cashSales": 125000, "cashReturns": 2000, "paidIn": 0, "paidOut": 10000,
                       "depositCashIn": 5000, "depositCashOut": 0,
                       "expectedCash": 148000, "actualCash": 147900, "difference": -100 }
}
```

#### 業務ルール

- 端末につき `Open` のシフトは同時に 1 つ
- 取引・入出金・前受金は `Open` のシフトにのみ登録できる。  
  **端末は精算要求の前に、そのシフトの取引・入出金をすべて送信し終えていること** ([§6](#6-端末側の同期フロー))。  
  前受金はオンライン限定なので送信を待たない
- 精算後の再開はしない (翌営業日は新しいシフト)。  
  精算後の訂正は返品または手動調整で行う

### 3.14 日次締め (DailyClosings)

店舗 × 営業日の締め ([D-12](decisions.md#d-12-日次締めは日計を写して持ち締めた日の取消を止める))。  
締めた時点の日計と内訳を確定する (取消の制限と、締めた後に届いた取引の扱いは業務ルール)。

#### 日次締めの項目 (`DailyClosingResponseItem`)

一覧の要素は店舗 × 営業日 (シフト・取引・締めのある日) で、未締めの日も含む。  
締め済みは締めた時点の日計、未締めは取引からの集計 (取消済みを除き、返品は負。[§3.17](#317-レポート-reports) の売上集計と同じ定義)。

| フィールド | 型 | 説明 |
| --- | --- | --- |
| `id` | guid? | 締め済みのときだけ |
| `storeId` | guid | |
| `businessDate` | date | 営業日 |
| `status` | enum | `Open` (未締め) / `Closed` (締め済み) |
| `hasLateTransactions` | bool | 締めた後に同じ営業日の取引が届いた (締め直すまで日計に含まれない) |
| `shiftCount` | int | その営業日のシフトと、その営業日の取引を含むシフト (営業日の切替時刻をまたいで開いていたシフト) の数 |
| `openShiftCount` | int | そのうち未精算のシフト (現在の状態。1 件でもあれば締められない) |
| `salesCount`, `returnCount`, `voidCount` | int | 販売・返品・取消の件数 |
| `customerCount` | int | 客数 (会員の人数) |
| `salesTotal`, `returnsTotal`, `netSales`, `discountTotal`, `taxTotal` | money | |
| `pointsEarned`, `pointsRedeemed` | int | |
| `closedAt` | datetime? | |
| `closedBy` | string? | 締めた管理画面のアカウント名 (認証を無効にしてログインしていないときは `null`) |

`DailyClosingSummaryResponse` (締めの内容。内訳は締め済みなら締めた時点、未締めなら取引からの集計。シフトは現在の状態):

```jsonc
{
  "dailyClosing":    { /* DailyClosingResponseItem */ },
  "byPaymentMethod": [ { "paymentMethodId": "...", "name": "現金", "kind": "Cash",
                         "salesAmount": 125000, "salesCount": 40, "returnAmount": 2000, "returnCount": 1 } ],
  "byTaxRate":       [ { "taxRateId": "...", "rate": 0.10, "taxIncluded": true, "taxableAmount": 300000, "taxAmount": 27272 } ],
  "shifts":          [ { "id": "...", "terminalId": "...", "status": "Closed", "businessDate": "2026-09-11",
                         "openedAt": "...", "openedByStaffId": "...", "closedAt": "...", "closedByStaffId": "...",
                         "expectedCash": 143000, "actualCash": 142900, "difference": -100 } ]
}
```

#### エンドポイント

| Method | Path | 用途 | 概要 |
| --- | --- | --- | --- |
| POST | `/daily-closings` | 管理 | 締め `DailyClosingCreateRequest { storeId, businessDate }` → `201` + `DailyClosingSummaryResponse`。シフトのない日は `422` (`SHIFT_NOT_FOUND`)、未精算のシフトがあれば `422` (`SHIFT_STILL_OPEN`)、締め済みは `409` (`ALREADY_CLOSED`) |
| GET | `/daily-closings?storeId&status&from&to&sort&desc&page&size` | 管理 | 店舗 × 営業日の一覧 (`DailyClosingResponse`)。営業日の降順、同じ日は店舗コード順。`sort` = `businessDate` / `netSales` |
| GET | `/daily-closings/preview?storeId&businessDate` | 管理 | その日の内容 (`DailyClosingSummaryResponse`)。未締めの日は締める前の確認に使う |
| GET | `/daily-closings/{id}` | 管理 | 締めた内容 (`DailyClosingSummaryResponse`) |
| DELETE | `/daily-closings/{id}` | 管理 (管理者) | 締め解除 (日計と内訳を消して未締めに戻す) → `204` |

売上日報の PDF は [§3.17](#317-レポート-reports) の `/reports/sales/daily/pdf` (取引から都度集計) を使う。

#### 業務ルール

- 締めるには、その営業日にシフトがあり、関係するシフト (その営業日のシフトと、その営業日の取引を含むシフト) がすべて精算済みであること
- 締めた営業日の取引は取消できない (`DAY_CLOSED`)。  
  返品で対応する
- 締めた後に届いた同じ営業日の取引 (オフラインだった端末の送信、締めた後に開設したシフト) は受理し、`warnings[]` に `DAY_ALREADY_CLOSED` を付けて `hasLateTransactions` を立てる。  
  締めを解除して締め直すと日計に入る

### 3.15 受注 (Orders)

取り寄せ・取り置きの約束 ([D-13](decisions.md#d-13-受注は会計前の約束として持ち会計で完了にする))。  
会計は取引 ([§3.12](#312-取引-transactions)) で行い、`orderId` を付けた会計で受注が完了になる。  
端末からの登録と状態の変更はオンライン限定。  
前受金は端末のシフトで受け取り、会計で全額を充てる ([D-14](decisions.md#d-14-前受金はシフトで受け取り会計で全額を充てる))。

#### 受注の項目 (`OrderResponseItem`)

| フィールド | 型 | 区分 | 説明 |
| --- | --- | --- | --- |
| `id` | guid | 入力 | 端末 / 管理画面が採番 |
| `orderNo` | string | サーバ | `{店舗コード}-O-{連番:000000}` (店舗ごとの連番) |
| `storeId` | guid | 入力 | |
| `terminalId` | guid? | 入力 | 管理画面で登録したときは `null` |
| `staffId` | guid | 入力 | 担当 |
| `customerId` | guid? | 入力 | 会員 |
| `customerName` | string(100) | 入力 | 宛名。会員のときは省略でき、会員の名前を使う (会員か宛名のどちらかが必要) |
| `phone` | string(20)? | 入力 | 省略すると会員の電話 |
| `type` | enum | 入力 | `BackOrder` (取り寄せ) / `Hold` (取り置き) |
| `status` | enum | サーバ | `Ordered` (入荷待ち) / `Arrived` (引き渡し待ち) / `Completed` (完了) / `Cancelled` (キャンセル)。取り置きは `Arrived` から始まる |
| `requestedDate` | date? | 入力 | 希望日 (入荷予定・取り置きの期限) |
| `note` | string(500)? | 入力 | |
| `total` | money | サーバ | 明細の金額の合計 (税・値引は会計で決まる) |
| `transactionId` | guid? | サーバ | 会計した取引 (完了のとき) |
| `orderedAt` | datetime | 入力 | 省略すると登録時刻 |
| `arrivedAt`, `completedAt`, `cancelledAt` | datetime? | サーバ | |
| `cancelReason` | string(200)? | 入力 (キャンセル時) | |
| `lines[]` | object[] | 入力 | `{ id, lineNo, productId, productCode, productName, quantity, unitPrice, amount, note }`。`amount` はサーバが計算する (単価 × 数量の切り捨て) |
| `depositAmount` | money | サーバ | 会計で充てる前受金 (受け取った額 − 返した額)。完了・キャンセルした受注は `0` |
| `deposits[]` | object[] | サーバ | 前受金の受取と返金の記録 `{ id, type (Receive / Refund), paymentMethodId, kind, amount, reference, terminalId, shiftId, staffId, occurredAt }` |
| `createdAt`, `updatedAt`, `version` | | サーバ | |

#### エンドポイント

| Method | Path | 用途 | 概要 |
| --- | --- | --- | --- |
| POST | `/orders` | 端末 / 管理 | 登録 (`OrderCreateRequest`)。`201` 新規 / `200` 同一 `id` 既存 / `409` 同一 `id` で内容相違 / `400` 会員も宛名もない / `422` 商品・会員・店舗が見つからない |
| GET | `/orders?storeId&status&open&type&customerId&keyword&from&to&sort&desc&page&size` | 端末 / 管理 | 一覧 (`OrderResponse`)。`open=true` は未完了だけ、`keyword` は受注番号・宛名・電話の部分一致、`from` / `to` は受注日。`sort` = `orderedAt` / `orderNo` / `requestedDate` |
| GET | `/orders/{id}` | 端末 / 管理 | 詳細 |
| GET | `/orders/{id}/pdf` | 管理 | 受注票 (PDF。お客様の控えで、前受金を受け取ったときは預り証を兼ねる。[D-21](decisions.md#d-21-帳票はサーバが-pdf-にする)) |
| PUT | `/orders/{id}` | 管理 | 変更 (`OrderUpdateRequest`: 会員・宛名・電話・希望日・備考・明細 (全体を置き換える)・`version`)。完了・キャンセル済みは `422` (`ORDER_STATUS_INVALID`)、版の不一致は `409` |
| POST | `/orders/{id}/arrive` | 端末 / 管理 | 入荷 (入荷待ちのときだけ。それ以外は `422` `ORDER_STATUS_INVALID`) |
| POST | `/orders/{id}/cancel` | 端末 / 管理 | キャンセル `OrderCancelRequest { reason }` (未完了で、前受金がないときだけ。前受金があれば `422` `ORDER_DEPOSIT_INVALID`) |
| POST | `/orders/{id}/deposit` | 端末 | 前受金の受取 `OrderDepositRequest { id, shiftId, terminalId, staffId, paymentMethodId, amount, reference, occurredAt }` → 受注。`201` 新規 / `200` 同一 `id` 既存 / `409` 同一 `id` で内容相違 |
| POST | `/orders/{id}/deposit/refund` | 端末 | 前受金の返金 `OrderDepositRefundRequest { id, shiftId, terminalId, staffId, occurredAt }` → 受注。前受金の全額を受け取った方法で返す。`201` 新規 / `200` 同一 `id` 既存 / `409` 同一 `id` で内容相違 |

#### 業務ルール

- 会計の条件 (自店の引き渡し待ち、前受金の全額を `kind = Deposit` で充てる) と、登録と同じトランザクションで受注を完了 (`transactionId`・`completedAt`) にすることは [§3.12](#312-取引-transactions) の販売の 10 のとおり。  
  会計の明細は受注の明細と同じでなくてよい。  
  その取引を取り消すと受注は引き渡し待ちに戻り、前受金も戻る
- 在庫は会計のときに減らす (受注では引き当てない)。  
  取り寄せの入荷は状態だけ
- 前受金は未完了の受注に 1 つだけ受け取れる (返したら受け取り直せる)。  
  金額は受注の金額まで、支払方法は有効な `Cash` / `Card` / `Qr` / `EMoney` で、違えば `422` `ORDER_DEPOSIT_INVALID` (1 円未満は `400`)。  
  未完了でない受注は `422` `ORDER_STATUS_INVALID`
- 前受金を受け取る・返すシフトは開設中で、その端末のものであること (`SHIFT_NOT_FOUND` / `SHIFT_CLOSED` / `SHIFT_TERMINAL_MISMATCH`)。  
  シフトの店舗が受注の店舗と違えば `ORDER_NOT_FOUND`、担当は取引と同じく `STAFF_INVALID` で確かめる
- 返金は前受金の全額を、最後に受け取った方法で返す。  
  前受金がなければ `422` `ORDER_DEPOSIT_INVALID`
- 現金の前受金はシフトの予想現金に入り (`depositCashIn` / `depositCashOut`)、会計で充てた分は現金売上に入らない
- 受注の変更で金額が前受金を下回っても変更はできる。  
  会計の合計が前受金より少ないときは、前受金を返してから会計する

### 3.16 在庫 (Inventory)

現在庫 (`InventoryLevelResponseItem`) と変動履歴 (`InventoryChangeResponseItem`)。  
取引による変動はサーバが自動生成し、端末からは棚卸・調整だけを送る ([D-15](decisions.md#d-15-在庫は変動の履歴と現在庫で持つ))。  
仕入先からの入荷と店舗間移動は伝票で持ち、受領・出荷の操作で変動を記録する ([D-16](decisions.md#d-16-入荷と店舗間移動は伝票で持つ))。  
仕入先への発注も伝票で持ち、[発注] で入荷予定を作る ([D-17](decisions.md#d-17-発注は入荷予定を作りその受領とキャンセルに合わせる))。

| `InventoryLevelResponseItem` | 型 | 説明 |
| --- | --- | --- |
| `storeId`, `productId` | guid | 複合キー |
| `quantity` | qty | 現在庫 (負も許容し、要確認として扱う) |
| `updatedAt` | datetime | |

| `InventoryChangeResponseItem` | 型 | 説明 |
| --- | --- | --- |
| `id` | guid | 端末採番 (棚卸・調整) / サーバ採番 (取引・入荷・移動由来) |
| `storeId`, `productId` | guid | |
| `type` | enum | `Sale` / `Return` / `Void` (取引由来) / `PhysicalCount` (棚卸) / `Adjustment` (調整) / `Receive` (入荷の受領) / `TransferOut` (移動の出荷) / `TransferIn` (移動の受領) |
| `quantityDelta` | qty | 増減 (符号付き) |
| `quantityAfter` | qty | 処理後在庫 (サーバ計算) |
| `reasonId` | guid? | 調整理由 (`adjustmentReasons`) |
| `reason` | string? | 自由記述 (入荷は納品書番号、移動は移動番号) |
| `referenceType`, `referenceId`, `referenceLineId` | | 由来の伝票と明細。取引は `Transaction`、入荷は `InventoryReceipt`、移動は `InventoryTransfer` |
| `staffId` | guid? | |
| `occurredAt`, `createdAt` | datetime | |

| Method | Path | 用途 | 概要 |
| --- | --- | --- | --- |
| GET | `/inventory?storeId&productId&categoryId&negativeOnly&updatedSince&page&size` | 端末 / 管理 | 現在庫一覧 (端末は自店分を差分同期) |
| GET | `/inventory/{productId}` | 端末 / 管理 | 商品の**全店舗**在庫 (他店在庫照会) `{ productId, levels: [{ storeId, storeName, quantity, updatedAt }] }` |
| POST | `/inventory/changes` | 端末 / 管理 | 棚卸・調整の一括登録 (`InventoryChangeRequest`、下記) |
| GET | `/inventory/changes?storeId&productId&type&from&to&page&size` | 端末 / 管理 | 変動履歴 (`from` / `to` は UTC の日時で、`to` を含まない) |
| GET | `/inventory/adjustment-reasons?updatedSince&includeDeleted` | 端末 / 管理 | 調整理由一覧 (破損 / 廃棄 / 万引き / 自家消費 / 棚卸差異 ...) |
| GET | `/inventory/adjustment-reasons/{id}` | 端末 / 管理 | 調整理由詳細 |
| POST / PUT / DELETE | `/inventory/adjustment-reasons`, `.../{id}` | 管理 (管理者) | 登録 / 更新 / 論理削除 |
| GET | `/inventory/suppliers?includeDeleted`, `.../{id}` | 管理 | 仕入先一覧 / 詳細 (`SupplierResponseItem`: コード・名称・電話・メール・備考・有効) |
| POST / PUT / DELETE | `/inventory/suppliers`, `.../{id}` | 管理 (管理者) | 登録 / 更新 (`version`) / 論理削除。コードの重複は `409` |

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
- `type` は `PhysicalCount` / `Adjustment` だけ (ほかは `400`)。  
  入荷・移動の変動は下の伝票の受領・出荷で作る

#### 入荷の項目 (`InventoryReceiptResponseItem`)

| フィールド | 型 | 区分 | 説明 |
| --- | --- | --- | --- |
| `id` | guid | サーバ | |
| `storeId` | guid | 入力 | 入荷する店舗 |
| `supplierId` | guid | 入力 | 仕入先 |
| `supplierName` | string | サーバ | 仕入先の名前 (削除済みでも引く) |
| `purchaseOrderId`, `purchaseOrderNo` | | サーバ | 発注から作った入荷予定はその発注 (ほかは `null`) |
| `slipNo` | string(50)? | 入力 | 仕入先の納品書番号 |
| `expectedDate` | date? | 入力 | 入荷予定日 |
| `status` | enum | サーバ | `Draft` (入荷予定) / `Received` (受領済み) / `Cancelled` (キャンセル) |
| `note` | string(500)? | 入力 | |
| `receivedAt`, `receivedByStaffId` | | 入力 (受領時) | 受領の日時 (省略すると受付時刻) と担当 |
| `cancelledAt` | datetime? | サーバ | |
| `lines[]` | object[] | 入力 | `{ id, lineNo, productId, productCode, productName, quantity, receivedQuantity, cost }`。`quantity` は予定の数、`receivedQuantity` は受領した数 (受領まで `null`)、`cost` は仕入単価 |
| `createdAt`, `updatedAt`, `version` | | サーバ | |

#### 店舗間移動の項目 (`InventoryTransferResponseItem`)

| フィールド | 型 | 区分 | 説明 |
| --- | --- | --- | --- |
| `id` | guid | サーバ | |
| `transferNo` | string | サーバ | `{出荷店コード}-T-{連番:000000}` (出荷店ごとの連番) |
| `fromStoreId`, `fromStoreName` | | 入力 / サーバ | 出荷店 |
| `toStoreId`, `toStoreName` | | 入力 / サーバ | 入荷店 (出荷店と別の店舗) |
| `status` | enum | サーバ | `Requested` (出荷待ち) / `Shipped` (受領待ち) / `Received` (受領済み) / `Cancelled` (キャンセル) |
| `note` | string(500)? | 入力 | |
| `shippedAt`, `shippedByStaffId` | | 入力 (出荷時) | |
| `receivedAt`, `receivedByStaffId` | | 入力 (受領時) | |
| `cancelledAt` | datetime? | サーバ | |
| `lines[]` | object[] | 入力 | `{ id, lineNo, productId, productCode, productName, quantity, receivedQuantity }`。`quantity` は依頼・出荷の数 |
| `createdAt`, `updatedAt`, `version` | | サーバ | |

#### 入荷・店舗間移動のエンドポイント

| Method | Path | 用途 | 概要 |
| --- | --- | --- | --- |
| GET | `/inventory/receipts?storeId&supplierId&status&from&to&sort&desc&page&size` | 端末 / 管理 | 入荷一覧 (`InventoryReceiptResponse`)。`from` / `to` は入荷予定日、`sort` = `createdAt` / `expectedDate` |
| GET | `/inventory/receipts/{id}` | 端末 / 管理 | 詳細 |
| POST | `/inventory/receipts` | 管理 | 入荷予定の登録 (`InventoryReceiptCreateRequest`: 店舗・仕入先・納品書番号・入荷予定日・備考・明細 `{ productId, quantity, cost }`)。`201`。店舗・仕入先がなければ `422` `VALIDATION_ERROR`、商品がなければ `422` `PRODUCT_NOT_FOUND` |
| POST | `/inventory/receipts/{id}/receive` | 端末 / 管理 | 受領 (`InventoryReceiptReceiveRequest { staffId, receivedAt, lines: [{ lineId, quantity }] }`)。入荷予定のときだけ |
| POST | `/inventory/receipts/{id}/cancel` | 管理 | キャンセル (入荷予定のときだけ) |
| GET | `/inventory/transfers?storeId&fromStoreId&toStoreId&status&open&sort&desc&page&size` | 端末 / 管理 | 移動一覧 (`InventoryTransferResponse`)。`storeId` は出荷店か入荷店のどちらか、`open=true` は未受領 (出荷待ち・受領待ち) だけ。`sort` = `createdAt` / `transferNo` |
| GET | `/inventory/transfers/{id}` | 端末 / 管理 | 詳細 |
| POST | `/inventory/transfers` | 管理 | 依頼 (`InventoryTransferCreateRequest`: 出荷店・入荷店・備考・明細 `{ productId, quantity }`)。`201`。同じ店舗どうしは `400`、店舗がなければ `422` `VALIDATION_ERROR`、商品がなければ `422` `PRODUCT_NOT_FOUND` |
| POST | `/inventory/transfers/{id}/ship` | 管理 | 出荷 (`InventoryTransferShipRequest { staffId, shippedAt }`)。出荷待ちのときだけ |
| POST | `/inventory/transfers/{id}/receive` | 端末 / 管理 | 受領 (`InventoryTransferReceiveRequest`、入荷の受領と同じ形)。受領待ちのときだけ |
| POST | `/inventory/transfers/{id}/cancel` | 管理 | キャンセル (出荷待ちのときだけ) |

- 状態に合わない受領・出荷・キャンセルは `422` (`INVENTORY_RECEIPT_STATUS_INVALID` / `INVENTORY_TRANSFER_STATUS_INVALID`)、伝票がなければ `404`
- 受領の `lines` に含めない明細は、予定 (移動は出荷) の数で受け取る。  
  数が `0` の明細は在庫を動かさない
- 入荷の受領は入荷する店舗に `Receive` (+)、移動の出荷は出荷店に `TransferOut` (−依頼の数)、移動の受領は入荷店に `TransferIn` (+受領した数) を記録する。  
  出荷と受領の差は移動の明細に残る
- 端末の受領はオンライン限定 (自店の入荷予定と、自店宛に出荷済みの移動)

#### 発注の項目 (`PurchaseOrderResponseItem`)

発注は仕入先への注文の記録で、受領は発注で作った入荷予定で行う ([D-17](decisions.md#d-17-発注は入荷予定を作りその受領とキャンセルに合わせる))。

| フィールド | 型 | 区分 | 説明 |
| --- | --- | --- | --- |
| `id` | guid | サーバ | |
| `purchaseOrderNo` | string | サーバ | `{店舗コード}-P-{連番:000000}` (店舗ごとの連番) |
| `storeId` | guid | 入力 | 発注する店舗 (入荷する店舗。登録の後は変えられない) |
| `supplierId` | guid | 入力 | 仕入先 |
| `supplierName` | string | サーバ | 仕入先の名前 (削除済みでも引く) |
| `status` | enum | サーバ | `Draft` (下書き) / `Ordered` (発注済み) / `Received` (入荷済み) / `Cancelled` (キャンセル) |
| `expectedDate` | date? | 入力 | 希望納期 (作る入荷予定の入荷予定日) |
| `note` | string(500)? | 入力 | |
| `orderedAt`, `orderedBy` | | サーバ | 発注の日時と、発注した管理画面のアカウント名 |
| `receiptId` | guid? | サーバ | 発注で作った入荷予定 |
| `cancelledAt` | datetime? | サーバ | |
| `totalCost` | money | サーバ | 明細の数量 × 仕入単価の合計 (単価のない明細は含めない) |
| `lines[]` | object[] | 入力 | `{ id, lineNo, productId, productCode, productName, quantity, cost, receivedQuantity }`。`receivedQuantity` は入荷予定で受領した数 (受領まで `null`) |
| `createdAt`, `updatedAt`, `version` | | サーバ | |

#### 発注のエンドポイント

| Method | Path | 用途 | 概要 |
| --- | --- | --- | --- |
| GET | `/inventory/purchase-orders?storeId&supplierId&status&open&from&to&sort&desc&page&size` | 管理 | 発注一覧 (`PurchaseOrderResponse`)。`open=true` は未完了 (下書き・発注済み) だけ、`from` / `to` は希望納期、`sort` = `createdAt` / `expectedDate` / `purchaseOrderNo` |
| GET | `/inventory/purchase-orders/{id}` | 管理 | 詳細 |
| GET | `/inventory/purchase-orders/{id}/pdf` | 管理 | 発注書 (PDF) |
| POST | `/inventory/purchase-orders` | 管理 | 登録 (`PurchaseOrderCreateRequest`: 店舗・仕入先・希望納期・備考・明細 `{ productId, quantity, cost }`)。下書きで `201`。店舗・仕入先がなければ `422` `VALIDATION_ERROR`、商品がなければ `422` `PRODUCT_NOT_FOUND` |
| PUT | `/inventory/purchase-orders/{id}` | 管理 | 変更 (`PurchaseOrderUpdateRequest`: 仕入先・希望納期・備考・明細・`version`)。下書きのときだけで、明細は置き換える |
| POST | `/inventory/purchase-orders/{id}/order` | 管理 | 発注 (本文なし)。下書きのときだけ。明細を写した入荷予定を作り、`receiptId` に入れる。仕入先が削除済みなら `422` `VALIDATION_ERROR` |
| POST | `/inventory/purchase-orders/{id}/cancel` | 管理 | キャンセル。下書きと発注済みのときで、発注済みは入荷予定もキャンセルする |

- 状態に合わない変更・発注・キャンセルは `422` (`PURCHASE_ORDER_STATUS_INVALID`)、版が合わない変更は `409` (`VERSION_MISMATCH`)、発注がなければ `404`
- 入荷予定を受領すると発注は入荷済みに、入荷予定をキャンセルすると発注もキャンセルになる

### 3.17 レポート (Reports)

取引テーブルからの集計。  
取消済みは除外し、返品は負として扱う。  
管理画面用だが端末の売上照会にも使える。

| Method | Path | 用途 | 概要 |
| --- | --- | --- | --- |
| GET | `/reports/sales/summary?storeId&from&to&groupBy=` | 管理 / 端末 | 売上集計 (`ReportSalesSummaryResponse`)。`groupBy` = `day` / `store` / `hour` / `terminal` / `staff` / `paymentMethod` / `taxRate` / `category` (不正なら 400) |
| GET | `/reports/sales/products?storeId&from&to&categoryId&sort=netSales\|quantity&size` | 管理 | 商品別売上 (`ReportProductSalesResponse`)。`size` は既定 50 |
| GET | `/reports/sales/summary/csv`, `/reports/sales/products/csv` | 管理 | CSV 出力 (同じクエリ、CsvHelper、BOM 付き UTF-8。summary は合計行付き) |
| GET | `/reports/sales/daily/pdf?storeId&date` | 管理 | 売上日報の PDF (店舗 × 営業日、[D-21](decisions.md#d-21-帳票はサーバが-pdf-にする)) |

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
残り (D − Σ alloc_i) を、raw_i − alloc_i の大きい明細から 1 ずつ加算 (同値なら明細の並び順で先の明細から)
allocatedDiscountAmount_i = alloc_i
netAmount_i       = base_i − alloc_i
```

取引値引の合計 `D` は Σ base_i を超えない (超えれば検証エラー)。

### 4.3 税

税率 × 内税/外税 のグループごとに合計してから税額を計算する ([D-03](decisions.md#d-03-金額は端末が計算しサーバが検証する))。  
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
充当分にはポイントを付けない ([D-04](decisions.md#d-04-ポイントは商品ごとの還元率で付け1-円として使う))。

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

この例は `Pos.Domain.Tests` の `SalesLogicTests.CalculateExample` で確かめている。

---

## 5. エラーコード

| HTTP | `errorCode` | 発生箇所 |
| --- | --- | --- |
| 400 | `VALIDATION_ERROR` | 入力形式・必須項目 (詳細は `errors`) |
| 401 | (本文なし) | ログインもトークンもない、トークンが解除済み・不明、端末が無効・削除済み ([§2.6](#26-認証認可)) |
| 403 | (本文なし) | 役割が足りない (管理者だけの API、端末からの管理だけの API) |
| 403 | `TERMINAL_MISMATCH` | 端末のトークンと、本文・クエリの店舗・端末が一致しない |
| 429 | (本文なし) | ログイン・ペアリングの試行回数の上限 |
| 413 / 415 | `VALIDATION_ERROR` | 本文をそのまま送る API (商品画像・CSV 取込) の大きさ・`Content-Type` |
| 422 | `VALIDATION_ERROR` | 取引の入力の誤り (明細なし、数量・単価・値引の値、値引の超過など)、取消済みの取引の取消、参照先 (店舗・会員・仕入先) がない、有効な `Points` / `Deposit` の支払方法の 2 件目、部門の親に自身、商品画像の形式、CSV 取込の誤りのある行 (`errors` は行番号ごと) |
| 404 | `NOT_FOUND` | 対象なし |
| 409 | `DUPLICATE_ID_MISMATCH` | 同一 `id` で内容が異なる再送 |
| 409 | `VERSION_MISMATCH` | 楽観ロック失敗 |
| 409 | `DUPLICATE_CODE` | コード・バーコード・会員番号の重複 |
| 409 | `TERMINAL_HAS_OPEN_SHIFT` | 開設中シフトがある端末で再開設 |
| 409 | `ALREADY_CLOSED` | 締め済みの営業日の締め |
| 422 | `SHIFT_NOT_FOUND` / `SHIFT_CLOSED` / `SHIFT_TERMINAL_MISMATCH` | 取引・入出金・取消・前受金の受取と返金。`SHIFT_NOT_FOUND` はシフトのない営業日の締め、`SHIFT_CLOSED` は精算済みのシフトへの実査金額の違う精算にも使う |
| 422 | `SHIFT_STILL_OPEN` | 未精算のシフトがある営業日の締め |
| 422 | `DAY_CLOSED` | 締め済みの営業日の取引の取消 |
| 422 | `ORDER_NOT_FOUND` / `ORDER_NOT_READY` | 受注から会計したが、受注が見つからない (他店を含む) / 引き渡し待ちでない (登録の間に変わったときを含む)。前受金のシフトの店舗が受注の店舗と違うときも `ORDER_NOT_FOUND` |
| 422 | `ORDER_STATUS_INVALID` | 受注の状態に合わない変更・入荷・キャンセル・前受金 |
| 422 | `ORDER_DEPOSIT_INVALID` | 前受金の重複・金額・支払方法の誤り、前受金のない返金、前受金のある受注のキャンセル |
| 422 | `INVENTORY_RECEIPT_STATUS_INVALID` / `INVENTORY_TRANSFER_STATUS_INVALID` | 入荷・店舗間移動の状態に合わない受領・出荷・キャンセル |
| 422 | `PURCHASE_ORDER_STATUS_INVALID` | 発注の状態に合わない変更・発注・キャンセル |
| 422 | `DUPLICATE_RECEIPT_NO` | レシート番号重複 |
| 422 | `PRODUCT_NOT_FOUND` | 取引・受注・入荷予定・店舗間移動・発注の明細の商品がない |
| 422 | `PRICE_OVERRIDE_NOT_ALLOWED` | 売価変更不可商品の単価相違 |
| 422 | `CALCULATION_MISMATCH` | 計算項目不一致 (`expected` にサーバ計算) |
| 422 | `PAYMENT_MISMATCH` | 支払合計・釣銭の不整合、前受金の充当額が受注の前受金と違う、返品のポイント返還額の違いと前受金での返金 |
| 422 | `CUSTOMER_REQUIRED` | ポイントの付与・利用 (返品では取消・返還) に顧客なし |
| 422 | `ORIGINAL_NOT_FOUND` / `ORIGINAL_NOT_RETURNABLE` | 返品の元取引 |
| 422 | `RETURN_QUANTITY_EXCEEDED` | 返品数量超過 |
| 422 | `HAS_RETURNS` | 返品済み取引の取消 |
| 422 | `IN_USE` | 使用中マスタの削除 |
| 422 | `PAIRING_CODE_INVALID` | ペアリングコードの不一致・期限切れ・使用済み、無効な端末 |
| 422 | `STAFF_INVALID` | 担当がその店舗 (または本部) の有効なスタッフでない (取引の登録・取消、前受金の受取・返金) |
| 422 | `APPROVAL_REQUIRED` | 承認が必要な値引・レジ係の取消に、店長以上の承認者がない (承認者が無効・役割不足を含む) |
| 500 | (なし) | 想定外の例外 (Problem Details。`errorCode` は付かない) |

警告 (受理するが、登録した `201` の応答の `warnings[]` に含める): `POINT_BALANCE_NEGATIVE` (ポイント残高不足)、`PRODUCT_INACTIVE` (販売停止中の商品)、`DAY_ALREADY_CLOSED` (締め済みの営業日の取引)。

---

## 6. 端末側の同期フロー

API 設計が前提にしている MAUI 側の動き。  
通信は `HttpService` (HttpClient + System.Text.Json。失敗時は Problem Details を `ApiResult<T>` で返す) + `NetworkService` (接続確認・インジケータ・エラー通知) を使う ([D-28](decisions.md#d-28-端末は-viewmodel-から-service-と-usecase-を呼ぶ))。

1. **初回**: 設定 QR (`ApiEndPoint` / `PairingCode`) を読み取るか入力して `POST /terminals/pair` でトークン・端末・店舗を受け取り、`GET /sync/masters` (全件) と `GET /inventory?storeId=` (自店) をローカル DB (SQLite) に保存する。  
   会員はローカルに持たず、都度オンラインで照会する
2. **定期**: 5 分ごとに `GET /sync/masters?since={前回の serverTime}` と `GET /inventory?storeId=&updatedSince=` で差分を取り込み、1 分ごとに `POST /terminals/me/heartbeat` を送る
3. **書き込みは Outbox**: 端末で発生した書き込み (`XxxRequest`) を発生順にローカル DB の `Outbox` テーブルへ JSON で保存し、バックグラウンドで順に送信する
   - 順序: シフト開設 → 取引・取消・入出金・棚卸と調整 (発生順) → 精算
   - `200` / `201` で完了。  
     `401` と `429` を除く 4xx は「要確認」として止め、後続を送らない (シフトの整合のため。要確認は端末の設定画面で再送か破棄を選ぶ)。  
     5xx・通信エラー・`429` は指数バックオフ (最大 5 分) で再送する。  
     `401` を受けたら登録し直すまで送らず、登録し直すと続きを送る
   - `id` を端末が採番しているので、応答を受け取る前に切断しても再送で重複しない
4. **オンライン限定の操作**: 会員の照会・登録・変更、他店在庫照会、シリアル番号での取引検索、受注と前受金、入荷・移動の受領、売上照会。  
   レシート番号検索 (返品) はオンラインならサーバ、オフラインならローカルの取引から探し、ローカルにある取引の返品はオフラインでもできる
