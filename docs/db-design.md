# POS サーバ DB 設計

[api-design.md](api-design.md) に対応するサーバ側データベース (SQLite) の設計。  
設計判断は [decisions.md](decisions.md)、プロジェクト構成は [architecture.md](architecture.md)。

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
| RDBMS | **SQLite** (`Microsoft.Data.Sqlite`)。接続文字列は `Data Source=pos.db;Cache=Shared;Pooling=True`。WAL と `busy_timeout` を起動時の PRAGMA で設定する |
| データアクセス | `Usa.Smart.Data.Accessor` の `[DataAccessor]` + 2-way SQL ファイル (`Accessors/Sql/{Accessor}.{Method}.sql`)。ORM は使わない。SQL は Accessor だけが持ち、Accessor を使うのは `Services/` の Service だけ ([D-45](decisions.md#d-45-サーバの-service-層)) |
| スキーマ作成 | 起動時に `{Accessor}.Create.sql` (`CREATE TABLE IF NOT EXISTS` + `CREATE INDEX IF NOT EXISTS`) を実行する。後から増えた列は `SqlHelper.EnsureColumnAsync` (`PRAGMA table_info` で確認して `ALTER TABLE ADD COLUMN`) で既存の DB に足す |
| 命名 | テーブル = 複数形 PascalCase (`Transactions`)、列 = PascalCase。FK は `〜Id`。エンティティクラスは `{Table 単数}Entity` (`TransactionEntity`) |
| 主キー | `guid` を **TEXT (36 文字。`Microsoft.Data.Sqlite` の既定で大文字)** で保存。端末発のデータは端末が GUID v7 を採番 ([D-10](decisions.md#d-10-冪等性-クライアント採番-id)) |
| 列挙型 | TEXT (列挙名)。汎用 `EnumTextConverter<T>` を `DataProfile` (`[AccessorProfile]`) に列挙型ごとに宣言し、各 Accessor が `[ExecuteConfig(typeof(DataProfile))]` で参照する ([D-25](decisions.md#d-25-日時と列挙型の-sqlite-保存形式))。値は API の enum と同じ |
| 論理削除 | マスタ系は `IsDeleted`。差分同期で削除も伝える必要があるので、通常の照会側で `IsDeleted = 0` を明示する |
| 監査列 | `CreatedAt` / `UpdatedAt` (UTC)。マスタ系は楽観ロック用 `Version` (INTEGER、更新ごとに +1) |
| 履歴 | 取引・シフト・入出金・在庫変動・ポイント履歴は**更新・削除しない** (取消も `Status` 更新 + 逆方向の履歴追加) |
| スナップショット | 取引明細は商品名・単価・税率・還元率を販売時点の値で保持する。マスタ変更が過去の取引に影響しない |
| 外部キー | `FOREIGN KEY` は宣言するが、SQLite の既定では強制されないため接続文字列の `Foreign Keys=True` で接続ごとに有効化する (WAL と busy_timeout は起動時の `DatabaseAccessor.ExecutePragmaAsync`) |

型の表記 (C# ↔ SQLite):

| 表記 | C# | SQLite | 備考 |
| --- | --- | --- | --- |
| `guid` | `Guid` | `TEXT` | `Microsoft.Data.Sqlite` の既定 (36 文字、大文字)。生成コードは `GetGuid` で読む |
| `string(n)` | `string` | `TEXT` | 長さはアプリ側で検証 (SQLite は長さ制約を強制しない) |
| `money` | `decimal` | `NUMERIC` | 円。`Microsoft.Data.Sqlite` は `decimal` を TEXT で書くが、NUMERIC 親和性により数値 (INTEGER / REAL) に変換されて保存される ([D-13](decisions.md#d-13-金額数量率の表現-decimal)) |
| `rate` | `decimal` | `NUMERIC` | `0.1` (REAL として保存)。集計しない |
| `qty` | `decimal` | `NUMERIC` | 数量 (小数可) |
| `int` | `int` | `INTEGER` | |
| `bool` | `bool` | `INTEGER` | 0 / 1 |
| `datetime` | `DateTime` (UTC) | `TEXT` | `yyyy-MM-dd HH:mm:ss.fffffff` (`Microsoft.Data.Sqlite` の既定書式。文字列比較で範囲検索できる)。`DateTimeTextConverter` で UTC に固定して読み書きする |
| `date` | `DateOnly` | `TEXT` | `yyyy-MM-dd` (`DateOnlyTextConverter`) |
| `enum` | enum | `TEXT` | 列挙名 |

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
    Stores ||--o{ Staff : "belongs (nullable)"
    Categories ||--o{ Categories : parent
    Categories ||--o{ Products : has
    TaxRates ||--o{ Products : applies
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
    Shifts ||--o{ Transactions : contains
    Shifts ||--o{ CashEvents : has
    Shifts ||--o{ ShiftDenominations : has
    Transactions ||--|{ TransactionLines : has
    Transactions ||--o{ TransactionDiscounts : has
    Transactions ||--|{ TransactionTaxSummaries : has
    Transactions ||--|{ TransactionPayments : has
    Transactions ||--o| TransactionDeliveries : has
    Transactions ||--o{ Transactions : "original (Return)"
    TransactionLines ||--o{ TransactionLineSerials : has
    TransactionLines ||--o{ TransactionLines : "original line (Return)"
    TransactionLines }o--o| TransactionDiscounts : "line discount"
    Customers ||--o{ Transactions : buys
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
| LastReceiptSeq | int | | 最終レシート連番 (取引登録時に更新) |
| LastSeenAt | datetime | ○ | |
| AppVersion | string(20) | ○ | |
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
| PinHash | BLOB | ○ | 未使用 (スタッフ PIN のハッシュ用) |
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
| ImageUrl | string(500) | ○ | |
| IsActive | bool | | |
| 共通列 + IsDeleted, Version | | | |

索引: `UQ(Code)`, `UQ(Barcode) WHERE Barcode IS NOT NULL`, `IX(CategoryId)`, `IX(Name)`, `IX(Kana)`, `IX(UpdatedAt)`

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

#### PaymentMethods (支払方法)

| 列 | 型 | NULL | 説明 |
| --- | --- | --- | --- |
| Id | guid | | PK |
| Code | string(20) | | UQ |
| Name | string(50) | | |
| ShortName | string(10) | ○ | 端末の支払ボタンに出す短い名前 (省略時は Name) |
| Kind | enum | | `Cash` / `Card` / `Qr` / `EMoney` / `Voucher` / `Points` / `Credit` / `Other` |
| AllowsChange | bool | | |
| RequiresReference | bool | | |
| IsActive | bool | | |
| SortOrder | int | | |
| 共通列 + IsDeleted, Version | | | |

アプリ側制約: `Kind = Points` かつ有効な行はちょうど 1 件。

#### AdjustmentReasons (在庫調整理由)

| 列 | 型 | NULL | 説明 |
| --- | --- | --- | --- |
| Id | guid | | PK |
| Code | string(20) | | UQ |
| Name | string(50) | | |
| SortOrder | int | | |
| IsActive | bool | | |
| 共通列 + IsDeleted, Version | | | |

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
| ExpectedCash | money | ○ | 精算時に確定 |
| Difference | money | ○ | `ActualCash − ExpectedCash` |
| CashSales | money | | 精算時に確定 (Open 中は取引から都度集計) |
| CashReturns | money | | 同上 |
| PaidIn | money | | 同上 |
| PaidOut | money | | 同上 |
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
| Denomination | int | | PK (10000, 5000, 1000, 500, 100, 50, 10, 5, 1) |
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

### 3.4 取引

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
| VoidedByStaffId | guid | ○ | FK → Staff |
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
| CategoryId | guid | | FK → Categories (販売時点) |
| Kind | enum | | `Goods` / `Service` |
| ListPrice | money | | |
| UnitPrice | money | | |
| Quantity | qty | | |
| TaxRateId | guid | | FK → TaxRates |
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
| SerialNumber | string(50) | | PK |

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

### 3.5 在庫

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
| Type | enum | | `Sale` / `Return` / `Void` / `PhysicalCount` / `Adjustment` |
| QuantityDelta | qty | | |
| QuantityAfter | qty | | |
| ReasonId | guid | ○ | FK → AdjustmentReasons |
| Reason | string(200) | ○ | |
| ReferenceType | string(20) | ○ | `Transaction` |
| ReferenceId | guid | ○ | 取引 ID |
| ReferenceLineId | guid | ○ | 取引明細 ID |
| StaffId | guid | ○ | FK → Staff |
| OccurredAt | datetime | | |
| CreatedAt | datetime | | |

索引: `IX(StoreId, ProductId, OccurredAt DESC)`, `IX(ReferenceId)`

---

## 4. DDL 例

`Accessors/Sql/{Accessor}.Create.sql` に置く SQLite の DDL。  
他のテーブルも同じ規則 (guid = TEXT、money = INTEGER、enum = TEXT、datetime = TEXT) で書く。

```sql
-- TransactionAccessor.Create.sql
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
    Subtotal               NUMERIC  NOT NULL,   -- decimal
    DiscountTotal          NUMERIC  NOT NULL,   -- decimal
    NetSubtotal            NUMERIC  NOT NULL,   -- decimal
    TaxTotal               NUMERIC  NOT NULL,   -- decimal
    Total                  NUMERIC  NOT NULL,   -- decimal
    TenderedTotal          NUMERIC  NOT NULL,   -- decimal
    ChangeAmount           NUMERIC  NOT NULL,   -- decimal
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

CREATE TABLE IF NOT EXISTS TransactionLines (
    Id                       TEXT     NOT NULL,
    TransactionId            TEXT     NOT NULL,
    LineNo                   INTEGER  NOT NULL,
    ProductId                TEXT     NOT NULL,
    ProductCode              TEXT     NOT NULL,
    ProductName              TEXT     NOT NULL,
    CategoryId               TEXT     NOT NULL,
    Kind                     TEXT     NOT NULL,   -- Goods / Service
    ListPrice                NUMERIC  NOT NULL,   -- decimal
    UnitPrice                NUMERIC  NOT NULL,   -- decimal
    Quantity                 NUMERIC  NOT NULL,   -- decimal
    TaxRateId                TEXT     NOT NULL,
    TaxRate                  NUMERIC  NOT NULL,   -- decimal
    TaxIncluded              INTEGER  NOT NULL,
    PointRate                NUMERIC  NOT NULL,   -- decimal
    Amount                   NUMERIC  NOT NULL,   -- decimal
    DiscountAmount           NUMERIC  NOT NULL,   -- decimal
    AllocatedDiscountAmount  NUMERIC  NOT NULL,   -- decimal
    NetAmount                NUMERIC  NOT NULL,   -- decimal
    PointsRedeemed           INTEGER  NOT NULL,
    PointsEarned             INTEGER  NOT NULL,
    OriginalLineId           TEXT,
    ReturnedQuantity         NUMERIC  NOT NULL DEFAULT 0,
    Note                     TEXT,
    PRIMARY KEY (Id),
    UNIQUE (TransactionId, LineNo),
    FOREIGN KEY (TransactionId) REFERENCES Transactions (Id),
    FOREIGN KEY (ProductId) REFERENCES Products (Id),
    FOREIGN KEY (OriginalLineId) REFERENCES TransactionLines (Id)
);
CREATE INDEX IF NOT EXISTS IX_TransactionLines_ProductId ON TransactionLines (ProductId);
CREATE INDEX IF NOT EXISTS IX_TransactionLines_OriginalLineId ON TransactionLines (OriginalLineId);

-- 部分ユニークインデックスの例 (ShiftAccessor.Create.sql)
CREATE UNIQUE INDEX IF NOT EXISTS UX_Shifts_Open ON Shifts (TerminalId) WHERE Status = 'Open';
```

エンティティと Accessor の例 (Smart.Data.Accessor 3.0.0-beta7。テーブル名はクラスの `[Name]` ではなく Builder 属性の `Table` で指定する):

```csharp
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
    [Insert(typeof(TransactionEntity), Table = "Transactions")]
    public partial ValueTask<int> InsertAsync(DbTransaction tx, TransactionEntity entity, CancellationToken cancellationToken);

    [QueryFirst]
    [SelectSingle(typeof(TransactionEntity), Table = "Transactions")]
    public partial ValueTask<TransactionEntity?> QueryAsync(Guid id, CancellationToken cancellationToken);
}
```

- 2-way SQL の POCO 引数 (`/*@ entity.Prop */`) にはコンバータが効かないので、列挙型・日付を渡す UPDATE はスカラー引数で書く (`UpdateAsync(id, code, ..., kind, updatedAt, version)`)。  
  INSERT は Builder (`[Insert]`) を使う
- 生 SQL (`/*# sort */Id`) のプレースホルダは 1 トークン。  
  並び替えは呼び出し側が `SqlHelper.NormalizeSort` で検証した `"Column DESC"` を渡す
- 集計は `Models/` の record (`ShiftTotals` / `SalesSummaryRow` など) に列名で写す

起動時の PRAGMA (`DatabaseAccessor.ExecutePragmaAsync.sql`。WAL は DB ファイルに永続化される):

```sql
PRAGMA journal_mode = WAL;
PRAGMA busy_timeout = 5000;
PRAGMA foreign_keys = ON
```

---

## 5. 整合性と更新の単位

SQLite は書き込みが直列化される (単一ライター) ため、サーバ内の同時更新は DB トランザクションで十分に守れる。  
トランザクションは Smart.Data の `IDbProvider.UsingTxAsync` で扱い、Service が Accessor の `DbTransaction` 付きメソッドを束ねる (Service / Usecase は置かない、[D-19](decisions.md#d-19-技術スタックプロジェクト構成-テンプレート準拠))。

### 5.1 取引登録 (`POST /transactions`) は 1 つの DB トランザクション

1. `Transactions.Id` が既に存在 → 既存を返して終了 (本文比較で相違なら 409)
2. 検証 (シフト状態、商品、計算一致、返品数量 …)
3. 以下を 1 トランザクションで実行
   - `Transactions` + `TransactionLines` + `TransactionLineSerials` + `TransactionDiscounts` + `TransactionTaxSummaries` + `TransactionPayments` + `TransactionDeliveries` を INSERT
   - `Return` なら元明細の `ReturnedQuantity` を加算 (超過チェックは同一トランザクション内で再確認)
   - `TrackInventory` の明細ごとに `InventoryLevels` を **UPSERT で加減算** (`INSERT ... ON CONFLICT (StoreId, ProductId) DO UPDATE SET Quantity = Quantity + excluded.Quantity`) し、更新後の値 (`RETURNING Quantity`) で `InventoryChanges` を INSERT
   - 顧客があれば `Customers.PointBalance` を `UPDATE ... SET PointBalance = PointBalance + @delta` で加減算し、`PointHistories` を INSERT (`Redeem` → `Earn` の順。Return は `Refund` → `Revoke`)
   - `Terminals.LastReceiptSeq` を `max(現在値, 今回の連番)` で更新
4. コミット

### 5.2 取消 (`POST /transactions/{id}/void`)

`Transactions.Status = Voided` + Void 列を更新し、在庫は逆方向の `InventoryChanges (Type = Void)`、ポイントは `PointHistories (Type = Void)` を追加。  
元の履歴行は変更しない。

### 5.3 精算 (`POST /shifts/{id}/close`)

シフト内の `Completed` 取引と `CashEvents` から集計列を確定して `Shifts` を更新し、`Status = Closed`。  
以降、そのシフトへの取引・入出金・取消は拒否。

### 5.4 集計の考え方

- 取引の集計は常に `Status = 'Completed'` を対象にし、`Type = 'Return'` を負として扱う。  
  金額列は NUMERIC 親和性で数値として保存されるので `SUM` をそのまま使える
- `Shifts` の集計列と `Customers.PointBalance`、`InventoryLevels.Quantity` は非正規化した値。  
  履歴から再計算できることを整合性チェック (管理画面のメンテナンス機能) の前提にする

---

## 6. 端末ローカル DB (SQLite) の概要

MAUI 側のローカル DB。  
`Microsoft.Data.Sqlite` + Smart.Data.Accessor (`DataAccessor` + `Services/Sql/*.sql`) で扱い、日時は INTEGER (UTC ticks) + `DateTimeTicksConverter` で保存する ([D-25](decisions.md#d-25-日時と列挙型の-sqlite-保存形式))。  
ローカルのエンティティ (`[Key]` あり) のキーによる取得・削除は `[SelectSingle]` / `[Delete]` で生成し、SQL ファイルは `SELECT` / `FROM` / `WHERE` / `ORDER BY` を行頭に置いて列と条件を字下げする。  
書き込みは `Services/` の Usecase が行い、1 文だけの書き込みにはトランザクションを使わない。

| テーブル | 内容 |
| --- | --- |
| マスタ各種 | `Settings` / `Stores` / `Terminals` / `Staff` / `Categories` / `TaxRates` / `Products` / `Discounts` / `PaymentMethods` / `AdjustmentReasons` を `Pos.Contract` の Response と同じ列で保持 (エンティティクラスは Response をそのまま使う)。`GET /sync/masters` の結果を Id で削除 → 挿入 (1 トランザクション)。削除済み (`IsDeleted`) も保持し、検索時に除く |
| `InventoryLevels` | 自店分のみ (`updatedSince` で差分取り込み。販売・返品・取消・棚卸ではローカルでも増減させる) |
| `Shifts` / `CashEvents` | 端末で開設したシフトと入出金 (精算の予想現金の計算に使う) |
| `Transactions` | 検索用の列 (種別・状態・シフト・レシート番号・営業日・日時・会員・合計・ポイント・元取引) + `Payload` (`TransactionResponse` の JSON。送信後はサーバの応答で置き換える)。取引履歴・再印字・返品の元取引参照に使う |
| `Outbox` | `Id` (guid)、`Kind` (ShiftOpen / Transaction / TransactionVoid / CashEvent / ShiftClose / InventoryChanges)、`TargetId` (取引 ID やシフト ID)、`Payload` (JSON、`XxxRequest` をそのまま直列化)、`CreatedAt`、`Status` (Pending / Sent / Failed)、`Attempts`、`LastError`、`SentAt`。Sent は 7 日で削除 |
| `SyncState` | `Key` / `Value` (最終 `ServerTime`、在庫の同期時刻、レシート番号の連番)。端末設定 (サーバ URL・店舗 ID・端末 ID) は `IPreferences` (`Settings`) に置く |
| `HoldCarts` | 会計途中の保留 (端末ローカルのみ、T-17)。`Summary` / `Total` と `Cart` の JSON |
