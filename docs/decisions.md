# 設計判断の記録 (Decision Log)

POS サーバ API / DB 設計にあたって行った判断の記録。各項目は次の形式で残す。

- **背景**: 何を決める必要があったか
- **選択肢**: 検討した案と長所・短所
- **決定**: 採用した案 (✅)
- **意図**: なぜその案にしたか
- **反映**: API / DB 設計への影響

「利用者と確認済み」の判断は §1、設計者が慣例から判断し利用者に確認をとっていないものは §2、参考プロジェクト (既存テンプレート) を確認して合わせた判断は §3、実装前に確認した事項は §4、実装中の判断は §5 に分ける。§2 / §3 / §5 は異論があれば変更してよい。

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

**背景**: 「デジカメ 80,000 円を現金 50,000 円 + クレジット 30,000 円で購入」のような会計をサーバへどう伝えるか。API の本数・状態遷移・オフライン対応のしやすさが大きく変わる。

**選択肢**:

| 案 | 内容 | 長所 | 短所 |
| --- | --- | --- | --- |
| ✅ **A. 一体型** (Loyverse / Lightspeed / スマレジ) | 端末で会計を完了させてから `POST /transactions` で明細 + 値引 + 税 + 支払を 1 回で送る | API 1 本。完了済み取引をキューで送るだけなのでオフラインと相性がよい。実装量が小さい | サーバが「会計途中」を知らない。保留の端末間共有、注文だけ先にキッチンへ、決済端末で先に決済確定 → 取引確定、といった流れは表現できない |
| B. 分離型 (Square / Clover) | `POST /orders` → `POST /payments` → `POST /orders/{id}/complete` の 3 段階。会計途中の状態をサーバが持つ | 決済端末連携や保留の共有、テーブル会計、EC 注文の店頭受取に自然に拡張できる | API と状態遷移が増える。オフライン時は結局端末で溜めて送るので二重管理になる |
| C. 一体型 + 受注 API を最初から | 取引は A のまま、取り寄せ / 取り置き / 配送用の「受注」リソースも MVP に含める | 家電店の取り寄せ業務を最初から見せられる | MVP が大きくなる |

**決定**: ✅ **A. 一体型**。受注 (取り寄せ / 取り置き) は将来、取引とは別の「受注」リソースとして追加する (スマレジも受注管理を別 API にしている)。

**意図**: レジ端末で会計が完結する物販 POS なので、会計途中をサーバが持つ必要がない。オフライン運用を前提にすると、完了した取引を送るだけの A が最も単純で堅い。

**反映**: `POST /transactions` が唯一の取引登録 API。返品も `type = Return` の取引として同じ API で送る。取消は `POST /transactions/{id}/void`。会計途中の「保留」は端末ローカル機能。

---

### D-02. 金額計算の主体: 端末計算 + サーバ検証 vs サーバ計算のみ

**背景**: 小計・値引・税・ポイント・合計を誰が計算するか。オフライン可否と、計算ロジックの置き場所が決まる。

**選択肢**:

| 案 | 内容 | 長所 | 短所 |
| --- | --- | --- | --- |
| ✅ **A. 端末計算 + サーバ検証** | 端末が計算して合計まで含めて送信。サーバは同じロジックで再計算し、一致しなければ 422 で拒否 | オフラインで会計できる。MAUI も ASP.NET Core も .NET なので**計算ロジックを 1 つの C# ライブラリで共有**できる | ロジックの二重実行 (ただしコードは 1 つ) |
| B. サーバ計算のみ | 端末は明細だけ送り `POST /transactions/calculate` で合計を受け取る | ロジックが一箇所。端末が薄くなる | ネットワーク断で会計が止まる。毎回往復が必要 |

**決定**: ✅ **A. 端末計算 + サーバ検証**。

**意図**: オフライン前提 (D-00) と矛盾しないのは A だけ。共有ライブラリにすれば「ロジックが二箇所」にはならない。

**反映**: 計算仕様を [api-design.md §4](api-design.md#4-金額税ポイント計算仕様-共有ライブラリ) に明文化し、`Pos.Domain` ([D-22](#d-22-共有プロジェクト-通信データとドメインロジックは別プロジェクト)) に実装する。`POST /transactions/calculate` は検証・テスト用に残す。

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

**意図**: 家電・カメラ店のサンプルとして「ポイント」「他店在庫」「初期不良の返品」は外せない。レポートは Blazor 管理画面 (D-05) の主なコンテンツになる。

**反映**: `Customers` / `PointHistories` / `InventoryLevels` / `InventoryChanges` を MVP のテーブルに含める。`POST /transactions` は `type = Return` を受け付ける。`GET /reports/sales/*` を MVP に含める。

---

### D-04. シリアル番号 (製造番号)

**背景**: カメラ・家電はレシート / 保証書に製造番号を印字することが多い。どこまで扱うか。

**選択肢**:

| 案 | 内容 | 長所 | 短所 |
| --- | --- | --- | --- |
| ✅ **A. 明細に記録 (モデルは MVP、UI は Phase 2)** | 商品に「シリアル入力要」フラグ、取引明細に `serialNumbers[]`。入力 UI と必須チェックは Phase 2 | 追加が小さい。後からテーブルを増やさずに済む | MVP 時点では入力されない |
| B. 明細に記録・MVP から入力 | A の UI・検証も MVP に含める | 最初からレシートに印字できる | MVP の MAUI 画面が増える |
| C. シリアル在庫まで追跡 | 入荷時にシリアル登録し、どの店にどのシリアルがあるかを管理。販売時に在庫シリアルから選ぶ | 保証・修理・盗難対策に強い | 在庫モデルが大きく変わる (Phase 3 規模) |
| D. 不要 | 扱わない | 最小 | 家電店らしさが減る |

**決定**: ✅ **A**。

**意図**: データモデルは最初から持っておかないと後で取引テーブルの移行が必要になる。一方で UI は MVP の範囲を広げないため後回しにする。

**反映**: `Products.RequiresSerial`、`TransactionLineSerials` テーブル、`line.serialNumbers[]`。MVP ではサーバは受け取って保存するだけで、必須チェックはしない。

---

### D-05. 管理系 CRUD の置き場所

**背景**: 商品・スタッフ・店舗などマスタの登録・更新をどこで行うか。管理 API の要否と優先度が決まる。

**選択肢**:

| 案 | 内容 | 長所 | 短所 |
| --- | --- | --- | --- |
| A. レジ専用 + シードデータ | サーバ起動時に CSV 等で投入。MAUI は参照のみ。登録・更新 API は後回し | 「レジ端末アプリ」に集中できる | 管理画面がなく、データ変更はシード編集 |
| ✅ **B. サーバ同居の Web 管理画面 (Blazor)** | ASP.NET Core サーバに API と Blazor 管理画面を同居させ、マスタ管理・レポート閲覧を行う | 「本部 (管理画面) → 店舗 (レジ)」の構成をサンプルで示せる | 管理 API と画面の実装量が増える |
| C. MAUI 内に管理モード | タブレット 1 台で商品登録まで完結 | 小規模店向けに現実的 | チェーン想定 (D-00) と合わない |

**決定**: ✅ **B**。利用者の当初想定 (サーバは API + Blazor で作る) に一致。基盤は `template-maui-server` (Minimal API + Blazor Server + MudBlazor) を流用する ([D-19](#d-19-技術スタックプロジェクト構成-テンプレート準拠))。

**意図**: 家電量販店・ホームセンターはチェーンなので、マスタは本部で管理して店舗へ配信する構成が自然。Blazor 管理画面から使う CRUD を API として定義しておけば、Blazor 側は HTTP 経由でもアプリケーションサービス直呼びでも実装できる。

**反映**: 各マスタに登録 (`POST`) / 更新 (`PUT`) / 論理削除 (`DELETE`) を定義し、「管理系」として区別する。端末が使うのは参照と差分同期のみ。Blazor ページは Accessor と Domain を直接呼ぶ (HTTP を経由しない、[D-19](#d-19-技術スタックプロジェクト構成-テンプレート準拠))。

---

### D-06. テナント構成

**背景**: 1 社専用か、複数社が使う SaaS 型か。後から変えると全テーブル・全クエリに手が入る。

**選択肢**:

| 案 | 内容 | 長所 | 短所 |
| --- | --- | --- | --- |
| ✅ **A. 単一テナント** | 1 社・複数店舗・複数端末。URL は `/api/v1/products`、DB に TenantId なし | 単純。サンプルの本題 (POS) に集中できる | SaaS 化するときに全面改修 |
| B. マルチテナント | トークンに tenantId、全テーブルに TenantId 列 + 全 SQL への条件付加。URL は `/api/v1/{tenantId}/...` かトークン判別 | 「POS SaaS の作り方」を示せる | 全リソースにテナント境界の実装とテストが必要 |

**決定**: ✅ **A. 単一テナント**。

**意図**: 「自社チェーンの POS」を示すサンプルなので、テナント分離はノイズになる。

**反映**: TenantId 列なし。店舗 (`Stores`) が最上位の組織単位。

---

### D-07. ポイント制度

**背景**: 家電量販店ではポイントが購買体験の中心。計算方法で商品マスタ・取引・支払の構造が変わる。

**選択肢**:

| 案 | 内容 | 長所 | 短所 |
| --- | --- | --- | --- |
| ✅ **A. 商品別還元率 + 1pt = 1 円充当** | `Product.pointRate` (10% / 5% / 1% / 0%) で付与。ポイント利用は支払方法の一種 (`kind = Points`) として扱い、充当分にはポイントを付けない | 家電量販店の実態に近い。支払方法として扱うので精算・レポートに自然に載る | 按分計算が必要 (充当分の控除) |
| B. 取引合計に一律率 | 例: 合計の 1% を付与 | 単純 | 商品別の還元率が表現できない |
| C. 付与のみ (利用なし) | 残高を積み上げるだけ | 最小 | ポイント払いがない |

**決定**: ✅ **A**。付与基準 (税込 / 税抜) と端数処理は会社設定にする。

**意図**: 「デジカメは 10%、SD カードは 1%」のような商品別還元と、ポイント払いの両方があって初めて家電店のレジらしくなる。

**反映**: `Products.PointRate`、`PaymentMethods.Kind = Points`、`Transactions.PointsEarned / PointsRedeemed / PointsBalanceAfter`、`TransactionLines.PointsEarned`、`Customers.PointBalance`、`PointHistories`。計算式は [api-design.md §4.4](api-design.md#44-ポイント)。

---

### D-08. 配送情報

**背景**: 大型家電や資材は配送、在庫切れは取り寄せが日常。レジ会計時の配送情報をどう扱うか。

**選択肢**:

| 案 | 内容 | 長所 | 短所 |
| --- | --- | --- | --- |
| ✅ **A. 取引に `delivery` を任意で持つ** | 配送先・希望日・時間帯を取引に記録。配送料は「配送料」商品 (サービス種別) の明細として計上。配送手配自体はスコープ外 | 追加が小さい (1 テーブル) | 配送の進捗管理はできない |
| B. 受注 (orders) で扱う | 配送・取り寄せ・取り置きをまとめて受注リソースで管理 (Phase 2)。取引はレジ会計のみ | 取り寄せまで一貫して扱える | D-01 の「受注は将来」と同じく MVP 外になる |
| C. 扱わない | 配送関連はスコープ外 | 最小 | 家電店らしさが減る |

**決定**: ✅ **A**。取り寄せ・取り置きは将来の受注リソースで扱う。

**意図**: レシートに配送先を印字できれば家電店のレジとして十分。進捗管理まで踏み込むと別システムの領域になる。

**反映**: `TransactionDeliveries` テーブル (取引 1 : 0..1)、`Products.Kind = Service` (配送料・延長保証・設置工事など、在庫を持たない商品)。

---

### D-09. 認証・端末登録 (後回し)

**背景**: 端末とスタッフの認証・認可をどう行うか。

**決定**: ✅ **後回し**。まず API と DB の設計・実装を進める。

**当面の扱い**: 端末発の要求は本文 / クエリで `storeId` / `terminalId` / `staffId` を明示的に渡す。認証導入後はトークンのクレームと本文の一致をサーバが検証する形にすれば、API の形を変えずに済む。ベースにした `Service-CloudManager` には認証がないため MVP は認証なし。Phase 2 で `template-maui-server` の管理画面 Cookie ログインと API の JWT (`/api/account/login`) を参考に追加する ([D-34](#d-34-参考プロジェクトの差し替え-phase-0))。

**将来の方針案** (未決定):

- 端末登録: 管理画面でワンタイムコード発行 → 端末がコードを送って端末トークン取得 (Square Devices API のデバイスコード方式)
- スタッフ認証: スタッフコード + PIN → JWT (storeId / terminalId / staffId / role)。テンプレートの `TokenService` を流用
- 認可: 役割 (Cashier / Manager / Admin) で取消・値引承認・精算・マスタ編集を制御

**反映**: `Staff.Role` 列と `TransactionDiscounts.ApprovedByStaffId` 列は先に用意しておく (値は MVP では未使用)。

---

### D-17. 端末のナビゲーション構成

**背景**: 端末は通常のスマートフォン (縦持ち・片手) で、カメラによる JAN / QR スキャンを使う。機能をどう並べるか。

**選択肢**:

| 案 | 内容 | 長所 | 短所 |
| --- | --- | --- | --- |
| ✅ **A. メニュー型** | ホーム画面に機能タイル (販売 / 返品 / 精算 / 照会 …)。各機能からはホームへ戻る | 分かりやすい。機能追加が容易。レジ以外の業務 (棚卸・照会) と同列に扱える | 販売に入るのに毎回 1 タップ |
| B. 販売起点 + ドロワー (Square / Loyverse 型) | ログイン後すぐ販売画面。他機能は左上のハンバーガーメニュー | レジ担当の操作の 9 割が販売なので最短 | 販売以外が隠れる。ドロワーは片手で開きにくい |
| C. ボトムタブ | 下部に 販売 / 取引 / 照会 / メニュー の 4 タブ | 頻出機能を常時表示 | タブ数を増やせない。縦画面で販売画面の高さが減る |

**決定**: ✅ **A. メニュー型** (利用者指定)。折衷として「販売タイルを最上段に大きく」「設定でログイン後に販売画面を直接開ける」を提案 ([screen-design.md §1.2](screen-design.md#12-ナビゲーション構成))。

**意図**: サンプルとして機能の一覧性を優先する。B の利点 (最短で販売に入る) は設定で補える。`template-maui` のメニュー画面 (大きなボタンのグリッド) と同じ作りになる。

**反映**: 画面 T-02 ホーム。レシートは画面表示 + 電子レシート QR を基本にし、Bluetooth 印刷は Phase 2。画面の骨格 (タイトル + F1〜F4) は [D-23](#d-23-端末の画面骨格-template-maui-のシェル準拠)。

---

### D-18. 管理画面の構成

**背景**: サーバ側 (Blazor) の画面構成。

**決定**: ✅ **左ナビゲーション + 右コンテンツ (一覧ページ + 詳細 / 編集ダイアログ)** の Blazor によくある構成 (利用者指定)。Blazor Web App (Interactive Server) で API と同じ ASP.NET Core に同居させる。

**UI ライブラリ**: ✅ **MudBlazor** (`template-maui-server` / `template-blazor-server` / `CloudManager` がいずれも MudBlazor のため)。`MudNavMenu` + `MudNavGroup` のグループ化ナビ、`MudDataGrid` の `ServerData` によるサーバ側ページング、`IDialogService` によるダイアログ、FluentValidation によるフォーム検証、Snackbar 通知をテンプレートのまま使う。

**反映**: [screen-design.md §2](screen-design.md#2-サーバ管理画面-blazor)。管理画面の機能は API の「管理」用途と 1 対 1 に対応させる。

---

## 2. 設計者判断 (未確認、異論があれば変更可)

### D-10. 冪等性: クライアント採番 ID

| 案 | 内容 |
| --- | --- |
| ✅ **A. クライアント採番 GUID (v7)** | 取引・シフト・入出金・在庫変動など端末発の書き込みは端末が ID を採番して送る。同じ ID が既にあれば 200 で既存を返す (本文が異なれば 409) |
| B. `Idempotency-Key` ヘッダ | Square 方式。サーバがキーと応答を保存する専用テーブルが必要 |

**意図**: オフライン再送を扱うには ID をクライアントが持つのが最も単純。`Guid.CreateVersion7()` (.NET 9) で時系列順にもなる。

**補足 (SQLite)**: テンプレートは `INTEGER PRIMARY KEY AUTOINCREMENT` の `long` ID だが、端末採番のために GUID を採る。SQLite では **TEXT (36 文字、小文字ハイフン区切り)** で保存する (`Microsoft.Data.Sqlite` の既定。v7 は文字列順 = 時系列順になる)。サーバだけが採番するもの (取引由来の在庫変動・ポイント履歴) も同じ GUID にそろえる。

### D-11. 税計算: 税率ごと一括計算

| 案 | 内容 |
| --- | --- |
| ✅ **A. 税率ごとに合計してから税額計算** | 同じ税率の明細の合計に対して税額を計算し、税率ごとに端数処理。インボイス対応レシートの「税率ごとの区分記載」と一致する |
| B. 明細ごとに税額計算して合計 | 明細ごとに端数処理するため、合計で誤差が出やすい |

**意図**: 日本のレシートの標準的な方式。取引値引は明細へ按分してから税計算する。

### D-12. 在庫: 変動履歴ベース

| 案 | 内容 |
| --- | --- |
| ✅ **A. 変動履歴 (`InventoryChanges`) + 現在庫 (`InventoryLevels`)** | Square Inventory API と同じ。販売 / 返品 / 取消 / 棚卸 / 調整をすべて履歴として残し、現在庫はその集計 (非正規化して保持) |
| B. 現在庫数量を直接更新 | Clover item stocks 方式。単純だが監査できない |

**意図**: 棚卸差異や返品の追跡ができる。取引による自動減算と手動調整を同じ仕組みで扱える。

### D-13. 金額・数量・率の表現: `decimal`

| 案 | 内容 |
| --- | --- |
| ✅ **A. 金額・数量・率とも `decimal`** + 会社設定の通貨コード | C# で自然。JPY は整数値のみ使う。数量は `decimal(9,2)` 相当で切り売り (m 単位) も表現できる |
| B. 金額は整数 (円、`long`)、数量は整数 (`int`) | Square `Money` (最小通貨単位の整数) 方式。SQLite の `INTEGER` に素直に載るが、C# 側で扱いが冗長 |

**決定**: ✅ **A** (利用者確認済み。SQLite 採用時に一度 B に変えたが、`decimal` でよいとの指示で A に戻した)。

**SQLite での扱い**: `Microsoft.Data.Sqlite` は `decimal` パラメータを TEXT で書き込むため、金額・数量・率の列は **`NUMERIC` 親和性**で宣言する。数値として正しい文字列は INTEGER / REAL に変換されて保存されるので、`SUM` などの集計がそのまま使える。読み出しは `GetDecimal` が INTEGER / REAL / TEXT のいずれからも変換する ([db-design.md §1](db-design.md#1-前提))。

### D-14. 返品の表現: `type = Return` の取引

| 案 | 内容 |
| --- | --- |
| ✅ **A. 返品を「取引」として登録** (`type = Return`、`originalTransactionId` で元取引に紐付け) | Loyverse の receipt_type = REFUND と同じ。オフラインでも端末が返品取引を作って送れる |
| B. `POST /transactions/{id}/refunds` で元取引に返金を追加 | Square 方式。サーバ側で元取引を更新する必要があり、オフライン時に扱いにくい |

**意図**: 一体型 (D-01) と D-02 を貫くと、返品も「端末で完結した取引」として同じ経路で送るのが一貫する。交換は「返品取引 + 販売取引」の 2 件で表す。

### D-15. レシート番号: 端末採番

| 案 | 内容 |
| --- | --- |
| ✅ **A. 端末が `{店舗コード}-{端末番号}-{連番}` を採番** (例 `S001-02-000123`) | オフラインでも採番できる。サーバは一意制約で重複を弾く |
| B. サーバ採番 | オフライン時に番号が決まらず、レシートが印字できない |

### D-16. 日次締めは Phase 2

店舗 × 営業日の締め (`DailyClosings`) は精算 (シフト) が揃ってからの集計であり、MVP のレポート API で代替できるため Phase 2 とする。テーブル定義だけ DB 設計に載せる。

---

## 3. 参考プロジェクトを確認して合わせた判断

利用者指定の参考プロジェクトを確認し、その流儀に合わせた判断。詳細は [architecture.md](architecture.md)。

| 参考プロジェクト | 役割 |
| --- | --- |
| `D:\GitHubTemplate\template-maui` (`Template.MobileApp`) | MAUI 端末アプリの基盤 (シェル・ナビゲーション・SQLite・通信・スキャン) |
| `D:\GitHubTemplate\template-web-api` (`Template.ApiServer`) | Minimal API + Smart.Data.Accessor + SQLite の API サーバ構成 |
| `D:\GitHubTemplate\template-blazor-server` (`Template.BlazorServer`) | Blazor Server + MudBlazor の管理画面構成 (CSV 出力・PDF 帳票あり) |
| `D:\GitHubTemplate\template-maui-server` (`Template.MobileServer`) | **上 2 つを合わせた「MAUI の対向サーバ」。本サンプルのサーバはこれをベースにする** |
| `D:\GitHubUser\Study-AWS\CloudManager` | MudBlazor の多画面管理 UI の実例 (`MudNavGroup` によるグループ化ナビ、ダイアログ多数) |

### D-19. 技術スタック・プロジェクト構成 (テンプレート準拠)

| 案 | 内容 |
| --- | --- |
| A. EF Core + 任意の RDBMS | 当初案 (DB 設計 v0.1) |
| ✅ **B. テンプレートと同じ構成: SQLite + `Usa.Smart.Data.Accessor` (SQL ファイル) + Minimal API + Blazor Server (MudBlazor) + Aspire AppHost** | 利用者のテンプレート群と同じ書き方になり、流用・比較がしやすい |

**決定**: ✅ **B**。DB は利用者指定で SQLite。

**反映**:

- ORM ではなく `[DataAccessor]` + 2-way SQL ファイル。テーブルは起動時に `CREATE TABLE IF NOT EXISTS` (テンプレートの `InitializeApplicationAsync` → `CreateTable()`) で作る。マイグレーションは持たない
- **Service / Usecase の層は置かない** (利用者指示)。サーバは Endpoints (API) / Blazor ページ → **Accessor (SQL) + Domain (ロジック)** の 2 段。複数テーブルの更新は呼び出し側が `IDbProvider.UsingTxAsync` で Accessor の `DbTransaction` 付きメソッドを束ねる。プロジェクトは `Core` (Accessors / Sql / Models.Entity / Infrastructure) と `Web` (Endpoints / Models / Mappers / Components / Settings)
- Serilog / OpenTelemetry / FeatureManagement / ヘルスチェック / OpenAPI (NSwag UI) はテンプレートのまま
- MAUI 側は `template-maui` 系テンプレートの構成 (Smart.Navigation + 独自シェル、Smart.Mvvm、Rester、BarcodeScanning.Native.Maui、Smart.Data.Accessor + SQLite、BunnyTail DI) をそのまま使う (Phase 0 でベースを `template-maui-keyboard` に変更、[D-34](#d-34-参考プロジェクトの差し替え-phase-0))。**Android 専用** (テンプレートが `net10.0-android` のみ)

### D-20. JSON 契約: camelCase

| 案 | 内容 |
| --- | --- |
| ✅ **A. camelCase** (Minimal API の既定 `JsonNamingPolicy.CamelCase`) | 利用者指示。MAUI 側は Rester の `UseJsonSerializer` で `PropertyNamingPolicy = CamelCase` を設定する |
| B. PascalCase (`PropertyNamingPolicy = null`) | `template-maui-server` の現状 (Rester 既定との契約)。テンプレート側を camelCase に変更予定とのことなので、実装時にテンプレートを再確認する |

**決定**: ✅ **A**。日時は `yyyy-MM-ddTHH:mm:ss.fffZ` (UTC) のテンプレート `DateTimeConverter`、`null` プロパティは省略、クエリパラメータも camelCase (`?storeId=`)。

### D-21. ページング: `page` / `size` + 総件数

| 案 | 内容 |
| --- | --- |
| A. カーソル方式 (`cursor` / `nextCursor`) | 当初案。Square 流 |
| ✅ **B. `page` (0 始まり) / `size` + 一覧応答 `xxxListResponse { total, page, size, items }`** | テンプレートの `DataListResponse` と `MudDataGrid` の `ServerData` (総件数が必要) に一致する |

**決定**: ✅ **B**。並び替えは `sort` / `desc` を `SqlHelper.NormalizeSort` の許可リストで検証する (テンプレートどおり)。差分同期は `updatedSince` で絞った一覧を `UpdatedAt, Id` 順にページングする。

### D-22. 共有プロジェクト: 通信データとドメインロジックは別プロジェクト

「`XxxRequest` / `XxxResponse` (通信データ) の共有」と「ドメインロジックの共有」は別の概念 (利用者指示) なので、混ぜずに分ける。

| 案 | 内容 |
| --- | --- |
| A. 1 つの共有プロジェクトに通信データと計算ロジックをまとめる | 当初案。概念が混ざる |
| ✅ **B. `Pos.Domain` (ドメインロジック) と `Pos.Shared` (通信データ) の 2 プロジェクト** | ドメインロジックを共通に切り出すなら `Xxx.Domain` (利用者指示)。通信データは Blazor の Client / Server / Shared 慣例に倣い `Shared` |
| C. 通信データは共有せず、テンプレート流儀でサーバ (`Web/Models/Api`) と端末 (`Models/Api`) に別々に持つ | 契約の変更に両側の修正が要る。取引の Request / Response は大きく、ずれやすい |

**決定**: ✅ **B**。

- `Pos.Domain`: 列挙型、計算ロジック (税・値引按分・ポイント・返品導出)、業務ルールの検証。UI・DB・HTTP に依存しない
- `Pos.Shared`: `XxxRequest` / `XxxResponse`。`Pos.Domain` の列挙型を参照する。サーバ (`Web`) と端末の両方から参照する
- エンティティ (DB) はサーバ `Core` に、端末のローカルエンティティは MAUI 側に、それぞれテンプレートどおり残す

### D-26. 命名: `Pos.Server.*` / `Pos.Terminal` / `Pos.Shared` / `Pos.Domain`

テンプレートの `Template.MobileServer.*` / `Template.MobileApp` に対し、`Template` の部分を `Pos` にし、続けてサーバ / 端末が分かる名称にする (利用者指示)。

| プロジェクト | 内容 |
| --- | --- |
| `Pos.Server.Core` / `Pos.Server.Host` / `Pos.Server.AppHost` | サーバ (テンプレートの `Template.MobileServer.*` 相当) |
| `Pos.Terminal` | 端末 (MAUI、テンプレートの `Template.MobileApp` 相当) |
| `Pos.Shared` | 通信データ |
| `Pos.Domain` | ドメインロジック |
| `Pos.Domain.Tests` / `Pos.Server.UnitTests` / `Pos.Server.IntegrationTests` | テスト |

名前空間も同じ。`Pos.Terminal` の名称 (端末) は `Pos.Register` などに変えてもよい。

### D-27. 用語: DTO は使わない

通信データは `XxxRequest` / `XxxResponse` と呼び、「DTO」という語は文書・アセンブリ名・名前空間・クラス名のいずれにも使わない (利用者指示)。一覧は `XxxListResponse`、入れ子の要素はテンプレートの `DataListResponseEntry` に倣い `XxxResponseLine` / `XxxRequestLine` のように親の名前に要素名を続ける。

### D-23. 端末の画面骨格: `template-maui` のシェル準拠

| 案 | 内容 |
| --- | --- |
| A. 一般的なスマホ UI (ハンバーガー、ボトムシート、FAB) | 当初の画面設計 v0.1 |
| ✅ **B. `template-maui` の独自シェル: 上部タイトル + 下部 F1〜F4 ファンクションキー、`ContentView` を `ViewId` で遷移、ダイアログは MauiComponents のポップアップ** | テンプレートの資産 (シェル、`AppViewModelBase`、`InputNumber` テンキー、`IDialog`) をそのまま使える。業務用ハンディ端末に近い操作体系で POS に向く |

**決定**: ✅ **B**。各画面は F1〜F4 の割り当てを持つ (画面一覧に列を追加)。ボトムシートは使わず、明細編集・値引・数量入力はポップアップ。数値入力はテンプレートの `InputNumber` ポップアップ (テンキー) を金額・数量に流用する。

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

**意図**: それぞれのテンプレートの流儀に合わせる。JSON 契約は両者とも ISO 8601 (UTC) なので、境界での変換はテンプレートのコンバータで済む。

---

## 4. 実装前に確認した事項

### D-28. UI の言語: 日本語固定

端末・管理画面とも日本語固定 (利用者確認済み)。多言語化 (resx) はしない。テンプレートの MAUI は英語ラベルだが、POS 画面はすべて日本語で作る。

### D-29. 実装順序

[architecture.md §7](architecture.md#7-実装計画) の順 (土台 → `Pos.Domain` → `Pos.Shared` → サーバ DB → サーバ API → 管理画面 → 端末) で進める (利用者確認済み)。サーバを先に通してから端末に入る。

### D-30. 初期データの規模

[architecture.md §8](architecture.md#8-初期データ) のとおり (商品は大分類ごとに 10 件程度、利用者確認済み)。他店在庫照会を見せるため店舗は 2 つにする (設計者判断)。

### D-31. 設計ドキュメントの扱い

設計は `docs/` に置き、このブランチにコミットする (利用者確認済み)。実装中に判断が変わった場合は本書 (decisions.md) に追記し、該当文書を更新する。

### D-32. リポジトリ構成: モノレポ + 2 ソリューション

**背景**: 参考テンプレートは端末 (`template-maui`) とサーバ (`template-maui-server`) が別リポジトリだが、本サンプルは 1 つのリポジトリに端末とサーバを同居させる (利用者指示)。一方で端末とサーバは Visual Studio で個別に動かしたい。

| 案 | 内容 |
| --- | --- |
| A. 2 リポジトリ (テンプレート流儀、共有はサーバ側を相対参照) | テンプレートと同じ分け方だが、共有プロジェクトの参照が兄弟リポジトリ前提になる |
| B. 1 リポジトリ・1 ソリューション | 全部を 1 つの `.slnx` に入れる。VS で端末とサーバを別々に扱いにくい |
| ✅ **C. 1 リポジトリ・2 ソリューション** (`server/Pos.Server.slnx`、`terminal/Pos.Terminal.slnx`、共有は `shared/` を両方に含める) | 利用者指示 (モノレポ、個別起動) を両方満たす。各フォルダはテンプレートと同じ形になる |

**決定**: ✅ **C**。共通のビルド設定 (`.editorconfig` / `Directory.Build.props` / `Analyzers.ruleset` / `.gitattributes`) はテンプレート間で同一なのでルートに 1 セット置く。設計ドキュメントは本リポジトリの `docs/` に置く。詳細は [architecture.md §2](architecture.md#2-プロジェクト構成)。

### D-33. 実装の進め方: フェーズ単位のチェックリスト

[implementation-plan.md](implementation-plan.md) にフェーズごとの `- [ ]` チェックリストと完了条件を置き、フェーズ単位で着手・完了報告する (利用者指示)。完了した項目はチェックを付け、設計の変更があれば本書と該当文書を更新してからフェーズを閉じる。

---

## 5. 実装中の判断

### D-34. 参考プロジェクトの差し替え (Phase 0)

**背景**: 土台作りの着手時に、参考にするプロジェクトが利用者から指定し直された。

| 対象 | 変更前 (§3) | 変更後 |
| --- | --- | --- |
| サーバ | `template-maui-server` | **`D:\GitHubService\Service-CloudManager`** (`CloudManager.Core` / `CloudManager.Host`)。Aspire AppHost と OpenAPI (NSwag UI) は含まれないので **`template-blazor-server`** から加える |
| 端末 | `template-maui` | **`D:\GitHubTemplate\template-maui-keyboard`** (`Template.MobileApp`)。不要なフォント (OpenSans / FluentUI、未使用の参照) と `dotnet_bot.png` は削除し、`MaterialIcons` だけ残す。色は同テンプレートの `Colors.xaml` を使い、販売・会計画面のデザインは `template-maui` の POS 画面 (`UIPosView`) に倣う |

**反映**:

- サーバのホストプロジェクト名は `Pos.Server.Web` ではなく **`Pos.Server.Host`** (`CloudManager.Host` / `Template.BlazorServer.Host` に倣う)。D-26 の表は読み替える
- `Service-CloudManager` には認証・OpenTelemetry・FeatureManagement・レート制限がない。MVP はそのまま (認証なし、[D-09](#d-09-認証端末登録-後回し))。JSON は最初から camelCase (`NamingPolicy`)
- `template-maui-keyboard` の Input (物理キー・ショートカット) と Behaviors は残す。Bluetooth バーコードスキャナ (HID キーボード) の入力にも使える
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

**背景**: 端末の画面遷移 (`ContentView` の差し替え) にアニメーションがなく、進む / 戻るの感覚がつかみにくい。Smart.Navigation.Maui には効果 (`MauiEffect.Forward` = 右からスライド、`MauiEffect.Back` = 左からスライド、`Push` / `Pop` / `Fade`) が用意されている。

| 選択肢 | 内容 | 評価 |
| --- | --- | --- |
| A. 遷移ごとに効果を指定 | `Navigator.ForwardAsync(ViewId.Xxx, new NavigationParameter().WithForwardEffect())` のように呼び出し側で毎回指定 | 指定漏れが起きやすく、メニューへ戻る `ForwardAsync(ViewId.Menu)` を Back にし忘れやすい |
| ✅ **B. 画面の階層で自動決定** | 各画面に `[Hierarchy(n)]` を付け、`HierarchyEffectPlugin` (Usa.Smart.Navigation 3.11.0) が階層が深くなる遷移に Forward、浅くなる遷移に Back を付ける | 遷移の呼び方は変えずに済む。`config.AddHierarchyEffectPlugin()` の 1 行と属性だけ |

**反映**:

- 階層は screen-design §1.3 の遷移図の深さ: T-00 = 0、T-01 = 1、ホーム T-02 = 2、ホーム直下 = 3、その下 = 4 …。親が複数ある画面は最も深い親 + 1
- 同じ階層への遷移と起動時の最初の遷移は効果なし。別の効果にしたい遷移は `NavigationParameter` の `WithFadeEffect()` などで明示する (明示した効果が優先)
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

- `Pos.Server.Host`: パッケージ `OysterReport`、`Assets/Fonts/ipaexg.ttf`、`Assets/Reports/*.xlsx` (`CopyToOutputDirectory`)、`Infrastructure/Reports/` にフォントリゾルバと帳票ごとの `XxxReportBuilder` (シングルトン、`byte[] Build(...)`)。エンドポイントは各リソースのグループに置き、`TypedResults.File(bytes, "application/pdf", ファイル名)` を返す。データがなければ 404 / 400
- 管理画面のボタンは `MudButton Href="api/v1/.../pdf"` (認証は後回しなので直接リンク)
- テンプレートは Excel で作る。1 シート = 1 ページを基本にし、明細行はプレースホルダの行から順に埋める。複数ページ (複数シフトなど) はシートのコピーで作る (`GadgetFood` の給与明細と同じ)
- 端末のレシート (T-22) は画面表示 + 電子レシート QR + 画像共有のままで、サーバの PDF は使わない (オフラインでも出せるように)

### D-38. 警告の抑止

`Pos.Shared` に型を置くと、名前空間の `Shared` が VB の予約語のため CA1716 が全ファイルで出る。`ProductResponse.ImageUrl` (string) には CA1056 が出る。

| 案 | 内容 |
| --- | --- |
| A. `Analyzers.ruleset` で Hidden | 他の `.Shared` プロジェクトと同じだが、全プロジェクトに効く |
| ✅ **B. `Pos.Shared` の `GlobalSuppressions.cs` でアセンブリ単位に抑止** | 影響を `Pos.Shared` に限定する |
| C. 名前空間を `Pos.Contracts.*` に変える | プロジェクト名と名前空間が食い違う |

**決定**: ✅ **B** (利用者確認済み)。

`Pos.Server.Core` の CA1000 (汎用 `EnumTextConverter<T>` の static メンバー。`IValueConverter` の static abstract 実装なので回避できない)、CA1056 (`ProductEntity.ImageUrl`)、CA1819 (`StaffEntity.PinHash`) は該当箇所だけ `#pragma warning disable / restore` で抑止する (利用者確認済み)。
