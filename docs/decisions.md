# 設計判断の記録 (Decision Log)

POS サーバ API / DB 設計にあたって行った判断の記録。  
各項目は次の形式で残す。

- **背景**: 何を決める必要があったか
- **選択肢**: 検討した案と長所・短所
- **決定**: 採用した案 (✅)
- **意図**: なぜその案にしたか
- **反映**: API / DB 設計への影響

「利用者と確認済み」の判断は §1、設計者が慣例から判断し利用者に確認をとっていないものは §2、参考プロジェクト (既存テンプレート) を確認して合わせた判断は §3、実装前に確認した事項は §4、実装中の判断は §5 に分ける。  
§2 / §3 / §5 は異論があれば変更してよい。

---

## 0. 前提の要約

| 項目 | 決定 | 参照 |
| --- | --- | --- |
| 業種 | 家電・カメラ・ホームセンター (物販) | [D-00](#d-00-業種前提) |
| 技術スタック | 既存テンプレート準拠: SQLite + Smart.Data.Accessor、Minimal API + Blazor Server (MudBlazor) + Aspire、MAUI (Android) + Smart.Navigation。層はサーバが Service、端末が Service / Usecase ([D-45](#d-45-サーバの-service-層) / [D-46](#d-46-端末の-service--usecase-とナビゲーションのコンテキスト)) | [D-19](#d-19-技術スタックプロジェクト構成-テンプレート準拠) |
| プロジェクト名 | `Pos.Server.*` (Core / Host / AppHost) / `Pos.Terminal` / `Pos.Contract` (通信データ) / `Pos.Domain` (ドメインロジック) | [D-22](#d-22-共有プロジェクト-通信データとドメインロジックは別プロジェクト), [D-26](#d-26-命名-posserver--posterminal--poscontract--posdomain) |
| 取引モデル | 一体型 (会計完了後に 1 回で送信)。受注は将来 | [D-01](#d-01-取引モデル-一体型-vs-分離型) |
| 金額計算 | 端末計算 + サーバ検証 (`Pos.Domain`) | [D-02](#d-02-金額計算の主体-端末計算--サーバ検証-vs-サーバ計算のみ) |
| MVP | 販売・レジ開閉精算 + 顧客ポイント・在庫・返品交換・売上レポート | [D-03](#d-03-mvp-の範囲) |
| ポイント | 商品別還元率 + 1pt = 1 円充当 (支払方法として扱う) | [D-07](#d-07-ポイント制度) |
| シリアル番号 | モデルに含める。入力 UI は Phase 2 | [D-04](#d-04-シリアル番号-製造番号) |
| 配送 | 取引に `delivery` を任意で持つ | [D-08](#d-08-配送情報) |
| 管理系 | サーバ同居の Blazor 管理画面 (MudBlazor) + 管理 API | [D-05](#d-05-管理系-crud-の置き場所), [D-18](#d-18-管理画面の構成) |
| テナント | 単一 | [D-06](#d-06-テナント構成) |
| JSON / ページング | camelCase、`page` / `size` + 総件数 | [D-20](#d-20-json-契約-camelcase), [D-21](#d-21-ページング-page--size--総件数) |
| 金額・数量・率 | `decimal` (SQLite は NUMERIC 列)、ID は端末採番の GUID | [D-13](#d-13-金額数量率の表現-decimal), [D-10](#d-10-冪等性-クライアント採番-id) |
| 用語 | 通信データは `XxxRequest` / `XxxResponse`。「DTO」は使わない | [D-27](#d-27-用語-dto-は使わない) |
| 端末 UI | スマートフォン縦持ち、カメラスキャン、メニュー型、テンプレートのシェル (タイトル + F1〜F4) | [D-17](#d-17-端末のナビゲーション構成), [D-23](#d-23-端末の画面骨格-template-maui-のシェル準拠) |
| 認証・端末登録 | 後回し (MVP は認証なし。Phase 2 で template-maui-server を参考に追加) | [D-09](#d-09-認証端末登録-後回し) |
| リポジトリ | モノレポ。`server/Pos.Server.slnx` と `terminal/Pos.Terminal.slnx` を VS で個別に開く。共有は `shared/` | [D-32](#d-32-リポジトリ構成-モノレポ--2-ソリューション) |
| 進め方 | フェーズ単位のチェックリスト (implementation-plan.md) | [D-33](#d-33-実装の進め方-フェーズ単位のチェックリスト) |

---

## 1. 利用者と確認済みの判断

### D-00. 業種・前提

| 項目 | 内容 |
| --- | --- |
| 業種 | **家電・カメラ・ホームセンター** (物販小売) |
| 構成 | MAUI = レジ端末アプリ、ASP.NET Core = POS サーバ (API + Blazor 管理画面を同居) |
| 決済 | 決済ゲートウェイ連携なし。現金は釣銭計算まで、クレジット / QR / 電子マネーは支払方法として金額を記録するだけ |
| 税 | 日本の消費税 (標準 10% / 軽減 8% / 非課税)、商品ごとの内税 / 外税、税率ごとの集計 (インボイス対応レシート) |
| オフライン | 端末はネットワーク断でも販売継続できる前提 |

業種から自動的に決まったこと:

- **飲食要素 (モディファイア / テーブル / 伝票分割) は扱わない** → 取引モデルを「注文 → 会計」の 2 段階にする必要がない
- **商品バリエーション (色 / サイズの親子商品) は扱わない** → 家電は色違いでも JAN コードが別なので「1 商品 = 1 JAN」で足りる
- 代わりに家電特有の論点として **ポイント制度 (D-07)**、**シリアル番号 (D-04)**、**配送 (D-08)** を扱う

---

### D-01. 取引モデル: 一体型 vs 分離型

**背景**: 「デジカメ 80,000 円を現金 50,000 円 + クレジット 30,000 円で購入」のような会計をサーバへどう伝えるか。  
API の本数・状態遷移・オフライン対応のしやすさが大きく変わる。

**選択肢**:

| 案 | 内容 | 長所 | 短所 |
| --- | --- | --- | --- |
| ✅ **A. 一体型** (Loyverse / Lightspeed / スマレジ) | 端末で会計を完了させてから `POST /transactions` で明細 + 値引 + 税 + 支払を 1 回で送る | API 1 本。完了済み取引をキューで送るだけなのでオフラインと相性がよい。実装量が小さい | サーバが「会計途中」を知らない。保留の端末間共有、注文だけ先にキッチンへ、決済端末で先に決済確定 → 取引確定、といった流れは表現できない |
| B. 分離型 (Square / Clover) | `POST /orders` → `POST /payments` → `POST /orders/{id}/complete` の 3 段階。会計途中の状態をサーバが持つ | 決済端末連携や保留の共有、テーブル会計、EC 注文の店頭受取に自然に拡張できる | API と状態遷移が増える。オフライン時は結局端末で溜めて送るので二重管理になる |
| C. 一体型 + 受注 API を最初から | 取引は A のまま、取り寄せ / 取り置き / 配送用の「受注」リソースも MVP に含める | 家電店の取り寄せ業務を最初から見せられる | MVP が大きくなる |

**決定**: ✅ **A. 一体型**。  
受注 (取り寄せ / 取り置き) は将来、取引とは別の「受注」リソースとして追加する (スマレジも受注管理を別 API にしている)。

**意図**: レジ端末で会計が完結する物販 POS なので、会計途中をサーバが持つ必要がない。  
オフライン運用を前提にすると、完了した取引を送るだけの A が最も単純で堅い。

**反映**: `POST /transactions` が唯一の取引登録 API。  
返品も `type = Return` の取引として同じ API で送る。  
取消は `POST /transactions/{id}/void`。  
会計途中の「保留」は端末ローカル機能。

---

### D-02. 金額計算の主体: 端末計算 + サーバ検証 vs サーバ計算のみ

**背景**: 小計・値引・税・ポイント・合計を誰が計算するか。  
オフライン可否と、計算ロジックの置き場所が決まる。

**選択肢**:

| 案 | 内容 | 長所 | 短所 |
| --- | --- | --- | --- |
| ✅ **A. 端末計算 + サーバ検証** | 端末が計算して合計まで含めて送信。サーバは同じロジックで再計算し、一致しなければ 422 で拒否 | オフラインで会計できる。MAUI も ASP.NET Core も .NET なので**計算ロジックを 1 つの C# ライブラリで共有**できる | ロジックの二重実行 (ただしコードは 1 つ) |
| B. サーバ計算のみ | 端末は明細だけ送り `POST /transactions/calculate` で合計を受け取る | ロジックが一箇所。端末が薄くなる | ネットワーク断で会計が止まる。毎回往復が必要 |

**決定**: ✅ **A. 端末計算 + サーバ検証**。

**意図**: オフライン前提 (D-00) と矛盾しないのは A だけ。  
共有ライブラリにすれば「ロジックが二箇所」にはならない。

**反映**: 計算仕様を [api-design.md §4](api-design.md#4-金額税ポイント計算仕様-共有ライブラリ) に明文化し、`Pos.Domain` ([D-22](#d-22-共有プロジェクト-通信データとドメインロジックは別プロジェクト)) に実装する。  
`POST /transactions/calculate` は検証・テスト用に残す。

---

### D-03. MVP の範囲

**背景**: 「販売 〜 レジ開閉・精算」に加えて、最初の実装に何を含めるか。

**選択肢** (複数選択):

| 機能 | 家電店での使われ方 | 含めない場合 |
| --- | --- | --- |
| ✅ 顧客・ポイント | 会員バーコードをスキャン → 商品ごとの還元率でポイント付与、ポイントで支払い | 取引に顧客が紐付かない。家電量販店らしさが出ない |
| ✅ 在庫 | 販売で自店在庫を減算、他店在庫の照会、棚卸・調整 | 在庫テーブル・変動履歴が不要になる |
| ✅ 返品・交換 | レシート番号から取引を呼び出し、明細単位で返品。ポイント・在庫も戻す | 全額取消のみ |
| ✅ 売上レポート | 日別 / 端末別 / 担当別 / 支払方法別 / 税率別 / 部門別 | 管理画面で見るものがない |

**決定**: ✅ **すべて MVP に含める**。

**意図**: 家電・カメラ店のサンプルとして「ポイント」「他店在庫」「初期不良の返品」は外せない。  
レポートは Blazor 管理画面 (D-05) の主なコンテンツになる。

**反映**: `Customers` / `PointHistories` / `InventoryLevels` / `InventoryChanges` を MVP のテーブルに含める。  
`POST /transactions` は `type = Return` を受け付ける。  
`GET /reports/sales/*` を MVP に含める。

---

### D-04. シリアル番号 (製造番号)

**背景**: カメラ・家電はレシート / 保証書に製造番号を印字することが多い。  
どこまで扱うか。

**選択肢**:

| 案 | 内容 | 長所 | 短所 |
| --- | --- | --- | --- |
| ✅ **A. 明細に記録 (モデルは MVP、UI は Phase 2)** | 商品に「シリアル入力要」フラグ、取引明細に `serialNumbers[]`。入力 UI と必須チェックは Phase 2 | 追加が小さい。後からテーブルを増やさずに済む | MVP 時点では入力されない |
| B. 明細に記録・MVP から入力 | A の UI・検証も MVP に含める | 最初からレシートに印字できる | MVP の MAUI 画面が増える |
| C. シリアル在庫まで追跡 | 入荷時にシリアル登録し、どの店にどのシリアルがあるかを管理。販売時に在庫シリアルから選ぶ | 保証・修理・盗難対策に強い | 在庫モデルが大きく変わる (Phase 3 規模) |
| D. 不要 | 扱わない | 最小 | 家電店らしさが減る |

**決定**: ✅ **A**。

**意図**: データモデルは最初から持っておかないと後で取引テーブルの移行が必要になる。  
一方で UI は MVP の範囲を広げないため後回しにする。

**反映**: `Products.RequiresSerial`、`TransactionLineSerials` テーブル、`line.serialNumbers[]`。  
MVP ではサーバは受け取って保存するだけで、必須チェックはしない。

---

### D-05. 管理系 CRUD の置き場所

**背景**: 商品・スタッフ・店舗などマスタの登録・更新をどこで行うか。  
管理 API の要否と優先度が決まる。

**選択肢**:

| 案 | 内容 | 長所 | 短所 |
| --- | --- | --- | --- |
| A. レジ専用 + シードデータ | サーバ起動時に CSV 等で投入。MAUI は参照のみ。登録・更新 API は後回し | 「レジ端末アプリ」に集中できる | 管理画面がなく、データ変更はシード編集 |
| ✅ **B. サーバ同居の Web 管理画面 (Blazor)** | ASP.NET Core サーバに API と Blazor 管理画面を同居させ、マスタ管理・レポート閲覧を行う | 「本部 (管理画面) → 店舗 (レジ)」の構成をサンプルで示せる | 管理 API と画面の実装量が増える |
| C. MAUI 内に管理モード | タブレット 1 台で商品登録まで完結 | 小規模店向けに現実的 | チェーン想定 (D-00) と合わない |

**決定**: ✅ **B**。  
利用者の当初想定 (サーバは API + Blazor で作る) に一致。  
基盤は `template-maui-server` (Minimal API + Blazor Server + MudBlazor) を流用する ([D-19](#d-19-技術スタックプロジェクト構成-テンプレート準拠))。

**意図**: 家電量販店・ホームセンターはチェーンなので、マスタは本部で管理して店舗へ配信する構成が自然。  
Blazor 管理画面から使う CRUD を API として定義しておけば、Blazor 側は HTTP 経由でもアプリケーションサービス直呼びでも実装できる。

**反映**: 各マスタに登録 (`POST`) / 更新 (`PUT`) / 論理削除 (`DELETE`) を定義し、「管理系」として区別する。  
端末が使うのは参照と差分同期のみ。  
Blazor ページは Accessor と Domain を直接呼ぶ (HTTP を経由しない、[D-19](#d-19-技術スタックプロジェクト構成-テンプレート準拠))。

---

### D-06. テナント構成

**背景**: 1 社専用か、複数社が使う SaaS 型か。  
後から変えると全テーブル・全クエリに手が入る。

**選択肢**:

| 案 | 内容 | 長所 | 短所 |
| --- | --- | --- | --- |
| ✅ **A. 単一テナント** | 1 社・複数店舗・複数端末。URL は `/api/v1/products`、DB に TenantId なし | 単純。サンプルの本題 (POS) に集中できる | SaaS 化するときに全面改修 |
| B. マルチテナント | トークンに tenantId、全テーブルに TenantId 列 + 全 SQL への条件付加。URL は `/api/v1/{tenantId}/...` かトークン判別 | 「POS SaaS の作り方」を示せる | 全リソースにテナント境界の実装とテストが必要 |

**決定**: ✅ **A. 単一テナント**。

**意図**: 「自社チェーンの POS」を示すサンプルなので、テナント分離はノイズになる。

**反映**: TenantId 列なし。  
店舗 (`Stores`) が最上位の組織単位。

---

### D-07. ポイント制度

**背景**: 家電量販店ではポイントが購買体験の中心。  
計算方法で商品マスタ・取引・支払の構造が変わる。

**選択肢**:

| 案 | 内容 | 長所 | 短所 |
| --- | --- | --- | --- |
| ✅ **A. 商品別還元率 + 1pt = 1 円充当** | `Product.pointRate` (10% / 5% / 1% / 0%) で付与。ポイント利用は支払方法の一種 (`kind = Points`) として扱い、充当分にはポイントを付けない | 家電量販店の実態に近い。支払方法として扱うので精算・レポートに自然に載る | 按分計算が必要 (充当分の控除) |
| B. 取引合計に一律率 | 例: 合計の 1% を付与 | 単純 | 商品別の還元率が表現できない |
| C. 付与のみ (利用なし) | 残高を積み上げるだけ | 最小 | ポイント払いがない |

**決定**: ✅ **A**。  
付与基準 (税込 / 税抜) と端数処理は会社設定にする。

**意図**: 「デジカメは 10%、SD カードは 1%」のような商品別還元と、ポイント払いの両方があって初めて家電店のレジらしくなる。

**反映**: `Products.PointRate`、`PaymentMethods.Kind = Points`、`Transactions.PointsEarned / PointsRedeemed / PointsBalanceAfter`、`TransactionLines.PointsEarned`、`Customers.PointBalance`、`PointHistories`。  
計算式は [api-design.md §4.4](api-design.md#44-ポイント)。

---

### D-08. 配送情報

**背景**: 大型家電や資材は配送、在庫切れは取り寄せが日常。  
レジ会計時の配送情報をどう扱うか。

**選択肢**:

| 案 | 内容 | 長所 | 短所 |
| --- | --- | --- | --- |
| ✅ **A. 取引に `delivery` を任意で持つ** | 配送先・希望日・時間帯を取引に記録。配送料は「配送料」商品 (サービス種別) の明細として計上。配送手配自体はスコープ外 | 追加が小さい (1 テーブル) | 配送の進捗管理はできない |
| B. 受注 (orders) で扱う | 配送・取り寄せ・取り置きをまとめて受注リソースで管理 (Phase 2)。取引はレジ会計のみ | 取り寄せまで一貫して扱える | D-01 の「受注は将来」と同じく MVP 外になる |
| C. 扱わない | 配送関連はスコープ外 | 最小 | 家電店らしさが減る |

**決定**: ✅ **A**。  
取り寄せ・取り置きは将来の受注リソースで扱う。

**意図**: レシートに配送先を印字できれば家電店のレジとして十分。  
進捗管理まで踏み込むと別システムの領域になる。

**反映**: `TransactionDeliveries` テーブル (取引 1 : 0..1)、`Products.Kind = Service` (配送料・延長保証・設置工事など、在庫を持たない商品)。

---

### D-09. 認証・端末登録 (後回し)

**背景**: 端末とスタッフの認証・認可をどう行うか。

**決定**: ✅ **後回し**。  
まず API と DB の設計・実装を進める。

**当面の扱い**: 端末発の要求は本文 / クエリで `storeId` / `terminalId` / `staffId` を明示的に渡す。  
認証導入後はトークンのクレームと本文の一致をサーバが検証する形にすれば、API の形を変えずに済む。  
ベースにした `Service-CloudManager` には認証がないため MVP は認証なし。  
Phase 2 で `template-maui-server` の管理画面 Cookie ログインと API の JWT (`/api/account/login`) を参考に追加する ([D-34](#d-34-参考プロジェクトの差し替え-phase-0))。

**将来の方針案** (未決定):

- 端末登録: 管理画面でワンタイムコード発行 → 端末がコードを送って端末トークン取得 (Square Devices API のデバイスコード方式)
- スタッフ認証: スタッフコード + PIN → JWT (storeId / terminalId / staffId / role)。  
  テンプレートの `TokenService` を流用
- 認可: 役割 (Cashier / Manager / Admin) で取消・値引承認・精算・マスタ編集を制御

**反映**: `Staff.Role` 列と `TransactionDiscounts.ApprovedByStaffId` 列は先に用意しておく (値は MVP では未使用)。

---

### D-17. 端末のナビゲーション構成

**背景**: 端末は通常のスマートフォン (縦持ち・片手) で、カメラによる JAN / QR スキャンを使う。  
機能をどう並べるか。

**選択肢**:

| 案 | 内容 | 長所 | 短所 |
| --- | --- | --- | --- |
| ✅ **A. メニュー型** | ホーム画面に機能タイル (販売 / 返品 / 精算 / 照会 …)。各機能からはホームへ戻る | 分かりやすい。機能追加が容易。レジ以外の業務 (棚卸・照会) と同列に扱える | 販売に入るのに毎回 1 タップ |
| B. 販売起点 + ドロワー (Square / Loyverse 型) | ログイン後すぐ販売画面。他機能は左上のハンバーガーメニュー | レジ担当の操作の 9 割が販売なので最短 | 販売以外が隠れる。ドロワーは片手で開きにくい |
| C. ボトムタブ | 下部に 販売 / 取引 / 照会 / メニュー の 4 タブ | 頻出機能を常時表示 | タブ数を増やせない。縦画面で販売画面の高さが減る |

**決定**: ✅ **A. メニュー型** (利用者指定)。  
折衷として「販売タイルを最上段に大きく」「設定でログイン後に販売画面を直接開ける」を提案 ([screen-design.md §1.2](screen-design.md#12-ナビゲーション構成))。

**意図**: サンプルとして機能の一覧性を優先する。  
B の利点 (最短で販売に入る) は設定で補える。  
`template-maui` のメニュー画面 (大きなボタンのグリッド) と同じ作りになる。

**反映**: 画面 T-02 ホーム。  
レシートは画面表示 + 電子レシート QR を基本にし、Bluetooth 印刷は Phase 2。  
画面の骨格 (タイトル + F1〜F4) は [D-23](#d-23-端末の画面骨格-template-maui-のシェル準拠)。

---

### D-18. 管理画面の構成

**背景**: サーバ側 (Blazor) の画面構成。

**決定**: ✅ **左ナビゲーション + 右コンテンツ (一覧ページ + 詳細 / 編集ダイアログ)** の Blazor によくある構成 (利用者指定)。  
Blazor Web App (Interactive Server) で API と同じ ASP.NET Core に同居させる。

**UI ライブラリ**: ✅ **MudBlazor** (`template-maui-server` / `template-blazor-server` / `CloudManager` がいずれも MudBlazor のため)。  
`MudNavMenu` + `MudNavGroup` のグループ化ナビ、`MudDataGrid` の `ServerData` によるサーバ側ページング、`IDialogService` によるダイアログ、FluentValidation によるフォーム検証、Snackbar 通知をテンプレートのまま使う。

**反映**: [screen-design.md §2](screen-design.md#2-サーバ管理画面-blazor)。  
管理画面の機能は API の「管理」用途と 1 対 1 に対応させる。

---

## 2. 設計者判断 (未確認、異論があれば変更可)

### D-10. 冪等性: クライアント採番 ID

| 案 | 内容 |
| --- | --- |
| ✅ **A. クライアント採番 GUID (v7)** | 取引・シフト・入出金・在庫変動など端末発の書き込みは端末が ID を採番して送る。同じ ID が既にあれば 200 で既存を返す (本文が異なれば 409) |
| B. `Idempotency-Key` ヘッダ | Square 方式。サーバがキーと応答を保存する専用テーブルが必要 |

**意図**: オフライン再送を扱うには ID をクライアントが持つのが最も単純。  
`Guid.CreateVersion7()` (.NET 9) で時系列順にもなる。

**補足 (SQLite)**: テンプレートは `INTEGER PRIMARY KEY AUTOINCREMENT` の `long` ID だが、端末採番のために GUID を採る。  
SQLite では **TEXT (36 文字、小文字ハイフン区切り)** で保存する (`Microsoft.Data.Sqlite` の既定。v7 は文字列順 = 時系列順になる)。  
サーバだけが採番するもの (取引由来の在庫変動・ポイント履歴) も同じ GUID にそろえる。

### D-11. 税計算: 税率ごと一括計算

| 案 | 内容 |
| --- | --- |
| ✅ **A. 税率ごとに合計してから税額計算** | 同じ税率の明細の合計に対して税額を計算し、税率ごとに端数処理。インボイス対応レシートの「税率ごとの区分記載」と一致する |
| B. 明細ごとに税額計算して合計 | 明細ごとに端数処理するため、合計で誤差が出やすい |

**意図**: 日本のレシートの標準的な方式。  
取引値引は明細へ按分してから税計算する。

### D-12. 在庫: 変動履歴ベース

| 案 | 内容 |
| --- | --- |
| ✅ **A. 変動履歴 (`InventoryChanges`) + 現在庫 (`InventoryLevels`)** | Square Inventory API と同じ。販売 / 返品 / 取消 / 棚卸 / 調整をすべて履歴として残し、現在庫はその集計 (非正規化して保持) |
| B. 現在庫数量を直接更新 | Clover item stocks 方式。単純だが監査できない |

**意図**: 棚卸差異や返品の追跡ができる。  
取引による自動減算と手動調整を同じ仕組みで扱える。

### D-13. 金額・数量・率の表現: `decimal`

| 案 | 内容 |
| --- | --- |
| ✅ **A. 金額・数量・率とも `decimal`** + 会社設定の通貨コード | C# で自然。JPY は整数値のみ使う。数量は `decimal(9,2)` 相当で切り売り (m 単位) も表現できる |
| B. 金額は整数 (円、`long`)、数量は整数 (`int`) | Square `Money` (最小通貨単位の整数) 方式。SQLite の `INTEGER` に素直に載るが、C# 側で扱いが冗長 |

**決定**: ✅ **A** (利用者確認済み。SQLite 採用時に一度 B に変えたが、`decimal` でよいとの指示で A に戻した)。

**SQLite での扱い**: `Microsoft.Data.Sqlite` は `decimal` パラメータを TEXT で書き込むため、金額・数量・率の列は **`NUMERIC` 親和性**で宣言する。  
数値として正しい文字列は INTEGER / REAL に変換されて保存されるので、`SUM` などの集計がそのまま使える。  
読み出しは `GetDecimal` が INTEGER / REAL / TEXT のいずれからも変換する ([db-design.md §1](db-design.md#1-前提))。

### D-14. 返品の表現: `type = Return` の取引

| 案 | 内容 |
| --- | --- |
| ✅ **A. 返品を「取引」として登録** (`type = Return`、`originalTransactionId` で元取引に紐付け) | Loyverse の receipt_type = REFUND と同じ。オフラインでも端末が返品取引を作って送れる |
| B. `POST /transactions/{id}/refunds` で元取引に返金を追加 | Square 方式。サーバ側で元取引を更新する必要があり、オフライン時に扱いにくい |

**意図**: 一体型 (D-01) と D-02 を貫くと、返品も「端末で完結した取引」として同じ経路で送るのが一貫する。  
交換は「返品取引 + 販売取引」の 2 件で表す。

### D-15. レシート番号: 端末採番

| 案 | 内容 |
| --- | --- |
| ✅ **A. 端末が `{店舗コード}-{端末番号}-{連番}` を採番** (例 `S001-02-000123`) | オフラインでも採番できる。サーバは一意制約で重複を弾く |
| B. サーバ採番 | オフライン時に番号が決まらず、レシートが印字できない |

### D-16. 日次締めは Phase 2

店舗 × 営業日の締め (`DailyClosings`) は精算 (シフト) が揃ってからの集計であり、MVP のレポート API で代替できるため Phase 2 とする。  
テーブル定義だけ DB 設計に載せる。

---

## 3. 参考プロジェクトを確認して合わせた判断

利用者指定の参考プロジェクトを確認し、その流儀に合わせた判断。

| 本サンプル | ベースにしたプロジェクト | 流用したもの |
| --- | --- | --- |
| サーバ (`Pos.Server.*`) | `D:\GitHubService\Service-CloudManager` (`CloudManager.Core` / `CloudManager.Host`) | ソリューション構成 (Core / Host / UnitTests / IntegrationTests)、`Program.cs` と `ApplicationExtensions` (Serilog / ヘルスチェック / ProblemDetails / 圧縮 / エラーページ)、MudBlazor レイアウト・ダイアログ・Snackbar・FluentValidation、`NavMenu` のグループ化、`SqlHelper`、テスト基盤 (`TestApplicationFactory`、`MudBlazorTestBase`) |
| | `D:\GitHubTemplate\template-blazor-server` (`Template.BlazorServer.*`) | Aspire AppHost、OpenAPI (`Microsoft.AspNetCore.OpenApi` + NSwag の Swagger UI / ReDoc)。CSV 出力 (CsvHelper) と PDF 帳票 (OysterReport) は Phase 2 以降で参考にする |
| | `D:\GitHubTemplate\template-maui-server` | Phase 2 の認証 (管理画面 Cookie ログイン、API JWT)、設定 QR (`QrPage`) の参考 |
| 端末 (`Pos.Terminal`) | `D:\GitHubTemplate\template-maui-keyboard` (`Template.MobileApp`) | `MauiProgram` の構成 (BunnyTail DI、`[ComponentRegistration]`)、シェル (`MainPage` + `ShellProperty` + F1〜F4)、`AppViewModelBase`、`InputNumber` ポップアップ、Input (物理キー・ショートカット)、Behaviors、`Colors.xaml` / `Styles.xaml` |
| | `D:\GitHubTemplate\template-maui` (`Template.MobileApp`) | 販売・会計画面のデザイン (`UIPosView`)。QR スキャン / 表示、`Settings` / `SettingParser`、`NetworkOperator`、SQLite `DataAccessor` を Phase 6 で取り込んだ (`HttpService` は HttpClient で書き直し、[D-40](decisions.md#d-40-端末の通信-rester-ではなく-httpclient)) |

### D-19. 技術スタック・プロジェクト構成 (テンプレート準拠)

| 案 | 内容 |
| --- | --- |
| A. EF Core + 任意の RDBMS | 当初案 (DB 設計 v0.1) |
| ✅ **B. テンプレートと同じ構成: SQLite + `Usa.Smart.Data.Accessor` (SQL ファイル) + Minimal API + Blazor Server (MudBlazor) + Aspire AppHost** | 利用者のテンプレート群と同じ書き方になり、流用・比較がしやすい |

**決定**: ✅ **B**。  
DB は利用者指定で SQLite。

**反映**:

- ORM ではなく `[DataAccessor]` + 2-way SQL ファイル。  
  テーブルは起動時に `CREATE TABLE IF NOT EXISTS` (テンプレートの `InitializeApplicationAsync` → `CreateTable()`) で作る。  
  マイグレーションは持たない
- 当初は **Service / Usecase の層を置かず**、Endpoints (API) / Blazor ページ → Accessor (SQL) + Domain (ロジック) の 2 段としていた。  
  ソースの見直しで、SQL を Accessor に閉じ、業務の手順を Service に集める形に変えた ([D-45](#d-45-サーバの-service-層)、端末は [D-46](#d-46-端末の-service--usecase-とナビゲーションのコンテキスト))。  
  プロジェクトは `Core` (Accessors / Sql / Models / Services) と `Host` (Endpoints / Application / Models / Components / Settings)
- Serilog / OpenTelemetry / FeatureManagement / ヘルスチェック / OpenAPI (NSwag UI) はテンプレートのまま
- MAUI 側は `template-maui` 系テンプレートの構成 (Smart.Navigation + 独自シェル、Smart.Mvvm、BarcodeScanning.Native.Maui、Smart.Data.Accessor + SQLite、BunnyTail DI) をそのまま使う (通信は Rester ではなく HttpClient、[D-40](#d-40-端末の通信-rester-ではなく-httpclient)) (Phase 0 でベースを `template-maui-keyboard` に変更、[D-34](#d-34-参考プロジェクトの差し替え-phase-0))。  
  **Android 専用** (テンプレートが `net10.0-android` のみ)

### D-20. JSON 契約: camelCase

| 案 | 内容 |
| --- | --- |
| ✅ **A. camelCase** (Minimal API の既定 `JsonNamingPolicy.CamelCase`) | 利用者指示。MAUI 側は `HttpService.JsonOptions` (System.Text.Json の Web 既定) で camelCase にする (Rester から HttpClient に変更、[D-40](#d-40-端末の通信-rester-ではなく-httpclient)) |
| B. PascalCase (`PropertyNamingPolicy = null`) | `template-maui-server` の現状 (Rester 既定との契約)。テンプレート側を camelCase に変更予定とのことなので、実装時にテンプレートを再確認する |

**決定**: ✅ **A**。  
日時は `yyyy-MM-ddTHH:mm:ss.fffZ` (UTC) のテンプレート `DateTimeConverter`、`null` プロパティは省略、クエリパラメータも camelCase (`?storeId=`)。

### D-21. ページング: `page` / `size` + 総件数

| 案 | 内容 |
| --- | --- |
| A. カーソル方式 (`cursor` / `nextCursor`) | 当初案。Square 流 |
| ✅ **B. `page` (0 始まり) / `size` + 一覧応答 `xxxListResponse { total, page, size, items }`** | テンプレートの `DataListResponse` と `MudDataGrid` の `ServerData` (総件数が必要) に一致する |

**決定**: ✅ **B**。  
並び替えは `sort` / `desc` を受け付ける (検証の方法は [D-48](#d-48-サーバの見直し-並び順の列挙型returning初期データの-sql))。  
差分同期は `updatedSince` で絞った一覧を `UpdatedAt, Id` 順にページングする。

### D-22. 共有プロジェクト: 通信データとドメインロジックは別プロジェクト

「`XxxRequest` / `XxxResponse` (通信データ) の共有」と「ドメインロジックの共有」は別の概念 (利用者指示) なので、混ぜずに分ける。

| 案 | 内容 |
| --- | --- |
| A. 1 つの共有プロジェクトに通信データと計算ロジックをまとめる | 当初案。概念が混ざる |
| ✅ **B. `Pos.Domain` (ドメインロジック) と `Pos.Contract` (通信データ) の 2 プロジェクト** | ドメインロジックを共通に切り出すなら `Xxx.Domain` (利用者指示)。通信データは Blazor の Client / Server / Shared 慣例に倣い `Shared` |
| C. 通信データは共有せず、テンプレート流儀でサーバ (`Web/Models/Api`) と端末 (`Models/Api`) に別々に持つ | 契約の変更に両側の修正が要る。取引の Request / Response は大きく、ずれやすい |

**決定**: ✅ **B**。

- `Pos.Domain`: 列挙型、計算ロジック (税・値引按分・ポイント・返品導出)、業務ルールの検証。  
  UI・DB・HTTP に依存しない
- `Pos.Contract`: `XxxRequest` / `XxxResponse`。  
  `Pos.Domain` の列挙型を参照する。  
  サーバ (`Web`) と端末の両方から参照する
- エンティティ (DB) はサーバ `Core` に、端末のローカルエンティティは MAUI 側に、それぞれテンプレートどおり残す

### D-26. 命名: `Pos.Server.*` / `Pos.Terminal` / `Pos.Contract` / `Pos.Domain`

テンプレートの `Template.MobileServer.*` / `Template.MobileApp` に対し、`Template` の部分を `Pos` にし、続けてサーバ / 端末が分かる名称にする (利用者指示)。

| プロジェクト | 内容 |
| --- | --- |
| `Pos.Server.Core` / `Pos.Server.Host` / `Pos.Server.AppHost` | サーバ (テンプレートの `Template.MobileServer.*` 相当) |
| `Pos.Terminal` | 端末 (MAUI、テンプレートの `Template.MobileApp` 相当) |
| `Pos.Contract` | 通信データ |
| `Pos.Domain` | ドメインロジック |
| `Pos.Domain.Tests` / `Pos.Server.UnitTests` / `Pos.Server.IntegrationTests` | テスト |

名前空間も同じ。  
`Pos.Terminal` の名称 (端末) は `Pos.Register` などに変えてもよい。

### D-27. 用語: DTO は使わない

通信データは `XxxRequest` / `XxxResponse` と呼び、「DTO」という語は文書・アセンブリ名・名前空間・クラス名のいずれにも使わない (利用者指示)。  
一覧は `XxxResponse` でその要素は `XxxResponseItem`、入れ子の要素は `TransactionResponseItemLine` / `TransactionCreateRequestLine` のように親の名前に要素名を続ける ([D-47](#d-47-通信データと名前空間の命名))。

### D-23. 端末の画面骨格: `template-maui` のシェル準拠

| 案 | 内容 |
| --- | --- |
| A. 一般的なスマホ UI (ハンバーガー、ボトムシート、FAB) | 当初の画面設計 v0.1 |
| ✅ **B. `template-maui` の独自シェル: 上部タイトル + 下部 F1〜F4 ファンクションキー、`ContentView` を `ViewId` で遷移、ダイアログは MauiComponents のポップアップ** | テンプレートの資産 (シェル、`AppViewModelBase`、`InputNumber` テンキー、`IDialog`) をそのまま使える。業務用ハンディ端末に近い操作体系で POS に向く |

**決定**: ✅ **B**。  
各画面は F1〜F4 の割り当てを持つ (画面一覧に列を追加)。  
ボトムシートは使わず、明細編集・値引・数量入力はポップアップ (→ [D-50](#d-50-端末のポップアップは下端に寄せたシート) で下端に寄せたシートに変更)。  
数値入力はテンプレートの `InputNumber` ポップアップ (テンキー) を金額・数量に流用する。

### D-24. 端末セットアップ QR: テンプレート互換フォーマット

`template-maui-server` の `/qr` が出す `Key=Value` 行形式 (`SettingParser` 互換) をそのまま使い、POS 用に `StoreId` / `TerminalId` を追加する。

```
ApiEndPoint=http://server:8080/
StoreId=0192...-guid
TerminalId=0192...-guid
```

### D-25. 日時と列挙型の SQLite 保存形式

| 項目 | サーバ DB | 端末ローカル DB |
| --- | --- | --- |
| 日時 | TEXT (`yyyy-MM-dd HH:mm:ss.fffffff`、UTC。`Microsoft.Data.Sqlite` の既定書式) — サーバ系テンプレートの `CreatedAt TEXT` と同じ | INTEGER (UTC ticks) + `[TypeHandler(typeof(DateTimeTicksConverter))]` — `template-maui` と同じ |
| 営業日 (date) | TEXT (`yyyy-MM-dd`) | 同左 |
| 列挙型 | TEXT (列挙名) + 汎用 `[TypeHandler]` コンバータ (`EnumTextConverter<T>`)。DB を直接見たときに読める | 同左 |
| 真偽値 | INTEGER (0 / 1) | 同左 |

**意図**: それぞれのテンプレートの流儀に合わせる。  
JSON 契約は両者とも ISO 8601 (UTC) なので、境界での変換はテンプレートのコンバータで済む。

---

## 4. 実装前に確認した事項

### D-28. UI の言語: 日本語固定

端末・管理画面とも日本語固定 (利用者確認済み)。  
多言語化 (resx) はしない。  
テンプレートの MAUI は英語ラベルだが、POS 画面はすべて日本語で作る。

### D-29. 実装順序

[implementation-plan.md](implementation-plan.md) の順 (土台 → `Pos.Domain` → `Pos.Contract` → サーバ DB → サーバ API → 管理画面 → 端末) で進める (利用者確認済み)。  
サーバを先に通してから端末に入る。

### D-30. 初期データの規模

[architecture.md §6](architecture.md#6-初期データ) のとおり (商品は大分類ごとに 10 件程度、利用者確認済み)。  
他店在庫照会を見せるため店舗は 2 つにする (設計者判断)。

### D-31. 設計ドキュメントの扱い

設計は `docs/` に置き、このブランチにコミットする (利用者確認済み)。  
実装中に判断が変わった場合は本書 (decisions.md) に追記し、該当文書を更新する。

### D-32. リポジトリ構成: モノレポ + 2 ソリューション

**背景**: 参考テンプレートは端末 (`template-maui`) とサーバ (`template-maui-server`) が別リポジトリだが、本サンプルは 1 つのリポジトリに端末とサーバを同居させる (利用者指示)。  
一方で端末とサーバは Visual Studio で個別に動かしたい。

| 案 | 内容 |
| --- | --- |
| A. 2 リポジトリ (テンプレート流儀、共有はサーバ側を相対参照) | テンプレートと同じ分け方だが、共有プロジェクトの参照が兄弟リポジトリ前提になる |
| B. 1 リポジトリ・1 ソリューション | 全部を 1 つの `.slnx` に入れる。VS で端末とサーバを別々に扱いにくい |
| ✅ **C. 1 リポジトリ・2 ソリューション** (`server/Pos.Server.slnx`、`terminal/Pos.Terminal.slnx`、共有は `shared/` を両方に含める) | 利用者指示 (モノレポ、個別起動) を両方満たす。各フォルダはテンプレートと同じ形になる |

**決定**: ✅ **C**。  
共通のビルド設定 (`.editorconfig` / `Directory.Build.props` / `Analyzers.ruleset` / `.gitattributes`) はテンプレート間で同一なのでルートに 1 セット置く。  
設計ドキュメントは本リポジトリの `docs/` に置く。  
詳細は [architecture.md §2](architecture.md#2-プロジェクト構成)。

### D-33. 実装の進め方: フェーズ単位のチェックリスト

[implementation-plan.md](implementation-plan.md) にフェーズごとの `- [ ]` チェックリストと完了条件を置き、フェーズ単位で着手・完了報告する (利用者指示)。  
完了した項目はチェックを付け、設計の変更があれば本書と該当文書を更新してからフェーズを閉じる。

---

## 5. 実装中の判断

### D-34. 参考プロジェクトの差し替え (Phase 0)

**背景**: 土台作りの着手時に、参考にするプロジェクトが利用者から指定し直された。

| 対象 | 変更前 (§3) | 変更後 |
| --- | --- | --- |
| サーバ | `template-maui-server` | **`D:\GitHubService\Service-CloudManager`** (`CloudManager.Core` / `CloudManager.Host`)。Aspire AppHost と OpenAPI (NSwag UI) は含まれないので **`template-blazor-server`** から加える |
| 端末 | `template-maui` | **`D:\GitHubTemplate\template-maui-keyboard`** (`Template.MobileApp`)。不要なフォント (OpenSans / FluentUI、未使用の参照) と `dotnet_bot.png` は削除し、`MaterialIcons` だけ残す。色は同テンプレートの `Colors.xaml` を使い、販売・会計画面のデザインは `template-maui` の POS 画面 (`UIPosView`) に倣う |

**反映**:

- サーバのホストプロジェクト名は `Pos.Server.Web` ではなく **`Pos.Server.Host`** (`CloudManager.Host` / `Template.BlazorServer.Host` に倣う)。  
  D-26 の表は読み替える
- `Service-CloudManager` には認証・OpenTelemetry・FeatureManagement・レート制限がない。  
  MVP はそのまま (認証なし、[D-09](#d-09-認証端末登録-後回し))。  
  JSON は最初から camelCase (`NamingPolicy`)
- `template-maui-keyboard` の Input (物理キー・ショートカット) と Behaviors は残す。  
  Bluetooth バーコードスキャナ (HID キーボード) の入力にも使える
- 端末の POS 用スタイルは `Resources/Styles/Styles.xaml` の「POS」節に `Pos` 接頭辞で追加した (背景・行・区切り線・名称 / 金額ラベル・オプションボタン・実行ボタン)

### D-35. テストの実行方法

テストプロジェクトは Microsoft.Testing.Platform (xunit.v3) の実行ファイルなので、`dotnet test` ではなく **テストプロジェクトごとに `dotnet run --project` で実行する** (参考プロジェクトの Jenkins 設定と同じ)。

```bash
dotnet run --project shared/Pos.Domain.Tests
dotnet run --project server/tests/Pos.Server.UnitTests
dotnet run --project server/tests/Pos.Server.IntegrationTests
```

- .NET 10 SDK の `dotnet test` で Microsoft.Testing.Platform を使うには `global.json` の `test.runner` でのオプトインが必要になるが、この方式なら不要なので `global.json` は置かない
- レポートは `--report-xunit-trx` と `--coverage --coverage-settings CodeCoverage.runsettings` で出力する (Jenkins のパイプラインも同じコマンド。パイプラインは Jenkins 側の設定で、リポジトリには置かない)
- Visual Studio のテストエクスプローラーからも実行できる

### D-36. 端末の画面遷移アニメーション

**背景**: 端末の画面遷移 (`ContentView` の差し替え) にアニメーションがなく、進む / 戻るの感覚がつかみにくい。  
Smart.Navigation.Maui には効果 (`MauiEffect.Forward` = 右からスライド、`MauiEffect.Back` = 左からスライド、`Push` / `Pop` / `Fade`) が用意されている。

| 選択肢 | 内容 | 評価 |
| --- | --- | --- |
| A. 遷移ごとに効果を指定 | `Navigator.ForwardAsync(ViewId.Xxx, new NavigationParameter().WithForwardEffect())` のように呼び出し側で毎回指定 | 指定漏れが起きやすく、メニューへ戻る `ForwardAsync(ViewId.Menu)` を Back にし忘れやすい |
| ✅ **B. 画面の階層で自動決定** | 各画面に `[Hierarchy(n)]` を付け、`HierarchyEffectPlugin` (Usa.Smart.Navigation 3.11.0) が階層が深くなる遷移に Forward、浅くなる遷移に Back を付ける | 遷移の呼び方は変えずに済む。`config.AddHierarchyEffectPlugin()` の 1 行と属性だけ |

**反映**:

- 階層は screen-design §1.3 の遷移図の深さ: T-00 = 0、T-01 = 1、ホーム T-02 = 2、ホーム直下 = 3、その下 = 4 …。  
  親が複数ある画面は最も深い親 + 1
- 同じ階層への遷移と起動時の最初の遷移は効果なし。  
  別の効果にしたい遷移は `NavigationParameter` の `WithFadeEffect()` などで明示する (明示した効果が優先)
- Debug ビルドの `Navigated` ログに `effect=[...]` を出して確認できるようにした

### D-37. 帳票出力 (PDF): OysterReport

**背景**: 精算レポートや売上日報は紙で残す・本部へ送る運用があり、管理画面から帳票を出力できる必要がある。

| 案 | 内容 | 評価 |
| --- | --- | --- |
| ✅ **A. OysterReport (Excel テンプレート → PDF)** | `Assets/Reports/*.xlsx` に `{{Placeholder}}` を置き、`TemplateWorkbook` + `OysterReportEngine` で PDF 化。`template-blazor-server` / `GadgetFood.BlazorServer` と同じ構成 (`Infrastructure/Reports/XxxReportBuilder`、同梱の IPAex ゴシックを `EmbeddedFontResolver` で解決) | レイアウトは Excel で調整でき、コードは値の差し込みだけ。参考プロジェクトに実績がある |
| B. ブラウザ印刷 (印刷用 CSS) | 管理画面をそのまま印刷 | サーバで PDF を作れないので、保存・配布・端末からの取得に使えない |
| C. コードでレイアウト (QuestPDF など) | C# でレイアウトを記述 | レイアウト変更のたびにコードを直す |

**決定**: ✅ **A**。

**反映**:

| 帳票 | 内容 | API | 画面 | 優先 |
| --- | --- | --- | --- | --- |
| 精算レポート | シフト 1 件: 店舗・端末・営業日・担当・開設 / 精算時刻、現金 (準備金・現金売上・返金・入出金・予想・実査・過不足)、支払方法別・税率別・部門別、件数、ポイント (`ShiftSummaryResponse` と同じ内容) | `GET /shifts/{id}/summary/pdf` | S-31 | ★ |
| 売上日報 | 店舗 × 営業日: 売上・返品・値引・税・客数・客単価、支払方法別・税率別・部門別・時間帯別、シフト一覧 (端末・担当・過不足) | `GET /reports/sales/daily/pdf?storeId&date` | S-10 | ★ |
| レシート (再発行) | 取引 1 件の控え: 明細・値引・税率別・支払・ポイント・配送先 | `GET /transactions/{id}/receipt/pdf` | S-21 | ◎ |

- `Pos.Server.Host`: パッケージ `OysterReport`、`Assets/Fonts/ipaexg.ttf`、`Assets/Reports/*.xlsx` (`CopyToOutputDirectory`)、`Infrastructure/Reports/` にフォントリゾルバと帳票ごとの `XxxReportBuilder` (シングルトン、`byte[] Build(...)`)。  
  エンドポイントは各リソースのグループに置き、`TypedResults.File(bytes, "application/pdf", ファイル名)` を返す。  
  データがなければ 404 / 400
- 管理画面のボタンは `MudButton Href="api/v1/.../pdf"` (認証は後回しなので直接リンク)
- テンプレートは Excel で作る。  
  1 シート = 1 ページを基本にし、明細行はプレースホルダの行から順に埋める。  
  複数ページ (複数シフトなど) はシートのコピーで作る (`GadgetFood` の給与明細と同じ)
- 端末のレシート (T-22) は画面表示 + 電子レシート QR + 画像共有のままで、サーバの PDF は使わない (オフラインでも出せるように)

### D-38. 警告の抑止

`Pos.Contract` に型を置くと、名前空間の `Shared` が VB の予約語のため CA1716 が全ファイルで出る。  
`ProductResponse.ImageUrl` (string) には CA1056 が出る。

| 案 | 内容 |
| --- | --- |
| A. `Analyzers.ruleset` で Hidden | 他の `.Shared` プロジェクトと同じだが、全プロジェクトに効く |
| ✅ **B. `Pos.Contract` の `GlobalSuppressions.cs` でアセンブリ単位に抑止** | 影響を `Pos.Contract` に限定する |
| C. 名前空間を `Pos.Contracts.*` に変える | プロジェクト名と名前空間が食い違う |

**決定**: ✅ **B** (利用者確認済み)。

`Pos.Server.Core` の CA1000 (汎用 `EnumTextConverter<T>` の static メンバー。`IValueConverter` の static abstract 実装なので回避できない)、CA1056 (`ProductEntity.ImageUrl`)、CA1819 (`StaffEntity.PinHash`) は該当箇所だけ `#pragma warning disable / restore` で抑止する (利用者確認済み)。

### D-39. 管理画面の表現 (絵文字・チップ・バッジ)

管理画面は文字だけの表では状態が読み取りにくい。  
ナビ (screen-design §2.2) には絵文字を割り当ててある。

| 案 | 内容 |
| --- | --- |
| A. 文字と色だけ | MudBlazor の既定。状態の違いが弱い |
| ✅ **B. 絵文字 + チップ + バッジ** | 見出しに絵文字、状態は `MudChip` (絵文字付きの文言 + 色)、件数は `MudBadge` / タブの `BadgeData`、KPI はアイコン付きカード |
| C. 独自アイコン | 画像の管理が増える |

**決定**: ✅ **B** (利用者指示)。

- 文言と色は `Application/ViewHelper.cs` に集約し、`Controls/StatusChip` で表示する。  
  列挙型の日本語名・金額・日時の書式は `Application/ViewExtensions.cs` の拡張メソッド ([D-45](#d-45-サーバの-service-層))
- 端末側 (MAUI) は対象外。  
  レシートや帳票 (PDF) にも絵文字は使わない (フォントに依存するため)

### D-40. 端末の通信: Rester ではなく HttpClient

テンプレート (`template-maui`) の `HttpService` は Rester を使うが、Rester は 4xx / 5xx の応答本文 (Problem Details) を呼び出し側に返さない。  
端末は `409` / `422` の `errorCode` で Outbox の「要確認」を判定し、利用者に理由 (`PRICE_OVERRIDE_NOT_ALLOWED` など) を見せる必要がある。

| 案 | 内容 |
| --- | --- |
| A. Rester のまま | テンプレートどおり。エラー本文が読めず、要確認の理由を出せない |
| ✅ **B. HttpClient + System.Text.Json** | `IHttpClientFactory` の名前付きクライアント、`HttpService.JsonOptions` (Web 既定 = camelCase、null 省略、列挙型は文字列、`JsonDateTimeConverter`)。失敗時は Problem Details を `ApiResult<T>.Problem` に読み込む |
| C. Rester + 独自ハンドラでエラー本文を横取り | 二重管理になる |

**決定**: ✅ **B**。

- `ApiResult<T>` は `Status` (Success / HttpError / Unavailable / Canceled)、`StatusCode`、`Content`、`Problem`、`ErrorCode` を持つ。  
  `IsRejected` (4xx) を Outbox の Failed 判定に使う
- 接続確認・インジケータ・エラー通知は `NetworkService.ExecuteAsync` に集約する (オンライン限定の操作で使う)
- 端末の JSON 設定はサーバ (`ConfigureHttpJsonOptions`) と同じ (architecture §4.2)

### D-41. サンプル取引の生成: API 経由のコンソールツール

レポートやダッシュボードの確認には数日分の取引・シフトが要るが、端末から手で作るのは時間がかかる。  
初期データ (architecture §6) には入れない方針 ([D-30](#d-30-初期データの規模)) なので、別の手段が要る。

| 案 | 内容 |
| --- | --- |
| A. 起動時の初期データに取引も入れる | 営業日が固定になり、集計の検証やレシート番号の連番と噛み合わない。取引の登録処理 (在庫・ポイント・連番) を二重に持つことになる |
| B. SQL で直接 INSERT する | 副作用 (在庫変動・ポイント履歴・`lastReceiptSeq`) を自前で再現する必要があり、サーバの検証も通らない |
| ✅ **C. API を呼ぶコンソールツール** | `server/tools/Pos.Server.SampleData`。`Pos.Domain` で計算した `TransactionCreateRequest` を `POST /transactions` に送る (端末と同じ経路)。サーバの検証・副作用をそのまま使える |

**決定**: ✅ **C**。

- 対象は起動中のサーバ (`--base`、既定 `http://localhost:8080/`)。  
  有効な店舗 × 端末ごとに直近 `--days` 日分 (既定 7) を、開設 → 販売 → 返品 → 取消 → 出金 → 精算の順に登録する。  
  開設中のシフトがある端末は省略する
- 初日の開店前に物品の在庫を調整 (`POST /inventory/changes`、`Adjustment`「サンプル入荷」) で積み、販売で在庫がマイナスになりすぎないようにする (少数のマイナス在庫は残り、要確認の表示確認に使える)
- 乱数は `--seed` で固定し、同じ引数なら同じ内容になる (ID と時刻は除く)。  
  サーバに拒否された取引 (409 / 422) は省略して続行する
- `Pos.Server.slnx` の `/Tools/` に含める。  
  `Pos.Domain` / `Pos.Contract` だけを参照し、`Pos.Server.Core` / `Host` には依存しない

### D-42. 後回し項目の実装順序 (Phase 8 以降)

MVP (Phase 0〜7) の完了後、後回しにしていた項目 (認証・端末登録、日次締め、受注、商品画像・CSV 取込、レシート・帳票・検索、通知、在庫移動・入荷) をどの順で進めるか。

| 案 | 内容 |
| --- | --- |
| ✅ **A. 認証を先に、その後は業務機能を小さい順に** | 認証・端末登録 → 日次締め → 受注 → 商品画像・CSV 取込 → レシート・帳票・検索 → 通知 → 在庫移動・入荷 |
| B. 業務機能を先に、認証は最後 | 認証なしのまま機能を足せるが、後から全 endpoint と画面に認可を入れ直すことになり、テストも二度手間 |
| C. 画面の見栄え (画像・印刷) を先に | デモ映えはするが、業務の穴 (締め・受注) が残る |

**決定**: ✅ **A** (利用者指示で再計画)。

- 認証は以降のすべての API 追加に認可が絡むため最初にする。  
  テストと SampleData ツールの認証対応もここで済ませる
- 日次締めは管理画面だけで規模が小さく、精算 (完了済み) の上に乗る。  
  受注は端末と管理画面の両方に跨るので次
- 通知 (SignalR) と在庫移動・入荷は他の機能に依存されないので後ろに置く
- 各フェーズの詳細設計 (api / db / screen の該当節と `D-4x`) はフェーズ着手時に行う。  
  計画は [implementation-plan.md](implementation-plan.md#phase-8-以降-後回し項目の計画)
- 引き続き対象外: 外部向け Webhook、Bluetooth レシートプリンタ、受注の前受金、発注、管理画面の MFA / パスキー

### D-43. 端末シェルのデザイン: POS 画面に合わせる

テンプレート (`template-maui-keyboard`) のシェルは、タイトルを中央寄せの大きな文字で出し、F キーは赤 / 藍 / 緑 / 橙のアクセント色、ホームは青いタイルだった。  
販売・会計画面 (紺のヘッダ、白い行、青い金額) と並べると浮いて見える (利用者指摘)。

| 案 | 内容 |
| --- | --- |
| A. テンプレートのまま | 他のテンプレートアプリと同じ見た目だが POS 画面と噛み合わない |
| ✅ **B. POS 画面の配色に寄せる** | タイトル左寄せ (右に担当とバッジがあるため中央だと位置が中途半端)、F キーは紺のバーに 灰青 / 青 / 青 / 橙、ホームは白の平らなキーを罫線で区切る (角丸のカードは避ける)、ポップアップの下段ボタンも同じ配色 |
| C. 独自のアイコンやテーマを作る | 画像資産が増える |

**決定**: ✅ **B** (利用者指示)。

- `Styles.xaml` の `HeaderTitleLabel` / `FunctionGrid` / `FunctionButton1〜4` / `MenuGrid` / `MenuButton` / `MenuPrimaryButton` / `InputCancelButton` / `InputConfirmButton` / `InputDeleteButton`。  
  F1 = 戻る・メニュー、F4 = 会計・確定・開設・精算 という割り当てに色を合わせる
- 会計画面の支払方法ボタンは横に長くなる名称 (クレジットカード) があるため、支払方法マスタに `shortName` (ボタン名、10 文字まで) を足し、端末はそれを表示する (省略時は `name`)。  
  既存 DB には起動時に列を足し、初期データ相当のボタン名を入れる (サーバは `SqlHelper.EnsureColumnAsync`、端末は `SchemaHelper.EnsureColumnAsync`)
- 画面を離れるときに入力欄のフォーカスを外し、ソフトキーボードが次の画面に残らないようにする (`ShellUpdateBehavior`)

### D-44. 入力はキーボードに依存しない (数値・番号は電卓ボタン)

端末はスマートフォンだが、レジ操作の入力はほぼ数値 (数量・金額・枚数) と番号 (会員番号・電話・郵便番号・商品コード・伝票番号) である。  
OS のソフトキーボードは画面の半分を隠し、機種やキーボードアプリで見た目と挙動が変わり、F キーも隠れる (利用者指摘)。

| 案 | 内容 |
| --- | --- |
| A. `Entry` + `Keyboard="Numeric"` (テンプレートどおり) | 実装は楽だがキーボード次第。遷移時に自動でキーボードが出る |
| ✅ **B. 数値・番号は電卓ボタン、文字だけキーボード** | 数値・番号の入力欄はタップで `InputNumber` (電卓) を開くボタンにし、ポップアップの中からも電卓を重ねて開く。検索欄には 🔢 を置く。文字 (名前・住所・備考・検索キーワード) だけ `Entry` |
| C. 全入力を独自キーボードにする | かな入力まで作ることになる |

**決定**: ✅ **B** (利用者指示)。

- `NumberInputModel.KeepLeadingZeros` / `NumberInputParameter.Digits` / `IPopupNavigator.InputDigitsAsync` を足し、番号 (先頭が 0 の電話番号など) も電卓で入力できるようにした
- 適用: P-13 数量・単価、P-15 値引の値、金種別入力の枚数、T-16 / T-62 の電話・郵便番号・生年月日、T-11 の手入力コード、T-20 の伝票番号、T-12 / T-14 の 🔢
- テンプレートの `NavigationFocusPlugin` (遷移先の最初の入力欄に自動フォーカス。キーボード端末向け) は物理キー向けの `Input` 名前空間ごと削除した ([D-46](#d-46-端末の-service--usecase-とナビゲーションのコンテキスト))。  
  画面を離れるときはフォーカスを外し、キーボードを残さない (`ShellUpdateBehavior`)

### D-45. サーバの Service 層

実装後のソース見直し (利用者指摘) で、Endpoints と Blazor ページが Accessor と `IDbProvider` を直接使い、SQL の知識 (LIKE のエスケープ、並び替え列) や重複判定・トランザクションが呼び出し側に散っていた。  
表示用の加工 (チップの文言・色、金額や日時の書式) も `DisplayText` / `ChipText` と razor に分かれていた。

| 案 | 内容 |
| --- | --- |
| A. 2 段のまま (D-19) | 呼び出し側が増えるたびに同じ手順が重複する |
| ✅ **B. Service 層を置く** | SQL は Accessor だけ、業務の手順は `Core/Services` の `XxxService`、Endpoints / ページは入力の検証と表示だけ |

**決定**: ✅ **B** (利用者指示)。

- SQL は Accessor 以外に置かない。  
  パラメータの正規化 (並び替え列・LIKE・既定値) は Service で行う
- Accessor は処理の単位でまとめる (`MasterAccessor` にマスタ 9 種、`ProductAccessor` / `CustomerAccessor` / `TransactionAccessor` / `ShiftAccessor` / `InventoryAccessor` / `ReportAccessor`)。  
  DB の結果は `Models/Views`、Service への入力は `Models/Parameters` に置き、`DataProfile` / `SqlHelper` は `Accessors` に置く
- 重複判定は「読んでから更新」ではなく Service の中で `IDialect.IsDuplicate` と更新件数 0 で判定し、`DataWriteStatus` で返す
- Endpoints は Request → Entity / Parameter (Smart.Mapper の `[Mapper]`) → Service → Response の変換だけを持つ。  
  `Mappers` フォルダは廃止し、Entity ↔ Form の変換はフォームが持つ
- razor 表示用の加工は `ViewHelper` (部品の文言と色) と `ViewExtensions` (書式の拡張メソッド) に集約する
- Host は `Application` (アプリ固有) と `Infrastructure` (アプリに依存しない) に分ける。  
  自前の `DatabaseHealthCheck` は置かない
- Service は BunnyTail.ServiceRegistration で `AddCoreServices()` に一括登録する

### D-46. 端末の Service / Usecase とナビゲーションのコンテキスト

同じ見直しで、端末の ViewModel が `IDbProvider` を使ってトランザクションを書き、通信 → DB → 完了メッセージの手順や色・文言の切り替えを持っていた。  
`SalesState` / `StockState` は特定の機能の画面間でしか使わないのに Singleton の State だった。

| 案 | 内容 |
| --- | --- |
| A. ViewModel に手順を書く (従来) | 画面ごとに同じ手順が重複し、ViewModel が DB とサーバの両方を知る |
| ✅ **B. Service / Usecase に切り出し、共有状態はナビゲーションのコンテキストにする** | ViewModel は検証済みの入力を渡すだけ。機能内の共有状態は遷移パラメータで次の画面へ渡す |

**決定**: ✅ **B** (利用者指示)。

- `Services/` は単機能を `XxxService`、複合機能を `XxxUsecase`、組み立てを `XxxBuilder` と呼ぶ (`HttpService` / `DataAccessor` / `NetworkService` はそのまま)。  
  `TransactionUsecase` (保存・取消・履歴)、`SalesUsecase`、`ReturnUsecase`、`ShiftUsecase`、`StockUsecase`、`SetupUsecase`、`ReceiptService`、`DatabaseService`、`SyncService`
- ViewModel は `IDbProvider` を使わず、フィールドは Component → State → Service の順に並べる
- 特定の機能の画面間でだけ共有する状態は `SalesContext` / `ReturnContext` / `StockContext` として `Parameters.WithContext` で渡す (→ [D-49](#d-49-端末の見直し-scope-プラグイン入力の種類ごとの電卓ヘルパーの置き場所) で Scope プラグインに変更)。  
  使用者に常に紐付く情報 (店舗・端末・担当・シフト) は `Session` に集約する
- `DataAccessor` はローカルのエンティティ (`[Key]` あり) のキー取得・削除に `[SelectSingle]` / `[Delete]` を使い、1 文だけの書き込みにはトランザクションを使わない。  
  SQL は `SELECT` / `FROM` / `WHERE` / `ORDER BY` を行頭に置き、列と条件を字下げする書き方に揃える
- ナビゲーションイベントの中の遷移と非同期処理は `PostForwardAsync` / `PostActionAsync` で後回しにする
- 色・列挙型の文言・選択マーク・画面固有の文言 (棚卸の表題など) は ViewModel ではなく Converter (Smart.Maui の `BoolToColorConverter` / `MapToColorConverter` / `BoolToTextConverter` と `DisplayNameConverter`) と Trigger で扱う
- 物理キーボードは前提にしないので `Input` 名前空間 (ショートカット・フォーカス制御) と `KeyInputDriver` を削除した。  
  根の画面の戻るは ViewModel が `HandlesBack = false` で宣言し、`MainActivity` がタスクを背面へ回す (ViewModel は遷移だけを行う)
- `Helpers` にはアプリに依存しない処理だけを置く (`DataProfile` は `Services`、`AppDialogExtensions` は `Extensions.cs` へ。表示用の `DisplayText` は [D-49](#d-49-端末の見直し-scope-プラグイン入力の種類ごとの電卓ヘルパーの置き場所) で `Modules/Helpers/ViewHelper` に変更)。  
  日付の書式は `DateTimeHelper` に集約し、`Trim` のような補助は拡張メソッドにする
- `Models/Sales` は `Models/Cart` (`SalesCart` ...)、共通ダイアログは `Modules/Dialogs`、一覧は `ObservableCollection<T>`、`Show...` はダイアログを出すメソッドだけに使う

### D-47. 通信データと名前空間の命名

| 項目 | 決定 |
| --- | --- |
| 共有プロジェクト | `Pos.Shared` は `Pos.Contract` に改名 (通信データの契約であることを名前で示す) |
| 一覧と要素 | 一覧は `XxxResponse`、その要素は `XxxResponseItem` (`DiscountResponse` の `Items` は `DiscountResponseItem`)。`ListResponse<T>` は `Pos.Contract` 直下 |
| 契約でないもの | `JsonDateTimeConverter` と `ProblemResponse` はサーバと端末がそれぞれ持つ |
| 列挙型 | `Pos.Domain.Enums` に 1 型 1 ファイル |
| ロジック | 業務でまとめず `Pos.Domain.Logic` に `SalesLogic` / `ReturnLogic` / `TaxLogic` / `TransactionLogic` のようにまとめる。エラーの扱い (文言) はコアドメインではないので、Domain は `RuleReason` だけを返す |
| ソースの注釈 | ソースが正。ソースから設計文書 (節番号・`§`・画面 ID・決定番号) を参照しない |

### D-48. サーバの見直し: 並び順の列挙型、RETURNING、初期データの SQL

2 回目のレビューで、サーバの Core / Host に次の指摘を受けた。

| 指摘 | 決定 |
| --- | --- |
| 並び替え列の検証 (`SqlHelper.NormalizeSort`) と差分同期の並び (`SyncSort`) が C# 側にある | 並び順はリソースごとの列挙型 (`Models/Enums` の `StoreSort` など。列挙名 = 列名、先頭が既定) で受け取り、2-way SQL の中で `/*# sort */` と `/*% if (desc) */` / `updatedSince` の分岐で展開する。`SqlHelper` は 2-way SQL の `/*# */` から呼ぶ断片だけにし、列の追加は `SchemaHelper`、タイムゾーンの修飾子は `ReportService` の private に移す |
| `SelectSingle` などの Builder 属性で `Table` を個別に指定している | Entity クラスの `[Name("Stores")]` でテーブル名を持つ (Smart.Data.Accessor 3.0.0-beta11) |
| 生 SQL のプレースホルダに `.ToString()` を付けている (3.0.0-beta11 は `?.ToString()` で展開するため null 許容でない列挙型を直接書けなかった) | 3.0.0-beta12 (`StringBuilder.Append` で展開) に上げ、`/*# sort */` と書く |
| 更新後に `QueryAsync` で読み直している (読み直す間に削除される余地) | `UPDATE ... RETURNING *` を `[QueryFirst]` で受け、`DataWriteResult<T>` (Status + 更新後の行) で返す |
| `InitialData` がクラスで初期データを組み立てている | 外部の SQL ファイル (Host の `Assets/Data/InitialData.sql`。複数の `INSERT`、`@now` は投入時刻) を起動時に読み、`GenericAccessor.ExecuteScriptAsync` (`[DirectSql]` + `[Execute]`。第 1 引数の文字列が SQL、残りの引数がパラメータ) で投入する。`InitialData` は固定 ID だけを持つ。`DatabaseAccessor` は `GenericAccessor` に改名 |
| `Models/Views` の名前が不揃い | `XxxView` に統一 (`SalesSummaryView` / `TransactionDetailView` など) |
| Views / Parameters に列挙型が混ざる、Service の結果型がファイルに分かれている | 列挙型は `Models/Enums`、特定の Service だけの結果 (`TransactionResult` など) はその Service のファイルの先頭で定義 |
| `ArgumentNullException.ThrowIfNull` | 書かない (CA1062 は無効) |
| Host の置き場所 (`Application` 直下の雑多なクラス、`Infrastructure/Components`、`Application/Reports`、`RuleText`、`CsvExport` の名前空間) | `Components/` (PageComponentBase / AppComponentBase)、`Components/Dialogs/` (EditDialogBase / DialogServiceExtensions)、`Application/Lookup` / `State` / `Urls`、`Reports/` (Endpoints と同階層)、`Endpoints/ApiRuleText`、`Infrastructure/Csv` / `Logging`、`JsonDateTimeConverter` は Core の `Infrastructure/Json` |
| Endpoints の静的 `TryParse`、`ReportEndpoints` の大小比較 | 列挙値の解析は `Helpers/EnumHelper`。レポートの期間は `[AsParameters] ReportPeriodQuery` の `IValidatableObject` で `from > to` を 400 にする (API の入力検証は DataAnnotations で統一し、FluentValidation は管理画面のフォームだけ)。省略時の既定 (`ResolvePeriod`) は `to` を今日 (`from` が未来ならその日) にして逆転しない |
| 1 つのページだけのフォーム、CSV / PDF の URL | `SettingsForm` は `SettingsPage` の内部クラス (他のフォームはダイアログと呼び出し元ページの 2 か所で使うので `Models/Forms` のまま)。URL は `Application/Urls/ExportUrls` |
| `PageRenderMode` を常に対話型にできないか | できない。エラーと 404 のページは例外や 404 の再実行 (回線なし) で描画されるので静的 SSR が必要。プリレンダリングもないため、常に対話型にすると空の HTML になる |
| 文字列の長さが各所に数値で散らばる | `Pos.Domain.Length` の定数に集約し、Contract の `MaxLength`、フォームの `MaximumLength`、端末の電卓の桁数で使う |
| Request / Response の名前 | エンドポイントのクラス名 + メソッド名 (`TransactionCreateRequest`、`CustomerPointHistoryResponse`、`ShiftCashEventRequest`、`ReportSalesSummaryResponse` など)。一覧 `XxxResponse` / 要素 `XxxResponseItem` は維持。`AdjustmentReason` は独自の名前空間とエンドポイント |
| `TransactionResponseItem` の拡張メソッド | 型と同じファイルに置く (`IsReturnable` / `HasReturnableLine`) |
| Accessor の `VoidAsync` / `CloseAsync` (業務の動詞) | Accessor は DB の操作で名付ける。取消は `UpdateVoidedAsync`、精算は `UpdateClosedAsync` (端末は `UpdateShiftClosedAsync`、サーバに残ったシフトの登録は `InsertServerShiftAsync`)。Void / Close / Import のような業務の動詞は Service / Usecase の名前にだけ使う |

SQL は `UPDATE` / `SET` / `WHERE` などの句を行頭に置き、表名・列・条件を次の行に字下げする書き方に統一した (サーバ・端末とも)。  
1 行書きを禁止はせず長さで判断する。  
短い 2 条件の `OR` (`StoreId = … OR StoreId IS NULL`) は 1 行のまま、キーワード検索の `LIKE` を 4〜5 個並べる条件は `AND (` の中で 1 条件 1 行にする。  
精算の集計 (`ShiftAccessor.QuerySummaryAsync`) は長いスカラー副問い合わせ 9 つを、表ごと (取引・支払・入出金) の派生表 3 つの `CROSS JOIN` に書き直した (各表を 1 回ずつ読む)。

### D-49. 端末の見直し: Scope プラグイン、入力の種類ごとの電卓、ヘルパーの置き場所

| 指摘 | 決定 |
| --- | --- |
| 機能の画面間で持ち回るコンテキストを遷移パラメータで渡している | Smart.Navigation の Scope プラグインを使う。`SalesContext` / `ReturnContext` / `StockContext` / `CustomerDraft` を DI に transient 登録し、ViewModel の `[Scope]` プロパティ (同じ名前) に注入する。どの画面からも参照されなくなると破棄されるので、会計完了やメニューへ戻ると新しいカートになる。スキャンは途中の画面なので各コンテキストのプロパティを持ち、呼び出し元の状態を保持する |
| `DisplayText` / `RuleText` が `Models` にある | 表示用なので `Modules/Helpers/ViewHelper` (業務ルールの文言も `ViewHelper.Reason` / `Warning`) |
| 単一値と比較するだけの拡張メソッド (`IsVoided` など) | 削除して比較式にする。`IsReturnable` のように意味でまとめるものは Contract の型と同じファイル |
| Service と Usecase が同じフォルダ | `Services/` (単機能) と `Usecases/` (複合。`TransactionMapper` / `ShiftSummaryCalculator` も) に分ける。`Builder` はテキスト・画像の組み立てだけに使う |
| 電卓入力の表題・桁数を画面ごとに指定している | `PopupNavigatorExtensions` に入力の種類ごとのメソッド (`InputPhoneAsync` / `InputQuantityAsync` など) を置き、桁数は `Pos.Domain.Length` |
| `DiscountChooser` が `Modules` 直下にある | 削除し、明細値引も取引値引と同じ `DiscountView` のポップアップを使う |
| LIKE のエスケープが ViewModel にある、`Where(...).ToList()` で削除している | `Helpers/Data/SqlHelper.ToLikePattern`、削除は後ろから `RemoveAt` |
| 画面固有の Converter が `Modules` にある、`EmptyText` の名前 | Converter は `Converters/` にまとめる。文言が入るプロパティは `Message` |
| キーボードに依存しないか | 棚卸のコードは電卓かスキャン、返品のレシート番号は端末番号 + 連番の電卓入力 (自店)、取消 / 入出金 / 値引の理由は定型の選択 (`ReasonSelect`)。キーボードは会員・配送先の文字項目、検索、設定に限る |

### D-50. 端末のポップアップは下端に寄せたシート

[D-23](#d-23-端末の画面骨格-template-maui-のシェル準拠) でボトムシートは使わず中央のポップアップにしていたが、片手で持つスマートフォンでは電卓や一覧が画面中央にあると親指が届きにくく、会計画面の埋め込みテンキー (下端) とも位置が揃わない。  
一覧からの選択 (操作メニュー・絞り込み・承認者) は OS の `AlertDialog` で、文字が小さくアプリの配色とも合っていなかった。

| 案 | 内容 |
| --- | --- |
| ✅ **A. CommunityToolkit の Popup を下端に寄せる** | `DefaultPopupSettings` で `VerticalOptions = End` / `HorizontalOptions = Fill` / `Margin = 0`、`PopupOptions.Shape` で上角を丸める。追加の依存がなく、`DialogId` / `IPopupNavigator` の仕組みがそのまま使え、ポップアップ同士を重ねられる (明細編集 → 値引 → 電卓) |
| B. Syncfusion `SfBottomSheet` | ドラッグや半開きの状態を持つが、ページ内のコントロールなので Popup の上には出せず重ねられない。`DialogId` とは別系統になる |

**決定**: ✅ **A** (利用者指示)。

- すべての `DialogId` のポップアップ (電卓・明細編集・値引・理由・金種別入力) を下端のシートにし、幅の指定 (`ScreenSize.LargeDialogWidth`) はやめる
- 一覧からの選択は `Select` シート (`IPopupNavigator.ChooseAsync`) にし、`IDialog.SelectAsync` は使わない。  
  `Select` だけは外側のタップでも閉じる (電卓などは誤操作を防ぐため閉じない)
- `PopupOptions.Shape` は要素なので、`PopupNavigatorConfig.OptionFactory` で表示のたびに作る
- 理由の任意入力はキーボードの Enter でも確定する (キーボードが出ている間はシートの下段ボタンが隠れるため)

### D-51. 現在時刻を扱うのは Service だけ

管理画面のページが `TimeProvider` を注入し、期間の既定 (今日から 30 日)、端末の通信中の判定 (5 分以内)、在庫調整の実施時刻を自分で計算していた (利用者指摘)。  
これらは業務の規則であり、`ReportService.ResolvePeriod` と同じ規則がページにも重複していた。

**決定**: `TimeProvider` を扱うのは Service と帳票 (`Reports/`) だけにし、ページとエンドポイントは時計を持たない。

- 今日と既定の期間は `ReportService.Today` / `ResolvePeriod`
- 端末の通信中は `TerminalService.IsOnline(lastSeenAt)` で判定し、`ViewHelper.OnlineChip` は結果を受け取るだけ
- 在庫調整の `OccurredAt` は省略可にし、省略時は Service が登録時刻を入れる (端末は実施時刻を送る)
- `PageComponentBase` の `TimeProvider` / `UtcNow` は削除

### D-52. 文書は人間向け、規則は AI 向け (`AGENTS.md` と `.claude/rules/`)

`AGENTS.md` にレビューの規則を足し続けた結果、1 行が 800 文字を超える箇条書きになり、人にもエージェントにも読みにくくなっていた。  
レビューの知見を `docs/guidelines.md` にもまとめていたため、同じ規則が 2 か所にあった (利用者指摘)。

**決定**: `docs/` は人間向けの設計文書として現状 (何を・なぜ) だけを書き、どう実装すべきかの規則は AI 向けに `AGENTS.md` と `.claude/rules/` に置く (AI がコードを書く前提)。

- `AGENTS.md`: 仕事の進め方 (コーディングスタイル・検証・進め方)。  
  `CLAUDE.md` が取り込む。  
  構成と層のようなコードの規則は書かない
- `.claude/rules/`: コードの書き方を領域別に。  
  `common.md` は全体と共有プロジェクト (`Pos.Domain` / `Pos.Contract`) の規則で常時読み込み、`server.md` / `terminal.md` / `sql.md` / `tests.md` / `docs.md` は `paths` フロントマターで対象のファイルを扱うときだけ読み込む
- `sql.md` は SQL ファイルの書き方だけにし、Accessor の C# 側の規則 (`[Name]`、`RETURNING` の受け取り、コンバータの登録) は `server.md` / `terminal.md` に置く
- `docs/guidelines.md` は規則に統合して削除し、設計文書にあった実装規則 (命名・null チェック・引数の渡し方など) も規則へ移す
- 分割の目安: 常時読み込む規則が 100〜150 行を超えたとき、規則の半分以上が特定の領域にしか当てはまらないとき、ファイル種別に固有の規則 (SQL の書き方など) があるとき
- 規則も日本語で書く。  
  規則ファイルは 1 項目 1 行 (文書の「。」+ 2 スペースの改行規則は適用しない)

### D-53. DDL と固定 ID を製品コードから外す

| 指摘 | 決定 |
| --- | --- |
| DDL が Accessor ごとの 2-way SQL (`{Accessor}.Create.sql`) に埋め込まれている | 初期データと同じ外部の SQL ファイルにする。サーバは `Host/Assets/Data/Schema.sql`、端末は `Resources/Raw/Schema.sql` (MauiAsset)。起動時に読んで `ExecuteSchemaAsync` (`[DirectSql]`) で実行する (`CREATE TABLE IF NOT EXISTS` なので何度実行してもよい) |
| 初期データの固定 ID を持つ `InitialData` クラスが Core にある | テストしか使わないので削除し、必要な定数はテスト側 (`TestData`) に定義する |
| 複数件を返す Accessor のメソッド名が `QueryLines` / `QueryPayments` のように揺れている | `QueryXxxList` に統一し、絶対値の設定や既定の解除も `UpdateXxx` にする (`UpdateInventoryQuantity`、`UpdateTaxRateDefaultCleared`)。名前の規則は `.claude/rules/sql.md` の表 |

### D-54. 規則は「追加・変更のときに手が入る項目」だけ

規則ファイルの見直しで、一度決めたら変わらない要素 (モノレポの構成、DDL や初期データを読み込む仕組み、シェルの配色など) が規則に混ざっていた (利用者指摘)。

**決定**: `AGENTS.md` と `.claude/rules/` には、処理を追加・変更するときに判断が要る項目だけを書く。  
変わらない要素は設計文書 (`docs/`) に書く。

- Service / Usecase / ViewModel / Endpoint の役割はそれぞれの領域の規則に書き、`common.md` は全体と共有プロジェクトの規則だけにする
- 集計の Accessor メソッドは `QueryXxxSummary` に統一する (`QueryTotals` → `QuerySummary` など 6 件)
- Contract の子要素は `Item` を重ねず親の名前 + 要素名にする (`TransactionResponseItemLine` → `TransactionResponseLine` など 9 型)

### D-55. テンプレートの基盤に揃える (テレメトリ・アクセスログ・ログの文脈・スタイル)

`template-blazor-server` の最新版と比べ、こちらに無かった基盤を取り込んだ (利用者指示。認証・ファイル保管・ワーカー・DB ヘルスチェック・FeatureManagement は POS に用途がないので取り込まない)。

- テレメトリ: OpenTelemetry (ログ・メトリクス・トレース)。  
  OTLP は `OTEL_EXPORTER_OTLP_ENDPOINT` があるときだけ、Prometheus は `Prometheus:Uri` で有効化。  
  独自の計測 (`ApplicationInstrument`: 稼働時間、API の要求数と長時間実行) は `MapApiGroup` のエンドポイントフィルタで配線し、SQL のトレース (`Profiler:SqlTelemetry`) も同じ経路に載せる
- ログ: HTTP 本文のダンプ (`Log:HttpDump`) と W3C アクセスログ (`Log:W3CLog`) を設定で切り替える。  
  接続元アドレスを `LoggingContext` (AsyncLocal) + `CallbackEnricher` で全ログ行に付ける。  
  `Microsoft.AspNetCore.HttpLogging` の Override を足し、`Log:HttpLog` が実際に出るようにした
- 管理画面のスタイル: `wwwroot/css/app.css` のクラスに集約し、要素の `Style=` は列幅だけにする (`text-right`、`min-w-*`、`kpi-card` など。列幅の例外は D-57 で廃止)
- 規則: `tmpl-guide.md` の作業ルールと技術方針のうち、追加・変更のときに判断が要るものを AGENTS.md と rules に取り込んだ (コンストラクタ引数の順序、設定・ログ・計測・マッピングの書き方、テストの AAA、パッケージの固定)

### D-56. ログの文脈はアクセサーから読み、アクセスログは例外ハンドラーの外に置く

テンプレート側 (tmpl-record §4-33) の見直しに合わせた (利用者指示)。

- 接続元アドレスは `LoggingContextMiddleware` で捕捉せず、`CallbackEnricher` がログ出力時点の `IHttpContextAccessor.HttpContext` から読む。  
  `AsyncLocal` は下流にしか流れないため、ミドルウェアで捕捉する方式ではその外側 (例外ハンドラー・HTTP ログ) の行に付かない。  
  アクセサーは要求完了後に null になるので、要求から派生した処理が古い `HttpContext` を読むこともない
- ミドルウェアの順序を ForwardedHeaders → W3CLog → ErrorHandler → UseRouting → Compression → HttpLog → Antiforgery → Endpoints にした (`UseLogging` を `UseW3CLog` / `UseHttpLog` に分割)。  
  HTTP ログと W3C ログが例外ハンドラーの内側にあると、未処理例外の応答を状態 200 (HttpLogging) / 状態なし (W3C) で記録する。  
  運用のアクセスログである W3C は例外ハンドラーの外に置き、HTTP ログは本文ダンプを展開後で読むために圧縮の内 (= 例外ハンドラーの内。未処理例外は 200 と記録される開発用) に置く。  
  例外ハンドラーは標準どおり圧縮の外に残す
- 画面の未処理例外は `/error` ページに出す。  
  `GlobalExceptionHandler` (DI 登録の `IExceptionHandler`) は再実行より先に呼ばれ、画面の例外も ProblemDetails で返していたため、API のパス以外では処理せず (`false`) 再実行に任せる。  
  `UseWhen` 内の `UseExceptionHandler("/error")` の再実行は暗黙のルーティングに乗らないため、`UseErrorHandler` の直後に `UseRouting()` を明示する (`/not-found` を `UseWhen` の外に出したのと同じ理由)

### D-57. テーブルの列幅も幅クラスにする

テンプレート側の方針変更 (利用者指示) に合わせ、D-55 で残していた「要素の `Style=` は列幅だけ」の例外を廃止した。

- 列幅は `w-100` / `w-110` / `w-120` を `app.css` に定義し (`min-w-*` と同じ並び)、`<td class="… w-120">` や `TemplateColumn CellClass="… w-100"` で指定する。  
  要素に `Style=` / `style=` を書く箇所は無くなった

### D-58. 端末の確認・情報もシートにし、空の状態と描画待ちを見せる

デザインの見直し (利用者指示) で、端末と管理画面の細部を揃えた。

- 確認 (`ConfirmAsync` / `AskAsync`) と情報 (`InformationAsync`) は OS のダイアログではなく `Confirm` / `Message` のシートで出す。  
  `IDialog` を実装する `SheetDialog` がこの 2 つだけをシートに変換し、他 (トースト・ローディング・インジケータ) は `DialogImplementation` に委ねる。  
  呼び出し側 (ViewModel) は `IDialog` のまま変えない
- レシート画像の描画 (SkiaSharp) は UI スレッドを塞いで初回表示が空白になるため、`Task.Run` で背景に回し、終わるまでインジケータを出す
- 一覧やスキャン待ちの空の状態は案内文だけでなく絵文字と案内文を中央に出す (`PosEmptyStack`)。  
  会計の支払リストが空のときも案内を出す
- 会員照会・会員登録は遷移時に `Focus()` しない (キーボードが出て一覧を隠す。D-44 の方針に合わせる)
- シリアル番号が未入力なら案内のあとにその明細の編集を開き、取引詳細の明細に S/N を表示する
- アプリアイコンとスプラッシュはテンプレートの画像をやめ、紺の背景に白いレジのグリフにする
- 管理画面: `MudTable` の `FooterContent` は既に `<tr>` の中なので `MudTFootRow` を重ねない (列幅が崩れる)。  
  グラフの描画領域は 650×400 の比率で高さに合わせて拡大されるため、幅は `Height` (360px) で決める。  
  金額の軸は `YAxisFormat`、日別のラベルは月日だけ、13 本以上は 45° 回転。  
  横スクロールする商品一覧の操作列は `StickyRight`、税率は率だけ。  
  名称のように折り返してよい列は `cell-wrap` にし、幅が足りないときだけ折り返して表の横幅を収める (シフト一覧の店舗 / 端末と担当)。  
  商品一覧は列が多く折り返すと崩れるので、折り返さずに横スクロールし、価格の「内税」は省いて外税のときだけ注記する。  
  シフト一覧の開設・精算は年なし (`ToShortDateTimeText`)。  
  期間の入力欄は `filter-range` (最小 260px・最大 320px) で他の入力欄と幅を揃える。  
  会員一覧の状態は有効のチップを出し、端末の最終通信は「通信なし」と時刻にする

### D-59. 端末の画面の表現: 平らな面に状態と動きを足す

テンプレート (`template-maui`) の Card List にならい、状態の見分けやすさと操作の手応えを上げた (利用者指示)。  
これまでの端末は見出しと白い行だけで、種別や送信状態を文字でしか示さず、タップしても反応が見えなかった。

| 案 | 内容 |
| --- | --- |
| A. これまでのまま | 状態が文字でしか分からず、タップの反応も見えない |
| B. 角丸のカードを並べる | Card List のように余白付きの角丸の箱に分け、集計をタイルにする。どの画面も同じ箱の繰り返しになり、AI が生成した画面のように見える (利用者指摘。[D-43](#d-43-端末シェルのデザイン-pos-画面に合わせる) でもホームのカードを避けている) |
| ✅ **C. 平らな面のまま状態と動きを足す** | 灰色の背景の見出しと幅いっぱいの白い面 (角丸・枠・影なし) を保つ。主な金額は白い帯に大きく出し、集計は罫線で区切った表にする。一覧は左端の帯と行の背景で状態を示し、チップ・等幅のコード・アバター・波紋・控えめな動きを足す |

**決定**: ✅ **C** (利用者指示。B で実装したあとに見直した)。

- 共通部品は `Controls/SectionPanel` (見出し + 白い面)、`Controls/StatusChip` (色付きの短い文言)、`PosSectionTemplate` (`SummarySection` を見出しと面で並べる)。  
  角丸はチップ・アバター (円)・ボタン・下端のシートにだけ使う
- 状態のある一覧 (取引履歴・未送信・保留・棚卸・返品明細) は、左端の帯と行の背景で状態を示し、区切り線で並べる。  
  取引履歴は明細と支払を、未送信はエラーを行の中で展開する (行の要素は `NotificationObject` を継承して展開の状態を持つ)
- 動きは `Behaviors/AnimationOption` と `LabelOption.CountUp` に置く。  
  合計・お釣り・純売上の数え上げ、販売の合計が変わったときの背景の光り、要確認があるときの未送信バッジの拡縮、面と一覧が現れるときの浮き上がり (250 ms、40 ms ずつ遅らせる)。  
  販売の明細や棚卸のように続けて更新する一覧には付けない (操作のたびに動いて読みにくい)
- タップの反応は Syncfusion.Maui.Toolkit の `SfEffectsView` (波紋)。  
  影は付けない
- オンラインで取る画面 (売上照会・会員照会) の集計中と取得できないときは、結果の上に案内を重ねて隠す。  
  CommunityToolkit の `StateContainer` は、既定の中身が `CollectionView` のときに状態を戻しても一覧が描かれず、高さが自動の親の中では中身が空になったため使わない
