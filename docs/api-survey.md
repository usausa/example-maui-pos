# 公開 POS API の調査

POS サーバ API を設計するにあたり、API 仕様を公開している POS 系 SaaS を調査した記録。
ここから抽出した共通モデルをもとに、判断を [decisions.md](decisions.md)、設計を [api-design.md](api-design.md) / [db-design.md](db-design.md) にまとめている。

## 1. 調査した公開 POS API

| サービス | 種別 | 特徴 |
| --- | --- | --- |
| **Square** (developer.squareup.com) | 小売/飲食 SaaS | 最も体系的。Orders / Payments / Refunds を分離し、Catalog (Item / Variation / Category / Tax / Discount / Modifier) と Inventory (変動履歴ベース) を持つ。Cash Drawer Shifts, Devices (端末ペアリング) も API 化。 |
| **Clover** (docs.clover.com) | 小売/飲食 SaaS | `/v3/merchants/{mId}/...` 配下にフラットな REST。Orders に対して line items / discounts / payments を個別に操作する粒度の細かい設計。Employees / Roles / Shifts / Cash events / Tenders (支払方法) が揃う。 |
| **Lightspeed Retail X-Series** (旧 Vend) | 小売 SaaS | Sales (明細+支払一体) と Register (レジ) の open/close、Inventory counts / adjustments / consignments (入荷・移動)、Price books / Promotions など小売業務に厚い。 |
| **Loyverse** | 小規模店舗向け SaaS | 最もコンパクト。Receipts (SALE/REFUND 一体) / Shifts / Items / Variants / Inventory / Customers / Payment types / POS devices。**サンプルの粒度に最も近い**。 |
| **スマレジ・プラットフォーム API** | 国内 SaaS | 取引 (ヘッダ+明細+支払一体)、精算、日次締め、部門、支払方法、レジ端末、会員/ポイントなど**日本の POS 業務用語がそのまま**リソースになっている。税区分 (内税/外税) や取引区分の考え方の参考になる。 |
| (参考) **Toast** | 飲食 SaaS | Orders (checks / selections) と Menus、Cash Management。飲食特有のテーブル/チェック管理が必要になった場合の参考。 |

### リソース比較

| ドメイン | Square | Clover | Lightspeed X | Loyverse | スマレジ |
| --- | --- | --- | --- | --- | --- |
| 店舗 | Locations | Merchant (単一) | Outlets | Stores | 店舗 |
| レジ端末 | Devices / Terminal | Devices | Registers | POS Devices | レジ端末 |
| スタッフ | Team / Labor | Employees / Roles / Shifts | Users | Employees | スタッフ / 役割・役職 |
| 商品 | Catalog (Item / Variation / Category / Image) | Items / Item groups / Categories / Tags | Products / Types / Categories / Variant attributes | Items / Variants / Categories | 商品 / 部門 / 部門グループ |
| 税 | Catalog TAX | Tax rates / Tax rules | Taxes | Taxes | 税区分 (商品属性) |
| 値引・販促 | Catalog DISCOUNT / PRICING_RULE | Discounts | Promotions / Price books | Discounts | セール / クーポン |
| 支払方法 | (Payment.source_type) | Tenders | Payment types | Payment types | 支払方法 |
| 取引 | **Orders + Payments + Refunds (分離)** | Orders + Line items + Payments | **Sales (一体)** + Returns | **Receipts (一体, SALE/REFUND)** | **取引 (一体, 取引区分/取消区分)** |
| 現金管理 | Cash Drawer Shifts / Events | Cash events / Shifts | Register open/close / Shifts | Shifts | 精算 / 日次締め |
| 在庫 | Inventory (counts / changes / adjustment reasons / transfer) | Item stocks | Inventory levels / adjustments / consignments | Inventory | 在庫 / 在庫変動履歴 |
| 顧客 | Customers / Groups / Loyalty / Gift cards | Customers | Customers / Groups / Store credit / Gift cards | Customers | 会員 / ポイント |
| 仕入 | Vendors / Transfer orders | - | Suppliers / Purchase orders | Suppliers | 仕入先 |
| 通知 | Webhooks / Events | App notifications / Webhooks | Webhooks | Webhooks | Webhooks / 端末通知・印刷 |

### 各 API の特徴的な設計

| サービス | 参考にした点 |
| --- | --- |
| Square | `Money` = 最小通貨単位の整数 + 通貨。`idempotency_key` による冪等性。`CalculateOrder` で登録せずに価格計算。Inventory は `PHYSICAL_COUNT` / `ADJUSTMENT` / `TRANSFER` の変動履歴。Cash Drawer Shift に `opened_cash_money` / `expected_cash_money` / `closed_cash_money` とイベント (PAID_IN / PAID_OUT / NO_SALE …)。Devices API のデバイスコードで端末ペアリング |
| Clover | `modifiedTime` フィルタによる差分取得。Order に対する line item / discount / payment の個別操作。Cash events を端末・従業員別に取得 |
| Lightspeed X | Sale の `state` (parked / pending / voided / closed)。Adjustment (`DISCOUNT` / `TIP` / `NON_CASH_FEE`) の `target.method` (PROPORTIONED / EACH / SINGLE) による按分。`register_open_time` / `register_close_time`。ユーザー指定 ID による冪等性 |
| Loyverse | Receipt (`receipt_type` SALE / REFUND、`refund_for`) に line_items と payments を内包。Shift に starting_cash / cash_payments / cash_refunds / paid_in / paid_out / expected_cash / actual_cash |
| スマレジ | 取引ヘッダ (取引区分 / 取消区分 / 預り金 / 釣銭 / レシート番号 / 端末取引 ID) + 取引明細 (税区分 / 単価 / 販売単価) + 支払 (depositOthers)。精算 / 日次締めを別リソースに分離。役割・役職による実行制御 |

## 2. 調査から得られた共通ドメインモデル

どのサービスにも共通して存在するものを整理すると、以下の 4 層になる。

1. **マスタ**: 店舗 / レジ端末 / スタッフ / 部門(カテゴリ) / 商品 / 税率 / 値引定義 / 支払方法
2. **トランザクション**: 取引 (明細・値引・税・支払) / 取消・返品 / レジ開閉 (精算) / 入出金 / 日次締め
3. **付随データ**: 在庫 (現在庫 + 変動履歴) / 顧客 (会員・ポイント)
4. **横断**: 認証 (端末 + スタッフ) / 差分同期 / 冪等性 / ページング / 通知

設計上の主な分岐点 (判断は [decisions.md](decisions.md)):

| 論点 | 選択肢 A | 選択肢 B | 判断 |
| --- | --- | --- | --- |
| 取引の表現 | 一体型: 1 取引 = ヘッダ + 明細 + 支払 (Loyverse / Lightspeed / スマレジ) | 分離型: Order と Payment を別リソース (Square / Clover) | [D-01](decisions.md#d-01-取引モデル-一体型-vs-分離型) |
| 金額計算の主体 | 端末で計算し、サーバは検証 (オフライン運用可) | サーバで計算 (Square `CalculateOrder`, Toast prices) | [D-02](decisions.md#d-02-金額計算の主体-端末計算--サーバ検証-vs-サーバ計算のみ) |
| 在庫の表現 | 変動履歴ベース (Square) | 数量の直接更新 (Clover) | [D-12](decisions.md#d-12-在庫-変動履歴ベース) |
| 冪等性 | クライアント採番 ID (Lightspeed) | `Idempotency-Key` ヘッダ (Square) | [D-10](decisions.md#d-10-冪等性-クライアント採番-id) |

## 3. 参考資料

- Square API Reference: https://developer.squareup.com/reference/square
  - Orders API: https://developer.squareup.com/reference/square/orders-api
  - Payments API: https://developer.squareup.com/reference/square/payments-api
  - Refunds API: https://developer.squareup.com/reference/square/refunds-api
  - Catalog API: https://developer.squareup.com/reference/square/catalog-api
  - Inventory API: https://developer.squareup.com/reference/square/inventory-api
  - Cash Drawers API: https://developer.squareup.com/reference/square/cash-drawers-api
  - Devices API: https://developer.squareup.com/reference/square/devices-api
- Clover REST API Reference: https://docs.clover.com/dev/reference/api-reference-overview (索引: https://docs.clover.com/dev/llms.txt)
- Lightspeed Retail (X-Series) API: https://x-series-api.lightspeedhq.com/ (索引: https://x-series-api.lightspeedhq.com/llms.txt)
  - Create a sale: https://x-series-api.lightspeedhq.com/reference/createsale
  - Open register: https://x-series-api.lightspeedhq.com/reference/openregister
- Loyverse API: https://developer.loyverse.com/docs/ (SDK からのリソース一覧: https://github.com/siarheipashkevich/loyverse-sdk)
- スマレジ・プラットフォーム API リファレンス (POS): https://developers.smaregi.dev/platform-api-reference/apis/pos/
  - 共通仕様: https://developers.smaregi.dev/apidoc/common/
- Toast Orders API overview: https://doc.toasttab.com/doc/devguide/portalOrdersApiOverview.html
