# POS サーバ DB 設計

[api-design.md](api-design.md) に対応するサーバのデータベース (SQLite) の設計と、端末のローカル DB の概要 (§6)。  
設計方針は [decisions.md](decisions.md)、プロジェクト構成は [architecture.md](architecture.md)。

- [1. 前提](#1-前提)
- [2. ER 図](#2-er-図)
- [3. テーブル定義](#3-テーブル定義)
- [4. DDL 例](#4-ddl-例)
- [5. 整合性と更新の単位](#5-整合性と更新の単位)
- [6. 端末ローカル DB (SQLite) の概要](#6-端末ローカル-db-sqlite-の概要)

---

## 1. 前提

| 項目 | 内容 |
| --- | --- |
| RDBMS | **SQLite** (`Microsoft.Data.Sqlite`)。接続文字列は `Data Source=pos.db;Cache=Shared;Pooling=True;Foreign Keys=True`。起動時の PRAGMA で WAL にする (DB ファイルに残るので全接続に効く) |
| データアクセス | `Usa.Smart.Data.Accessor` の `[DataAccessor]` + 2-way SQL ファイル (`Accessors/Sql/{Accessor}.{Method}.sql`)。ORM は使わない。SQL は Accessor だけが持ち、Accessor を使うのは `Services/` の Service だけ ([D-27](decisions.md#d-27-サーバは-service-に手順を集めsql-は-accessor-に置く)) |
| スキーマ作成 | 起動時に `Host/Assets/Data/Schema.sql` (`CREATE TABLE IF NOT EXISTS` + `CREATE INDEX IF NOT EXISTS`) を読んで実行する。マイグレーションは持たない (列を足すときは下の「スキーマの変更」) |
| 命名 | テーブル = 複数形 PascalCase (`Transactions`)、列 = PascalCase。FK は `〜Id`。エンティティクラスは `{Table 単数}Entity` (`TransactionEntity`) |
| 主キー | `guid` を **TEXT (36 文字。`Microsoft.Data.Sqlite` の既定で大文字)** で保存。端末発のデータは端末が GUID v7 を採番 ([D-32](decisions.md#d-32-端末発の書き込みは端末が-id-を採番する)) |
| 列挙型 | TEXT (列挙名)。汎用 `EnumTextConverter<T>` を `DataProfile` (`[AccessorProfile]`) に列挙型ごとに宣言し、各 Accessor が `[ExecuteConfig(typeof(DataProfile))]` で参照する ([D-30](decisions.md#d-30-金額は-decimalid-は-guid-v7日時は-utc-にする))。値は API の enum と同じ |
| 論理削除 | マスタ系は `IsDeleted`。差分同期で削除も伝える必要があるので、通常の照会側で `IsDeleted = 0` を明示する |
| 監査列 | `CreatedAt` / `UpdatedAt` (UTC)。マスタ系・会員・アカウントと、状態を持つ伝票 (受注・入荷・発注・移動) は `Version` (INTEGER、更新ごとに +1) を持ち、編集の更新は版を条件にする (楽観ロック) |
| 履歴 | 取引・入出金・在庫変動・ポイント履歴・前受金は**消さない**。取引の取消は `Status` と取消の列だけを更新し、在庫とポイントは逆方向の履歴を足す。元明細は返品と返品の取消で `ReturnedQuantity` だけを加減する。シフトは精算で集計列と `Status` を確定する。日次締めは締め解除で行ごと消し、締め直しで作り直す |
| スナップショット | 取引明細は商品名・単価・税率・還元率を販売時点の値で保持する。マスタ変更が過去の取引に影響しない |
| 外部キー | `FOREIGN KEY` は宣言するが、SQLite の既定では強制されないため、接続文字列の `Foreign Keys=True` で接続ごとに有効にする |

型の表記 (C# ↔ SQLite):

| 表記 | C# | SQLite | 備考 |
| --- | --- | --- | --- |
| `guid` | `Guid` | `TEXT` | `Microsoft.Data.Sqlite` の既定 (36 文字、大文字)。生成コードは `GetGuid` で読む |
| `string(n)` | `string` | `TEXT` | 長さはアプリ側で検証 (SQLite は長さ制約を強制しない) |
| `money` | `decimal` | `NUMERIC` | 円。`Microsoft.Data.Sqlite` は `decimal` を TEXT で書くが、NUMERIC 親和性により数値 (INTEGER / REAL) に変換されて保存される ([D-30](decisions.md#d-30-金額は-decimalid-は-guid-v7日時は-utc-にする)) |
| `rate` | `decimal` | `NUMERIC` | `0.1` (REAL として保存)。集計しない |
| `qty` | `decimal` | `NUMERIC` | 数量 (小数可) |
| `decimal` | `decimal` | `NUMERIC` | 金額か率 (値引の値。`Type` で決まる) |
| `int` | `int` | `INTEGER` | |
| `bool` | `bool` | `INTEGER` | 0 / 1 |
| `datetime` | `DateTime` (UTC) | `TEXT` | `yyyy-MM-dd HH:mm:ss.fffffff` (`Microsoft.Data.Sqlite` の既定書式。文字列比較で範囲検索できる)。`DateTimeTextConverter` で UTC に固定して読み書きする |
| `date` | `DateOnly` | `TEXT` | `yyyy-MM-dd` (`DateOnlyTextConverter`) |
| `enum` | enum | `TEXT` | 列挙名 |
| `blob` | `byte[]` | `BLOB` | ハッシュと画像 |

スキーマの変更:

起動時の `CREATE TABLE IF NOT EXISTS` は既存のテーブルに列を足さないので、機能を足すときは既存のテーブルに列を足さず、新しいテーブルで持つ (受注と取引は `Orders.TransactionId`、発注と入荷予定は `PurchaseOrders.ReceiptId` で結ぶ)。  
列を足すしかないときは `Schema.sql` の `CREATE TABLE` に書いたうえで、起動時に `SchemaHelper.EnsureColumnAsync` (`PRAGMA table_info` で確かめて `ALTER TABLE ADD COLUMN`) で既存の DB にも足し、既存の行の値を補う。  
端末のローカル DB も同じ方法で列を足す。

| DB | 起動時に足す列 | 既存の行 |
| --- | --- | --- |
| サーバ | `PaymentMethods.ShortName` | 列を足したときに、初期データのコード (`CASH` など 6 件) の行へ初期データと同じボタン名を入れ、`UpdatedAt` / `Version` を進める (端末は差分同期で受け取る) |
| サーバ | `Shifts.DepositCashIn` / `DepositCashOut` | `DEFAULT 0` (前受金より前に精算したシフトは 0) |
| 端末 | `PaymentMethods.ShortName` | 空のまま (ボタンは `Name` を出し、同期で行が届くと入る) |
| 端末 | `Staff.PinHash` | `SyncState` の `ServerTime` を消し、次のマスタ同期を全件にする (PIN は全員分が要る) |

後から足した初期データ (前受金の支払方法 `DEPOSIT`) は起動時に足す (種別 `Deposit`、コード `DEPOSIT`、同じ Id のいずれかの行があれば足さない)。

---

## 2. ER 図

### 2.1 マスタ

```mermaid
erDiagram
    Settings {
        int Id PK
        string TaxRounding
        string PointBasis
    }
    Stores ||--o{ Terminals : has
    Stores |o--o{ Staff : belongs
    Categories |o--o{ Categories : parent
    Categories ||--o{ Products : has
    TaxRates ||--o{ Products : applies
    Products ||--o| ProductImages : "image (nullable)"
    Terminals ||--o{ TerminalTokens : "registration"
    Stores {
        guid Id PK
        string Code UK
        string Name
    }
    Terminals {
        guid Id PK
        guid StoreId FK
        int TerminalNo
        int LastReceiptSeq
    }
    Staff {
        guid Id PK
        string Code UK
        string Role
        blob PinHash
    }
    TerminalTokens {
        guid Id PK
        guid TerminalId FK
        string PairingCode
        blob TokenHash
    }
    Accounts {
        guid Id PK
        string Name UK
        string Role
    }
    Categories {
        guid Id PK
        string Code UK
        guid ParentId FK
    }
    TaxRates {
        guid Id PK
        rate Rate
        string Kind
    }
    Products {
        guid Id PK
        string Code UK
        string Barcode UK
        money Price
        bool TaxIncluded
        rate PointRate
        bool RequiresSerial
        bool TrackInventory
        string ImageUrl
    }
    ProductImages {
        guid ProductId PK
        blob Data
    }
    Discounts {
        guid Id PK
        string Type
        string Scope
    }
    PaymentMethods {
        guid Id PK
        string Kind
        bool AllowsChange
    }
    AdjustmentReasons {
        guid Id PK
        string Code UK
    }
```

### 2.2 取引・シフト

```mermaid
erDiagram
    Terminals ||--o{ Shifts : opens
    Shifts ||--o{ Transactions : contains
    Shifts ||--o{ CashEvents : has
    Shifts ||--o{ ShiftDenominations : has
    Transactions ||--|{ TransactionLines : has
    Transactions ||--o{ TransactionDiscounts : has
    Transactions ||--|{ TransactionTaxSummaries : has
    Transactions ||--|{ TransactionPayments : has
    PaymentMethods ||--o{ TransactionPayments : "paid by"
    Transactions ||--o| TransactionDeliveries : has
    Transactions |o--o{ Transactions : "original (Return)"
    TransactionLines ||--o{ TransactionLineSerials : has
    TransactionLines |o--o{ TransactionLines : "original line (Return)"
    TransactionLines |o--o{ TransactionDiscounts : "line discount"
    Customers |o--o{ Transactions : buys
    Stores ||--o{ DailyClosings : "closes (per BusinessDate)"
    DailyClosings ||--o{ DailyClosingPayments : has
    DailyClosings ||--o{ DailyClosingTaxes : has
    Orders ||--|{ OrderLines : has
    Orders |o--o| Transactions : "completed by"
    Orders ||--o{ OrderDeposits : has
    Shifts ||--o{ OrderDeposits : records
    PaymentMethods ||--o{ OrderDeposits : "paid by"
    Customers |o--o{ Orders : orders
    Shifts {
        guid Id PK
        guid TerminalId FK
        string Status
        date BusinessDate
        money OpeningCash
        money ExpectedCash
        money ActualCash
    }
    Transactions {
        guid Id PK
        string Type
        string Status
        string ReceiptNo UK
        guid ShiftId FK
        guid CustomerId FK
        guid OriginalTransactionId FK
        money Total
        int PointsEarned
        int PointsRedeemed
    }
    TransactionLines {
        guid Id PK
        guid TransactionId FK
        guid ProductId FK
        money UnitPrice
        qty Quantity
        money NetAmount
        guid OriginalLineId FK
        qty ReturnedQuantity
    }
    TransactionPayments {
        guid Id PK
        guid PaymentMethodId FK
        money Amount
        money TenderedAmount
    }
    CashEvents {
        guid Id PK
        string Type
        money Amount
    }
    DailyClosings {
        guid Id PK
        guid StoreId FK
        date BusinessDate
        money NetSales
        bool HasLateTransactions
    }
    Orders {
        guid Id PK
        string OrderNo UK
        string Type
        string Status
        guid CustomerId FK
        guid TransactionId FK
    }
    OrderDeposits {
        guid Id PK
        guid OrderId FK
        guid ShiftId FK
        string Type
        guid PaymentMethodId FK
        money Amount
    }
```

### 2.3 在庫・顧客

```mermaid
erDiagram
    Stores ||--o{ InventoryLevels : has
    Products ||--o{ InventoryLevels : has
    Stores ||--o{ InventoryChanges : has
    Products ||--o{ InventoryChanges : has
    AdjustmentReasons |o--o{ InventoryChanges : reason
    Transactions |o--o{ InventoryChanges : "reference"
    Suppliers ||--o{ InventoryReceipts : supplies
    Stores ||--o{ InventoryReceipts : receives
    InventoryReceipts ||--o{ InventoryReceiptLines : has
    InventoryReceipts |o--o{ InventoryChanges : "reference"
    Suppliers ||--o{ PurchaseOrders : "ordered from"
    Stores ||--o{ PurchaseOrders : orders
    PurchaseOrders ||--o{ PurchaseOrderLines : has
    PurchaseOrders |o--o| InventoryReceipts : creates
    Stores ||--o{ InventoryTransfers : "from / to"
    InventoryTransfers ||--o{ InventoryTransferLines : has
    InventoryTransfers |o--o{ InventoryChanges : "reference"
    Customers ||--o{ PointHistories : has
    Transactions |o--o{ PointHistories : "reference"
    InventoryLevels {
        guid StoreId PK
        guid ProductId PK
        qty Quantity
    }
    InventoryChanges {
        guid Id PK
        string Type
        qty QuantityDelta
        qty QuantityAfter
        string ReferenceType
        guid ReferenceId
    }
    InventoryReceipts {
        guid Id PK
        guid SupplierId FK
        string SlipNo
        string Status
    }
    PurchaseOrders {
        guid Id PK
        string PurchaseOrderNo UK
        guid SupplierId FK
        guid ReceiptId FK
        string Status
    }
    InventoryTransfers {
        guid Id PK
        string TransferNo UK
        guid FromStoreId FK
        guid ToStoreId FK
        string Status
    }
    Customers {
        guid Id PK
        string Code UK
        int PointBalance
    }
    PointHistories {
        guid Id PK
        string Type
        int Points
        int BalanceAfter
    }
```

---

## 3. テーブル定義

「共通列」= `CreatedAt datetime`, `UpdatedAt datetime`。  
マスタ系はさらに `IsDeleted bool`, `Version int`。

### 3.1 マスタ

#### Settings (会社設定、1 行)

| 列 | 型 | NULL | 説明 |
| --- | --- | --- | --- |
| Id | int | | PK。常に 1 |
| CompanyName | string(100) | | |
| Currency | string(3) | | `JPY` |
| TaxRounding | enum | | `Floor` / `Round` / `Ceiling` |
| PointBasis | enum | | `TaxIncluded` / `TaxExcluded` |
| BusinessDayStartTime | string(5) | | `05:00` |
| UpdatedAt, Version | | | |

#### Stores (店舗)

| 列 | 型 | NULL | 説明 |
| --- | --- | --- | --- |
| Id | guid | | PK |
| Code | string(10) | | UQ。レシート番号の一部 |
| Name | string(100) | | |
| PostalCode | string(10) | ○ | |
| Address | string(200) | ○ | |
| Phone | string(20) | ○ | |
| RegistrationNo | string(14) | ○ | 適格請求書発行事業者登録番号 |
| ReceiptHeader | string(500) | ○ | |
| ReceiptFooter | string(500) | ○ | |
| TimeZone | string(50) | | `Asia/Tokyo` |
| IsActive | bool | | |
| 共通列 + IsDeleted, Version | | | |

索引: `UQ(Code)`, `IX(UpdatedAt)`

#### Terminals (レジ端末)

| 列 | 型 | NULL | 説明 |
| --- | --- | --- | --- |
| Id | guid | | PK |
| StoreId | guid | | FK → Stores |
| TerminalNo | int | | 店舗内番号 |
| Name | string(50) | | |
| LastReceiptSeq | int | | 最終レシート連番。取引の登録で大きい方に更新し、`UpdatedAt` と版は進めない (差分同期には載らず、入れ直した端末がペアリングの全件同期で受け取って連番を続ける) |
| LastSeenAt | datetime | ○ | 最終通信 (取引の登録・ペアリング・ハートビートで更新) |
| AppVersion | string(50) | ○ | 端末が送ったアプリのバージョン (ペアリングとハートビートで更新) |
| IsActive | bool | | |
| 共通列 + IsDeleted, Version | | | |

索引: `UQ(StoreId, TerminalNo)`, `IX(UpdatedAt)`

#### Staff (スタッフ)

| 列 | 型 | NULL | 説明 |
| --- | --- | --- | --- |
| Id | guid | | PK |
| Code | string(20) | | UQ |
| Name | string(50) | | |
| Role | enum | | `Cashier` / `Manager` / `Admin` |
| StoreId | guid | ○ | FK → Stores。NULL = 本部 |
| PinHash | blob | ○ | PIN のハッシュ (`PinHasher`: PBKDF2 のソルト 16 + ハッシュ 32 バイト)。端末向けの同期にだけ載せる。NULL = 未設定 (端末で担当に選べない) |
| IsActive | bool | | |
| 共通列 + IsDeleted, Version | | | |

索引: `UQ(Code)`, `IX(StoreId)`, `IX(UpdatedAt)`

#### Categories (部門)

| 列 | 型 | NULL | 説明 |
| --- | --- | --- | --- |
| Id | guid | | PK |
| Code | string(20) | | UQ |
| Name | string(100) | | |
| ParentId | guid | ○ | FK → Categories (自己参照) |
| SortOrder | int | | |
| 共通列 + IsDeleted, Version | | | |

索引: `UQ(Code)`, `IX(ParentId)`, `IX(UpdatedAt)`

#### TaxRates (税率)

| 列 | 型 | NULL | 説明 |
| --- | --- | --- | --- |
| Id | guid | | PK |
| Code | string(10) | | UQ (`STD` / `RED` / `EXEMPT`) |
| Name | string(50) | | |
| Rate | rate | | `0.1000` |
| Kind | enum | | `Standard` / `Reduced` / `Exempt` |
| IsDefault | bool | | |
| SortOrder | int | | |
| 共通列 + IsDeleted, Version | | | |

索引: `UQ(Code)`, `IX(UpdatedAt)`

#### Products (商品)

| 列 | 型 | NULL | 説明 |
| --- | --- | --- | --- |
| Id | guid | | PK |
| Code | string(20) | | UQ 商品コード |
| Barcode | string(20) | ○ | UQ (NULL を除く)。JAN / EAN |
| Name | string(100) | | |
| Kana | string(100) | ○ | |
| Brand | string(50) | ○ | |
| ModelNo | string(50) | ○ | |
| CategoryId | guid | | FK → Categories |
| Kind | enum | | `Goods` / `Service` |
| Price | money | | |
| TaxIncluded | bool | | |
| TaxRateId | guid | | FK → TaxRates |
| Cost | money | ○ | |
| PointRate | rate | | |
| RequiresSerial | bool | | |
| TrackInventory | bool | | |
| AllowsPriceOverride | bool | | |
| Unit | string(10) | ○ | |
| ImageUrl | string(500) | ○ | 画像の URL (`/api/v1/products/{id}/image?v={内容のハッシュ}`)。画像を変えると `UpdatedAt` / `Version` も進める |
| IsActive | bool | | |
| 共通列 + IsDeleted, Version | | | |

索引: `UQ(Code)`, `UQ(Barcode) WHERE Barcode IS NOT NULL`, `IX(CategoryId)`, `IX(Name)`, `IX(Kana)`, `IX(UpdatedAt)`

#### ProductImages (商品画像)

画像は DB に持つ ([D-18](decisions.md#d-18-商品画像は-db-に持ちハッシュ付きの-url-で配る))。  
形式 (JPEG / PNG) は `Data` の先頭のバイトで判定し、列には持たない。

| 列 | 型 | NULL | 説明 |
| --- | --- | --- | --- |
| ProductId | guid | | PK, FK → Products |
| Data | blob | | 画像 (2 MB まで) |
| UpdatedAt | datetime | | |

#### Discounts (値引定義)

| 列 | 型 | NULL | 説明 |
| --- | --- | --- | --- |
| Id | guid | | PK |
| Code | string(20) | | UQ |
| Name | string(50) | | |
| Type | enum | | `Amount` / `Percent` |
| Value | decimal | | 金額または率 |
| Scope | enum | | `Line` / `Transaction` |
| RequiresApproval | bool | | |
| IsActive | bool | | |
| SortOrder | int | | |
| 共通列 + IsDeleted, Version | | | |

索引: `UQ(Code)`, `IX(UpdatedAt)`

#### PaymentMethods (支払方法)

| 列 | 型 | NULL | 説明 |
| --- | --- | --- | --- |
| Id | guid | | PK |
| Code | string(20) | | UQ |
| Name | string(50) | | |
| ShortName | string(10) | ○ | 端末の支払ボタンに出す短い名前 (省略時は Name) |
| Kind | enum | | `Cash` / `Card` / `Qr` / `EMoney` / `Voucher` / `Points` / `Credit` / `Other` / `Deposit` (受注の前受金を会計で充てる) |
| AllowsChange | bool | | |
| RequiresReference | bool | | |
| IsActive | bool | | |
| SortOrder | int | | |
| 共通列 + IsDeleted, Version | | | |

索引: `UQ(Code)`, `IX(UpdatedAt)`

アプリ側制約: `Kind = Points` と `Kind = Deposit` の有効な行はそれぞれ 1 件まで (2 件目を有効にする登録・更新は拒否する)。

#### AdjustmentReasons (在庫調整理由)

| 列 | 型 | NULL | 説明 |
| --- | --- | --- | --- |
| Id | guid | | PK |
| Code | string(20) | | UQ |
| Name | string(50) | | |
| SortOrder | int | | |
| IsActive | bool | | |
| 共通列 + IsDeleted, Version | | | |

索引: `UQ(Code)`, `IX(UpdatedAt)`

#### Suppliers (仕入先)

| 列 | 型 | NULL | 説明 |
| --- | --- | --- | --- |
| Id | guid | | PK |
| Code | string(20) | | UQ |
| Name | string(100) | | |
| Phone | string(20) | ○ | |
| Email | string(100) | ○ | |
| Note | string(500) | ○ | |
| IsActive | bool | | 入荷予定と発注の登録で選べる |
| 共通列 + IsDeleted, Version | | | |

索引: `UQ(Code)`

入荷と発注は削除済みの仕入先の名前も引く。  
端末には同期しない。

### 3.2 顧客

#### Customers (顧客)

| 列 | 型 | NULL | 説明 |
| --- | --- | --- | --- |
| Id | guid | | PK |
| Code | string(20) | | UQ 会員番号 |
| Name | string(100) | | |
| Kana | string(100) | ○ | |
| Phone | string(20) | ○ | |
| Email | string(100) | ○ | |
| PostalCode | string(10) | ○ | |
| Address | string(200) | ○ | |
| BirthDate | date | ○ | |
| PointBalance | int | | 現在残高 (PointHistories の集計を非正規化) |
| Note | string(500) | ○ | |
| 共通列 + IsDeleted, Version | | | |

索引: `UQ(Code)`, `IX(Phone)`, `IX(Kana)`, `IX(UpdatedAt)`

#### PointHistories (ポイント履歴)

| 列 | 型 | NULL | 説明 |
| --- | --- | --- | --- |
| Id | guid | | PK |
| CustomerId | guid | | FK → Customers |
| Type | enum | | `Earn` / `Redeem` / `Revoke` / `Refund` / `Void` / `Adjust` |
| Points | int | | 符号付き |
| BalanceAfter | int | | |
| TransactionId | guid | ○ | FK → Transactions |
| Reason | string(200) | ○ | |
| StaffId | guid | ○ | FK → Staff |
| OccurredAt | datetime | | |
| CreatedAt | datetime | | |

索引: `IX(CustomerId, OccurredAt DESC)`, `IX(TransactionId)`

### 3.3 シフト (レジ開閉)

#### Shifts

| 列 | 型 | NULL | 説明 |
| --- | --- | --- | --- |
| Id | guid | | PK (端末採番) |
| StoreId | guid | | FK → Stores |
| TerminalId | guid | | FK → Terminals |
| Status | enum | | `Open` / `Closed` |
| BusinessDate | date | | |
| OpenedAt | datetime | | |
| OpenedByStaffId | guid | | FK → Staff |
| OpeningCash | money | | |
| ClosedAt | datetime | ○ | |
| ClosedByStaffId | guid | ○ | FK → Staff |
| ActualCash | money | ○ | |
| ExpectedCash | money | ○ | 精算時に確定 (`OpeningCash + CashSales − CashReturns + PaidIn − PaidOut + DepositCashIn − DepositCashOut`) |
| Difference | money | ○ | `ActualCash − ExpectedCash` |
| CashSales | money | | 精算時に確定 (Open 中は取引から都度集計) |
| CashReturns | money | | 同上 |
| PaidIn | money | | 同上 |
| PaidOut | money | | 同上 |
| DepositCashIn | money | | 同上。現金で受け取った前受金 (`OrderDeposits` から集計) |
| DepositCashOut | money | | 同上。現金で返した前受金 |
| SalesCount | int | | 同上 |
| ReturnCount | int | | 同上 |
| VoidCount | int | | 同上 |
| SalesTotal | money | | 同上 |
| ReturnsTotal | money | | 同上 |
| Note | string(500) | ○ | |
| 共通列 | | | |

索引: `UQ(TerminalId) WHERE Status = 'Open'` (端末につき開設中は 1 つ。SQLite の部分インデックス)、`IX(StoreId, BusinessDate)`

#### ShiftDenominations (金種別枚数)

| 列 | 型 | NULL | 説明 |
| --- | --- | --- | --- |
| ShiftId | guid | | PK, FK → Shifts |
| Denomination | int | | PK。額面 (端末は 10000, 5000, 2000, 1000, 500, 100, 50, 10, 5, 1) |
| Count | int | | |

#### CashEvents (入出金)

| 列 | 型 | NULL | 説明 |
| --- | --- | --- | --- |
| Id | guid | | PK (端末採番) |
| ShiftId | guid | | FK → Shifts |
| Type | enum | | `PaidIn` / `PaidOut` / `NoSale` |
| Amount | money | | |
| Reason | string(100) | ○ | |
| StaffId | guid | | FK → Staff |
| OccurredAt | datetime | | |
| CreatedAt | datetime | | |

索引: `IX(ShiftId, OccurredAt)`

### 3.4 日次締め

店舗 × 営業日の締め ([D-12](decisions.md#d-12-日次締めは日計を写して持ち締めた日の取消を止める))。  
締めた時点の日計と内訳を写して持ち、締め解除で内訳ごと消す。

#### DailyClosings

| 列 | 型 | NULL | 説明 |
| --- | --- | --- | --- |
| Id | guid | | PK (サーバ採番) |
| StoreId | guid | | FK → Stores |
| BusinessDate | date | | 営業日 |
| ClosedAt | datetime | | |
| ClosedBy | string(50) | ○ | 締めた管理画面のアカウント名 (認証を無効にしているときは NULL) |
| ShiftCount | int | | その営業日のシフトと、その営業日の取引を含むシフトの数 |
| SalesCount | int | | 以下は締めた時点の日計 (取消済みを除き、返品は負) |
| ReturnCount | int | | |
| VoidCount | int | | |
| CustomerCount | int | | 会員の人数 |
| SalesTotal | money | | |
| ReturnsTotal | money | | |
| NetSales | money | | |
| DiscountTotal | money | | |
| TaxTotal | money | | |
| PointsEarned | int | | |
| PointsRedeemed | int | | |
| HasLateTransactions | bool | | 締めた後に同じ営業日の取引が届いた (締め直すまで日計に含まれない) |
| CreatedAt / UpdatedAt | datetime | | |

索引: `UQ(StoreId, BusinessDate)`

#### DailyClosingPayments (支払方法別)

| 列 | 型 | NULL | 説明 |
| --- | --- | --- | --- |
| DailyClosingId | guid | | PK, FK → DailyClosings |
| LineNo | int | | PK (表示順) |
| PaymentMethodId | guid | | FK → PaymentMethods |
| Name | string | | 締めた時点の名称 |
| Kind | enum | | |
| SalesAmount / SalesCount | money / int | | 販売への充当額と件数 |
| ReturnAmount / ReturnCount | money / int | | 返金額と件数 |

#### DailyClosingTaxes (税率別)

| 列 | 型 | NULL | 説明 |
| --- | --- | --- | --- |
| DailyClosingId | guid | | PK, FK → DailyClosings |
| LineNo | int | | PK (表示順) |
| TaxRateId | guid | | FK → TaxRates |
| Rate | rate | | |
| TaxIncluded | bool | | |
| TaxableAmount | money | | 返品は負として合算 |
| TaxAmount | money | | 同上 |

### 3.5 取引

#### Transactions

| 列 | 型 | NULL | 説明 |
| --- | --- | --- | --- |
| Id | guid | | PK (端末採番) |
| Type | enum | | `Sale` / `Return` |
| Status | enum | | `Completed` / `Voided` |
| StoreId | guid | | FK → Stores |
| TerminalId | guid | | FK → Terminals |
| StaffId | guid | | FK → Staff |
| ShiftId | guid | | FK → Shifts |
| CustomerId | guid | ○ | FK → Customers |
| ReceiptNo | string(20) | | UQ |
| BusinessDate | date | | |
| TransactedAt | datetime | | |
| OriginalTransactionId | guid | ○ | FK → Transactions (Return の元取引) |
| Subtotal | money | | |
| DiscountTotal | money | | |
| NetSubtotal | money | | |
| TaxTotal | money | | |
| Total | money | | |
| TenderedTotal | money | | |
| ChangeAmount | money | | |
| PointsEarned | int | | Return は負 |
| PointsRedeemed | int | | Return は負 |
| PointsBalanceAfter | int | ○ | |
| Note | string(500) | ○ | |
| VoidedAt | datetime | ○ | |
| VoidedByStaffId | guid | ○ | 取り消した担当 (Staff の Id。FK は宣言しない) |
| VoidReason | string(200) | ○ | |
| 共通列 | | | |

索引: `UQ(ReceiptNo)`, `IX(StoreId, BusinessDate)`, `IX(ShiftId)`, `IX(TerminalId, TransactedAt)`, `IX(CustomerId, TransactedAt)`, `IX(OriginalTransactionId)`, `IX(TransactedAt)`

#### TransactionLines

| 列 | 型 | NULL | 説明 |
| --- | --- | --- | --- |
| Id | guid | | PK (端末採番) |
| TransactionId | guid | | FK → Transactions |
| LineNo | int | | |
| ProductId | guid | | FK → Products |
| ProductCode | string(20) | | スナップショット |
| ProductName | string(100) | | スナップショット |
| CategoryId | guid | | 販売時点の部門 (Categories の Id。FK は宣言しない) |
| Kind | enum | | `Goods` / `Service` |
| ListPrice | money | | |
| UnitPrice | money | | |
| Quantity | qty | | |
| TaxRateId | guid | | 販売時点の税率 (TaxRates の Id。FK は宣言しない) |
| TaxRate | rate | | スナップショット |
| TaxIncluded | bool | | スナップショット |
| PointRate | rate | | スナップショット |
| Amount | money | | |
| DiscountAmount | money | | |
| AllocatedDiscountAmount | money | | |
| NetAmount | money | | |
| PointsRedeemed | int | | 按分 |
| PointsEarned | int | | |
| OriginalLineId | guid | ○ | FK → TransactionLines (Return の元明細) |
| ReturnedQuantity | qty | | 元明細側で更新される唯一の列 |
| Note | string(200) | ○ | |

索引: `UQ(TransactionId, LineNo)`, `IX(ProductId)`, `IX(OriginalLineId)`

#### TransactionLineSerials

| 列 | 型 | NULL | 説明 |
| --- | --- | --- | --- |
| TransactionLineId | guid | | PK, FK → TransactionLines |
| SerialNumber | string | | PK |

索引: `IX(SerialNumber)` (シリアルからの取引検索用)

#### TransactionDiscounts

| 列 | 型 | NULL | 説明 |
| --- | --- | --- | --- |
| Id | guid | | PK |
| TransactionId | guid | | FK → Transactions |
| LineId | guid | ○ | FK → TransactionLines。NULL = 取引値引 |
| DiscountId | guid | ○ | FK → Discounts。NULL = 任意値引 |
| SortNo | int | | |
| Name | string(50) | | |
| Type | enum | | `Amount` / `Percent` |
| Value | decimal | | |
| Amount | money | | |
| Reason | string(200) | ○ | |
| ApprovedByStaffId | guid | ○ | FK → Staff。承認が必要な値引の承認者 |

索引: `IX(TransactionId)`

#### TransactionTaxSummaries

| 列 | 型 | NULL | 説明 |
| --- | --- | --- | --- |
| TransactionId | guid | | PK, FK → Transactions |
| TaxRateId | guid | | PK, FK → TaxRates |
| TaxIncluded | bool | | PK |
| Rate | rate | | |
| TaxableAmount | money | | |
| TaxAmount | money | | |

#### TransactionPayments

| 列 | 型 | NULL | 説明 |
| --- | --- | --- | --- |
| Id | guid | | PK |
| TransactionId | guid | | FK → Transactions |
| SeqNo | int | | |
| PaymentMethodId | guid | | FK → PaymentMethods |
| Kind | enum | | スナップショット |
| Amount | money | | |
| TenderedAmount | money | | |
| Reference | string(50) | ○ | |
| Note | string(200) | ○ | |

索引: `UQ(TransactionId, SeqNo)`, `IX(PaymentMethodId)`

#### TransactionDeliveries

| 列 | 型 | NULL | 説明 |
| --- | --- | --- | --- |
| TransactionId | guid | | PK, FK → Transactions |
| RecipientName | string(100) | | |
| Phone | string(20) | ○ | |
| PostalCode | string(10) | ○ | |
| Address | string(200) | | |
| RequestedDate | date | ○ | |
| TimeSlot | string(20) | ○ | |
| Note | string(200) | ○ | |

### 3.6 受注

取り寄せ・取り置きの約束 ([D-13](decisions.md#d-13-受注は会計前の約束として持ち会計で完了にする))。  
会計した取引とは `Orders.TransactionId` で紐付ける (取引には列を足さない)。

#### Orders

| 列 | 型 | NULL | 説明 |
| --- | --- | --- | --- |
| Id | guid | | PK (端末 / 管理画面が採番) |
| StoreId | guid | | FK → Stores |
| Seq | int | | 店舗ごとの連番 (登録時に採番) |
| OrderNo | string | | `{店舗コード}-O-{Seq:000000}` |
| TerminalId | guid | ○ | FK → Terminals (管理画面で登録したときは NULL) |
| StaffId | guid | | FK → Staff |
| CustomerId | guid | ○ | FK → Customers |
| CustomerName | string(100) | | 宛名 (省略すると会員の名前) |
| Phone | string(20) | ○ | 省略すると会員の電話番号 |
| Type | enum | | `BackOrder` / `Hold` |
| Status | enum | | `Ordered` / `Arrived` / `Completed` / `Cancelled`。取り置き (`Hold`) は `Arrived` から始める |
| RequestedDate | date | ○ | 希望日 |
| Note | string(500) | ○ | |
| Total | money | | 明細の金額の合計 |
| TransactionId | guid | ○ | FK → Transactions (完了のとき) |
| OrderedAt | datetime | | |
| ArrivedAt / CompletedAt / CancelledAt | datetime | ○ | |
| CancelReason | string(200) | ○ | |
| 共通列 + Version | | | |

索引: `UQ(StoreId, Seq)`、`UQ(OrderNo)`、`IX(StoreId, Status)`、`IX(CustomerId)`、`IX(TransactionId)`

#### OrderLines

| 列 | 型 | NULL | 説明 |
| --- | --- | --- | --- |
| Id | guid | | PK |
| OrderId | guid | | FK → Orders |
| LineNo | int | | |
| ProductId | guid | | FK → Products |
| ProductCode / ProductName | string | | 受注時点のスナップショット |
| Quantity | qty | | |
| UnitPrice | money | | 約束した単価 |
| Amount | money | | 単価 × 数量 (切り捨て) |
| Note | string(200) | ○ | |

索引: `IX(OrderId)`

#### OrderDeposits (前受金)

受注の前受金の受取と返金 ([D-14](decisions.md#d-14-前受金はシフトで受け取り会計で全額を充てる))。  
会計で充てた分は取引の支払 (`TransactionPayments.Kind = Deposit`) に残り、ここには書かない。  
会計で充てる額は、未完了の受注の「受取の合計 − 返金の合計」。

| 列 | 型 | NULL | 説明 |
| --- | --- | --- | --- |
| Id | guid | | PK (端末が採番。同じ Id の再送は受け取り済み) |
| OrderId | guid | | FK → Orders |
| StoreId | guid | | FK → Stores (受注の店舗) |
| TerminalId | guid | | FK → Terminals |
| ShiftId | guid | | FK → Shifts (受け取った・返したシフト。現金は予想現金に入る) |
| StaffId | guid | | FK → Staff |
| Type | enum | | `Receive` / `Refund` |
| PaymentMethodId | guid | | FK → PaymentMethods (返金は受け取った方法) |
| Kind | enum | | 支払方法の種別 (`Cash` / `Card` / `Qr` / `EMoney`) |
| Amount | money | | 正の値 (返金は前受金の全額) |
| Reference | string(50) | ○ | カードの伝票番号など |
| OccurredAt | datetime | | |
| CreatedAt | datetime | | |

索引: `IX(OrderId)`、`IX(ShiftId)`

### 3.7 在庫

#### InventoryLevels (現在庫)

| 列 | 型 | NULL | 説明 |
| --- | --- | --- | --- |
| StoreId | guid | | PK, FK → Stores |
| ProductId | guid | | PK, FK → Products |
| Quantity | qty | | 負も許容 |
| UpdatedAt | datetime | | |

索引: `IX(ProductId)` (他店在庫照会)、`IX(StoreId, UpdatedAt)` (差分同期)

#### InventoryChanges (在庫変動履歴)

| 列 | 型 | NULL | 説明 |
| --- | --- | --- | --- |
| Id | guid | | PK (端末採番 or サーバ採番) |
| StoreId | guid | | FK → Stores |
| ProductId | guid | | FK → Products |
| Type | enum | | `Sale` / `Return` / `Void` / `PhysicalCount` / `Adjustment` / `Receive` / `TransferOut` / `TransferIn` |
| QuantityDelta | qty | | |
| QuantityAfter | qty | | |
| ReasonId | guid | ○ | FK → AdjustmentReasons |
| Reason | string(200) | ○ | 自由記述 (入荷は納品書番号、移動は移動番号) |
| ReferenceType | string(20) | ○ | `Transaction` / `InventoryReceipt` / `InventoryTransfer` |
| ReferenceId | guid | ○ | 取引・入荷・移動の ID |
| ReferenceLineId | guid | ○ | その明細の ID |
| StaffId | guid | ○ | FK → Staff |
| OccurredAt | datetime | | |
| CreatedAt | datetime | | |

索引: `IX(StoreId, ProductId, OccurredAt DESC)`, `IX(ReferenceId)`

#### InventoryReceipts (入荷)

入荷予定を登録し、受領で在庫に入れる ([D-16](decisions.md#d-16-入荷と店舗間移動は伝票で持つ))。

| 列 | 型 | NULL | 説明 |
| --- | --- | --- | --- |
| Id | guid | | PK (サーバ採番) |
| StoreId | guid | | FK → Stores (入荷する店舗) |
| SupplierId | guid | | FK → Suppliers |
| SlipNo | string(50) | ○ | 仕入先の納品書番号 |
| ExpectedDate | date | ○ | 入荷予定日 |
| Status | enum | | `Draft` / `Received` / `Cancelled` |
| Note | string(500) | ○ | |
| ReceivedAt | datetime | ○ | |
| ReceivedByStaffId | guid | ○ | FK → Staff |
| CancelledAt | datetime | ○ | |
| 共通列 + Version | | | |

索引: `IX(StoreId, Status)`

#### InventoryReceiptLines

| 列 | 型 | NULL | 説明 |
| --- | --- | --- | --- |
| Id | guid | | PK |
| ReceiptId | guid | | FK → InventoryReceipts |
| LineNo | int | | |
| ProductId | guid | | FK → Products |
| ProductCode / ProductName | string | | 登録時点のスナップショット |
| Quantity | qty | | 予定の数 |
| ReceivedQuantity | qty | ○ | 受領した数 (受領まで NULL) |
| Cost | money | ○ | 仕入単価 |

索引: `IX(ReceiptId)`

#### PurchaseOrders (発注)

仕入先への注文。  
[発注] で明細を写した入荷予定を作り、入荷予定の受領とキャンセルで状態が変わる ([D-17](decisions.md#d-17-発注は入荷予定を作りその受領とキャンセルに合わせる))。

| 列 | 型 | NULL | 説明 |
| --- | --- | --- | --- |
| Id | guid | | PK (サーバ採番) |
| StoreId | guid | | FK → Stores (発注して入荷する店舗) |
| Seq | int | | 店舗ごとの連番 (登録時に採番) |
| PurchaseOrderNo | string | | `{店舗コード}-P-{Seq:000000}` |
| SupplierId | guid | | FK → Suppliers |
| Status | enum | | `Draft` / `Ordered` / `Received` / `Cancelled` |
| ExpectedDate | date | ○ | 希望納期 (入荷予定日になる) |
| Note | string(500) | ○ | |
| OrderedAt | datetime | ○ | 発注の日時 |
| OrderedBy | string(50) | ○ | 発注した管理画面のアカウント名 (認証を無効にしているときは NULL) |
| ReceiptId | guid | ○ | FK → InventoryReceipts (発注で作った入荷予定) |
| CancelledAt | datetime | ○ | |
| 共通列 + Version | | | |

索引: `UQ(StoreId, Seq)`、`UQ(PurchaseOrderNo)`、`IX(StoreId, Status)`、`IX(ReceiptId)`

#### PurchaseOrderLines

| 列 | 型 | NULL | 説明 |
| --- | --- | --- | --- |
| Id | guid | | PK |
| PurchaseOrderId | guid | | FK → PurchaseOrders |
| LineNo | int | | 入荷予定の明細と同じ番号 |
| ProductId | guid | | FK → Products |
| ProductCode / ProductName | string | | 登録・変更時点のスナップショット |
| Quantity | qty | | 発注の数 |
| Cost | money | ○ | 仕入単価 |

索引: `IX(PurchaseOrderId)`

#### InventoryTransfers (店舗間移動)

| 列 | 型 | NULL | 説明 |
| --- | --- | --- | --- |
| Id | guid | | PK (サーバ採番) |
| FromStoreId | guid | | FK → Stores (出荷店) |
| Seq | int | | 出荷店ごとの連番 (登録時に採番) |
| TransferNo | string | | `{出荷店コード}-T-{Seq:000000}` |
| ToStoreId | guid | | FK → Stores (入荷店) |
| Status | enum | | `Requested` / `Shipped` / `Received` / `Cancelled` |
| Note | string(500) | ○ | |
| ShippedAt / ReceivedAt / CancelledAt | datetime | ○ | |
| ShippedByStaffId / ReceivedByStaffId | guid | ○ | FK → Staff |
| 共通列 + Version | | | |

索引: `UQ(FromStoreId, Seq)`、`UQ(TransferNo)`、`IX(ToStoreId, Status)`

#### InventoryTransferLines

| 列 | 型 | NULL | 説明 |
| --- | --- | --- | --- |
| Id | guid | | PK |
| TransferId | guid | | FK → InventoryTransfers |
| LineNo | int | | |
| ProductId | guid | | FK → Products |
| ProductCode / ProductName | string | | 依頼時点のスナップショット |
| Quantity | qty | | 依頼・出荷の数 |
| ReceivedQuantity | qty | ○ | 受領した数 (受領まで NULL) |

索引: `IX(TransferId)`

### 3.8 認証

管理画面のアカウントと端末の登録 ([D-35](decisions.md#d-35-認証は管理画面のログイン端末のトークンスタッフの-pin-にする))。  
端末には同期しない。

#### Accounts (管理画面のアカウント)

| 列 | 型 | NULL | 説明 |
| --- | --- | --- | --- |
| Id | guid | | PK |
| Name | string(50) | | UQ。ログイン ID |
| Password | blob | | PBKDF2 (SHA-256、310,000 回) のソルト 32 + ハッシュ 32 バイト (`IPasswordProvider`) |
| Role | enum | | `Administrator` / `Operator` |
| IsActive | bool | | 無効はログインできない |
| LastLoginAt | datetime | ○ | 最終ログイン (版を変えずに更新する) |
| CreatedAt, UpdatedAt | | | |
| Version | int | | 役割・有効・パスワードを変えると +1。ログイン中のセッションは版が変わると無効になる |

索引: `UQ(Name)`。  
起動時に 1 件もなければ設定 (`Auth:InitialName` / `InitialPassword`) の管理者を作る。  
削除は行ごと消す (他の表から参照しない)

#### TerminalTokens (端末の登録)

ペアリングコードを発行した行が、ペアリングでトークンを持つ行になる (コードは消費する)。

| 列 | 型 | NULL | 説明 |
| --- | --- | --- | --- |
| Id | guid | | PK |
| TerminalId | guid | | FK → Terminals |
| PairingCode | string(6) | ○ | 未使用のペアリングコード (ペアリングで NULL) |
| PairingExpiresAt | datetime | ○ | コードの有効期限 (発行から 10 分) |
| TokenHash | blob | ○ | トークンの SHA-256 (トークン自体は持たない) |
| DeviceName | string(100) | ○ | 端末が送った機種名 |
| PairedAt | datetime | ○ | |
| RevokedAt | datetime | ○ | 登録の解除・再ペアリングで失効した日時 |
| CreatedAt | datetime | | |

索引: `IX(TerminalId)`、`UQ(TokenHash) WHERE TokenHash IS NOT NULL`、`IX(PairingCode) WHERE PairingCode IS NOT NULL`。  
コードを発行すると端末の未使用のコードは消し、ペアリングで端末の有効なトークンを失効させる (端末ごとに有効なトークンは 1 つ)

---

## 4. DDL 例

`Host/Assets/Data/Schema.sql` に置く SQLite の DDL (端末は `Resources/Raw/Schema.sql`)。  
他のテーブルも同じ規則 (guid = TEXT、money / rate / qty = NUMERIC、enum = TEXT、datetime = TEXT) で書く。

```sql
-- Schema.sql (抜粋)
CREATE TABLE IF NOT EXISTS Transactions (
    Id                     TEXT     NOT NULL,
    Type                   TEXT     NOT NULL,   -- Sale / Return
    Status                 TEXT     NOT NULL,   -- Completed / Voided
    StoreId                TEXT     NOT NULL,
    TerminalId             TEXT     NOT NULL,
    StaffId                TEXT     NOT NULL,
    ShiftId                TEXT     NOT NULL,
    CustomerId             TEXT,
    ReceiptNo              TEXT     NOT NULL,
    BusinessDate           TEXT     NOT NULL,   -- yyyy-MM-dd
    TransactedAt           TEXT     NOT NULL,   -- UTC
    OriginalTransactionId  TEXT,
    Subtotal               NUMERIC  NOT NULL,
    DiscountTotal          NUMERIC  NOT NULL,
    NetSubtotal            NUMERIC  NOT NULL,
    TaxTotal               NUMERIC  NOT NULL,
    Total                  NUMERIC  NOT NULL,
    TenderedTotal          NUMERIC  NOT NULL,
    ChangeAmount           NUMERIC  NOT NULL,
    PointsEarned           INTEGER  NOT NULL,
    PointsRedeemed         INTEGER  NOT NULL,
    PointsBalanceAfter     INTEGER,
    Note                   TEXT,
    VoidedAt               TEXT,
    VoidedByStaffId        TEXT,
    VoidReason             TEXT,
    CreatedAt              TEXT     NOT NULL,
    UpdatedAt              TEXT     NOT NULL,
    PRIMARY KEY (Id),
    UNIQUE (ReceiptNo),
    FOREIGN KEY (StoreId) REFERENCES Stores (Id),
    FOREIGN KEY (TerminalId) REFERENCES Terminals (Id),
    FOREIGN KEY (StaffId) REFERENCES Staff (Id),
    FOREIGN KEY (ShiftId) REFERENCES Shifts (Id),
    FOREIGN KEY (CustomerId) REFERENCES Customers (Id),
    FOREIGN KEY (OriginalTransactionId) REFERENCES Transactions (Id)
);
CREATE INDEX IF NOT EXISTS IX_Transactions_StoreId_BusinessDate ON Transactions (StoreId, BusinessDate);
CREATE INDEX IF NOT EXISTS IX_Transactions_ShiftId ON Transactions (ShiftId);
CREATE INDEX IF NOT EXISTS IX_Transactions_TerminalId_TransactedAt ON Transactions (TerminalId, TransactedAt);
CREATE INDEX IF NOT EXISTS IX_Transactions_CustomerId_TransactedAt ON Transactions (CustomerId, TransactedAt);
CREATE INDEX IF NOT EXISTS IX_Transactions_OriginalTransactionId ON Transactions (OriginalTransactionId);
CREATE INDEX IF NOT EXISTS IX_Transactions_TransactedAt ON Transactions (TransactedAt);

-- 部分ユニークインデックスの例
CREATE UNIQUE INDEX IF NOT EXISTS UX_Shifts_Open ON Shifts (TerminalId) WHERE Status = 'Open';
```

エンティティと Accessor の例 (テーブル名はクラスの `[Name]` で指定する):

```csharp
[Name("Transactions")]
public sealed class TransactionEntity
{
    [Key]
    public Guid Id { get; set; }

    public TransactionType Type { get; set; }      // 変換は DataProfile の EnumTextConverter<TransactionType>
    public TransactionStatus Status { get; set; }
    public Guid StoreId { get; set; }
    // ...
    public decimal Total { get; set; }
    public DateTime TransactedAt { get; set; }      // DateTimeTextConverter (UTC)
}

[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class TransactionAccessor
{
    [Execute]
    [Insert(typeof(TransactionEntity))]
    public partial ValueTask<int> InsertAsync(DbTransaction tx, TransactionEntity entity, CancellationToken cancellationToken);

    [QueryFirst]
    [SelectSingle(typeof(TransactionEntity))]
    public partial ValueTask<TransactionEntity?> QueryAsync(Guid id, CancellationToken cancellationToken);
}
```

初期データは Host の `Assets/Data/InitialData.sql` (複数の `INSERT`。`@now` は投入時刻) を起動時に読み、`GenericAccessor.ExecuteScriptAsync` (`[DirectSql]`) で会社設定がない DB へ 1 トランザクションで投入する (内容は [architecture.md §6](architecture.md#6-初期データ))。

起動時の PRAGMA (`GenericAccessor.ExecutePragmaAsync.sql`。WAL は DB ファイルに永続化される):

```sql
PRAGMA journal_mode = WAL;
PRAGMA busy_timeout = 5000;
PRAGMA foreign_keys = ON
```

---

## 5. 整合性と更新の単位

SQLite は書き込みが直列化される (単一ライター) が、検証はトランザクションの前に読むので、その後に変わりうる条件 (状態・版・前受金の残り・返品数量) は書き込みの文の条件に入れ、条件に合わなければ書き込みを戻す。  
トランザクションは Smart.Data の `IDbProvider.UsingTxAsync` で扱い、Service が Accessor の `DbTransaction` 付きメソッドを束ねる (Usecase 層は置かない。[D-27](decisions.md#d-27-サーバは-service-に手順を集めsql-は-accessor-に置く))。

### 5.1 取引登録 (`POST /transactions`) は 1 つの DB トランザクション

1. `Transactions.Id` が既にあれば既存を返して終える (種別・端末・シフト・レシート番号・合計・取引日時のどれかが違えば 409)
2. 検証 (シフト状態、商品、計算一致、返品数量 …)
3. 以下を 1 トランザクションで実行
   - `Transactions` + `TransactionLines` + `TransactionLineSerials` + `TransactionDiscounts` + `TransactionTaxSummaries` + `TransactionPayments` + `TransactionDeliveries` を INSERT
   - 取消済み (`Status = Voided`) で届いた取引は、INSERT と締め済みの印だけを行う
   - `Return` なら元明細の `ReturnedQuantity` を加算 (超過チェックは同一トランザクション内で再確認)
   - `TrackInventory` の明細ごとに `InventoryLevels` を **UPSERT で加減算** (`INSERT ... ON CONFLICT (StoreId, ProductId) DO UPDATE SET Quantity = Quantity + excluded.Quantity`) し、更新後の値 (`RETURNING Quantity`) で `InventoryChanges` を INSERT
   - 顧客があれば `Customers.PointBalance` を `UPDATE ... SET PointBalance = PointBalance + @delta` で加減算し、`PointHistories` を INSERT (`Redeem` → `Earn` の順。Return は `Refund` → `Revoke`。処理後の残高を `Transactions.PointsBalanceAfter` に書く)
   - `Terminals.LastReceiptSeq` を `max(現在値, 今回の連番)` で更新
   - 店舗 × 営業日が締め済みなら `DailyClosings.HasLateTransactions` を立てる
   - 受注から会計した販売なら、引き渡し待ち (`Arrived`) の受注を `Completed` にして `TransactionId` を入れる (状態か前受金の残りが検証のときと変わっていれば、登録全体を戻して `ORDER_NOT_READY`)
4. コミット

### 5.2 取消 (`POST /transactions/{id}/void`)

`UPDATE Transactions ... WHERE Status = 'Completed'` で `Status = Voided` と取消の列を書き (0 件なら取消済み)、在庫は逆方向の `InventoryChanges (Type = Void)`、ポイントは `PointHistories (Type = Void)` を足す。  
元の履歴行は変更しない。  
販売の取消は同じトランザクションで受注を `Arrived` に戻して `TransactionId` を外し、返品の取消は元明細の `ReturnedQuantity` を戻す。  
店舗 × 営業日が締め済みのとき、シフトが精算済みのとき、返品のある販売の取消は拒否する。

### 5.3 精算 (`POST /shifts/{id}/close`)

シフト内の `Completed` 取引と `CashEvents`、現金の `OrderDeposits` から集計列を求め、`Status = 'Open'` を条件にした UPDATE で `Shifts` に確定して `Status = Closed` にし、金種別枚数 (`ShiftDenominations`) と同じトランザクションで書く。  
先に精算が通っていたら金種を足さずに終え、実査金額が同じ再送は精算済みの内容を返す。  
以降、そのシフトへの取引・入出金・前受金・取消は拒否する。

### 5.4 日次締め (`POST /daily-closings`)

関係するシフト (その営業日のシフトと、その営業日の取引を含むシフト) がすべて `Closed` であることを確かめ、日計と支払方法別・税率別を集計して `DailyClosings` + `DailyClosingPayments` + `DailyClosingTaxes` を 1 トランザクションで INSERT する。  
同時に締めたときは `UQ(StoreId, BusinessDate)` の重複で片方を締め済みとして返す。  
締め解除 (`DELETE /daily-closings/{id}`) は内訳と行を 1 トランザクションで DELETE する。

### 5.5 受注の登録 (`POST /orders`)

受注番号は 1 文の `INSERT INTO Orders ... SELECT COALESCE(MAX(Seq), 0) + 1 ... RETURNING *` で採番して登録し、明細と同じトランザクションで書く。  
同時に登録しても連番は重ならない (`UQ(StoreId, Seq)` でも守る)。  
入荷・キャンセル・変更は状態を条件にした `UPDATE ... RETURNING *` で、行が返らなければ状態 (または版) が合わない。  
キャンセルは前受金の残りが 0 のときだけにする (条件に `OrderDeposits` の合計を入れる)。  
前受金の受取・返金は 1 文の `INSERT INTO OrderDeposits ... SELECT ... FROM Orders WHERE ...` で、受注が未完了で、前受金の残りが検証のときと同じで、シフトが開設中のときだけ登録する。  
同時の受取・返金・キャンセル・会計・精算と重なったら登録せず、改めて検証する。

### 5.6 商品画像と CSV 取込

画像の登録・削除は `ProductImages` の UPSERT / DELETE と `Products.ImageUrl` の更新 (`UpdatedAt` / `Version` を進める) を 1 トランザクションで行う。  
CSV 取込は全行を検証してから、登録と更新を 1 トランザクションで書く。  
更新は版を条件にした `UPDATE ... RETURNING *` で、行が返らないか一意制約・外部キーに反したら全体を取り消す (検証の後に他で変わった)。

### 5.7 入荷・店舗間移動・発注・棚卸

入荷の受領は、状態を条件にした `UPDATE InventoryReceipts ... WHERE Status = 'Draft' RETURNING *` と、明細ごとの受領数の更新・`InventoryLevels` の UPSERT・`InventoryChanges (Type = Receive)` の INSERT を 1 トランザクションで行う。  
行が返らなければ状態が合わないか伝票がない。  
移動と発注の番号は受注と同じく 1 文の `INSERT ... SELECT COALESCE(MAX(Seq), 0) + 1 ... RETURNING *` で採番する。  
出荷 (`TransferOut`、出荷店を依頼の数だけ減らす) と受領 (`TransferIn`、入荷店を受領した数だけ増やす) もそれぞれ状態を条件にした UPDATE と在庫の加減算を 1 トランザクションで行う。  
発注の [発注] は、状態を条件にした `UPDATE PurchaseOrders ... WHERE Status = 'Draft'` と入荷予定・明細の INSERT を 1 トランザクションで行う。  
入荷予定の受領とキャンセルは、同じトランザクションで `ReceiptId` の発注を入荷済み・キャンセルにする。  
発注済みの発注のキャンセルは、同じトランザクションで入荷予定もキャンセルする (入荷予定が先に受領されていたら、発注のキャンセルも行わない)。  
棚卸・調整は 1 件ずつ、`InventoryLevels` の UPSERT と `InventoryChanges` の INSERT を 1 トランザクションで行う (棚卸は現在庫との差を `QuantityDelta` にし、同じ Id の再送は登録済みを返す)。

### 5.8 集計の考え方

- 取引の集計は常に `Status = 'Completed'` を対象にし、`Type = 'Return'` を負として扱う。  
  金額列は NUMERIC 親和性で数値として保存されるので `SUM` をそのまま使える
- `Customers.PointBalance` と `InventoryLevels.Quantity` は非正規化した値で、`PointHistories` と `InventoryChanges` を書くのと同じトランザクションで加減算する (`Shifts` の集計列は精算の時点の集計を写す)。  
  初期データの会員の残高は `Adjust` の履歴で持つが、初期の在庫は変動の履歴を持たない。  
  管理画面のダッシュボードは、在庫とポイント残高が負のものを要確認に出す

---

## 6. 端末ローカル DB (SQLite) の概要

MAUI 側のローカル DB。  
`Microsoft.Data.Sqlite` + Smart.Data.Accessor (`DataAccessor` + `Services/Sql/*.sql`) で扱い、日時は INTEGER (UTC ticks) + `DateTimeTicksConverter` で保存する ([D-30](decisions.md#d-30-金額は-decimalid-は-guid-v7日時は-utc-にする))。  
スキーマはアプリに同梱した `Resources/Raw/Schema.sql` を起動時に実行する (列の追加は [§1](#1-前提))。

| テーブル | 内容 |
| --- | --- |
| マスタ各種 | `Settings` / `Stores` / `Terminals` / `Staff` / `Categories` / `TaxRates` / `Products` / `Discounts` / `PaymentMethods` / `AdjustmentReasons` を `Pos.Contract` の Response と同じ列で保持 (エンティティクラスは Response をそのまま使う)。`GET /sync/masters` の結果を Id で削除 → 挿入 (1 トランザクション)。削除済み (`IsDeleted`) も保持し、検索時に除く。商品画像は DB に持たず、表示するときに取得して `CacheDirectory/products/{商品 ID}_{v}` に置く (オフラインはキャッシュだけ) |
| `InventoryLevels` | 自店分のみ (`updatedSince` で差分取り込み。販売・返品・取消・棚卸・調整ではローカルでも増減させる) |
| `Shifts` / `CashEvents` | 端末で開設したシフトと入出金 (精算の予想現金の計算に使う)。サーバに開設中のシフトが残っていれば (入れ直したときなど)、写して引き継ぐ |
| `OrderDeposits` | サーバが受け付けた前受金の受取・返金のうち、今のシフトの分の写し (精算の予想現金の計算に使う)。受取・返金の応答と受注を読んだときに、まだ写していない記録を足す (写し済みは変えない)。前受金はオンライン限定なので Outbox には入れない |
| `Transactions` | 検索用の列 (種別・状態・シフト・レシート番号・営業日・日時・会員・合計・ポイント・元取引) + `Payload` (`TransactionResponseItem` の JSON。送信後はサーバの応答で置き換える)。取引履歴・再印字・返品の元取引参照に使う |
| `Outbox` | `Id` (guid)、`Kind` (ShiftOpen / Transaction / TransactionVoid / CashEvent / ShiftClose / InventoryChanges)、`TargetId` (取引 ID やシフト ID)、`Payload` (JSON、`XxxRequest` をそのまま直列化)、`CreatedAt`、`Status` (Pending / Sent / Failed)、`Attempts`、`LastError`、`SentAt`。Sent は 7 日で削除 |
| `SyncState` | `Key` / `Value` (最終 `ServerTime`、在庫の同期時刻、レシート番号の連番)。端末設定 (サーバ URL・店舗 ID・端末 ID・登録日時) は `IPreferences` (`Settings`)、端末のトークンは `SecureStorage` (`CredentialService`) に置く |
| `HoldCarts` | 会計途中の保留 (端末ローカルのみ、T-17)。`Summary` / `Total` と `Payload` (`SalesCart` の JSON) |
