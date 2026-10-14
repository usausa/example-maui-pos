# example-maui-pos

MAUI (レジ端末アプリ) + ASP.NET Core (POS サーバ: API + Blazor 管理画面) の POS サンプル。  
家電・カメラ・ホームセンターの物販を想定し、販売・会計・レシート、取り寄せ・取り置きの受注と前受金、レジ開閉と精算、日次締め、返品・取消、会員ポイント、在庫 (棚卸・調整、仕入先への発注と入荷、店舗間移動)、売上レポートを扱う。  
管理画面はログイン (管理者 / オペレーター)、端末はペアリングコードで登録し、スタッフは PIN でログインする (値引と取消の承認も PIN)。

## 📱 端末 (MAUI Android)

| | | | |
| --- | --- | --- | --- |
| ![ホーム](docs/images/terminal-home.png) | ![販売](docs/images/terminal-sales.png) | ![会計](docs/images/terminal-payment.png) | ![レシート](docs/images/terminal-receipt.png) |
| ホーム | 販売 | 会計 | レシート |
| ![初期設定](docs/images/terminal-setup.png) | ![商品検索](docs/images/terminal-product-search.png) | ![精算](docs/images/terminal-shift-close.png) | ![売上照会](docs/images/terminal-sales-report.png) |
| 初期設定 | 商品検索 | 精算 | 売上照会 |
| ![取引履歴](docs/images/terminal-transactions.png) | ![商品・在庫照会](docs/images/terminal-product-inquiry.png) | ![受注](docs/images/terminal-orders.png) | ![受注詳細](docs/images/terminal-order-detail.png) |
| 取引履歴 | 商品・在庫照会 | 受注 | 受注詳細 (前受金) |
| ![検品](docs/images/terminal-receiving.png) | ![明細の編集](docs/images/terminal-line-edit.png) | ![販売の操作](docs/images/terminal-sales-menu.png) | ![PIN の入力](docs/images/terminal-pin.png) |
| 入荷・移動の検品 | 明細の編集 | 販売の操作 | PIN の入力 |

## 💻 管理画面 (Blazor + MudBlazor)

| |
| --- |
| ![ダッシュボード](docs/images/server-dashboard.png) |
| ダッシュボード: 本日の売上と、要確認・未締め・引き渡し待ちの一覧 |
| ![売上集計](docs/images/server-sales-report.png) |
| 売上集計: 期間・店舗・集計単位ごとの売上と CSV |
| ![取引](docs/images/server-transactions.png) |
| 取引: 条件での検索と、詳細からのレシートの控え |
| ![受注](docs/images/server-orders.png) |
| 受注: 取り寄せ・取り置きと前受金、受注票 |
| ![日次締め](docs/images/server-daily-closings.png) |
| 日次締め: 店舗 × 営業日の締めと売上日報 |
| ![商品](docs/images/server-products.png) |
| 商品: 画像付きの商品の管理と CSV の入出力 |
| ![発注](docs/images/server-purchase-orders.png) |
| 発注: 発注から入荷予定を作り、発注書を出す |
| ![入荷](docs/images/server-inventory-receipts.png) |
| 入荷: 仕入先からの入荷予定と受領 |
| ![店舗間移動](docs/images/server-inventory-transfers.png) |
| 店舗間移動: 依頼・出荷・受領 |
| ![レジ端末](docs/images/server-terminals.png) |
| レジ端末: 設定 QR とペアリングコードによる端末の登録 |
| ![ユーザー](docs/images/server-accounts.png) |
| ユーザー: 管理者とオペレーターのアカウント |

## 📄 帳票 (PDF)

| | | | | |
| --- | --- | --- | --- | --- |
| ![受注票](docs/images/report-order.png) | ![発注書](docs/images/report-purchase-order.png) | ![精算レポート](docs/images/report-shift.png) | ![売上日報](docs/images/report-daily-sales.png) | ![レシートの控え](docs/images/report-receipt.png) |
| 受注票 (前受金の預り証を兼ねる) | 発注書 | 精算レポート | 売上日報 | レシートの控え |

## 📚 ドキュメント

| 文書 | 内容 |
| --- | --- |
| [docs/getting-started.md](docs/getting-started.md) | 構成、サーバと端末の起動、サンプル取引の生成、テストと静的解析 |
| [docs/architecture.md](docs/architecture.md) | ソリューション構成。プロジェクト・層・パッケージ、初期データとサンプル取引の生成ツール |
| [docs/api-design.md](docs/api-design.md) | サーバ API 設計。共通仕様、リソース別エンドポイントと Request / Response、金額・税・ポイント計算仕様、エラーコード、端末の同期フロー |
| [docs/db-design.md](docs/db-design.md) | サーバ DB 設計 (SQLite)。ER 図、テーブル定義、DDL 例、更新の単位、端末ローカル DB |
| [docs/screen-design.md](docs/screen-design.md) | 画面設計。端末と管理画面の画面一覧・遷移・レイアウト |
| [docs/decisions.md](docs/decisions.md) | 設計方針。機能とアーキテクチャについて、何をどこまでサポートし、どうあるべきか |
| [docs/backlog.md](docs/backlog.md) | 未実装と今後の検討事項。後回しにしているもの、作らないと決めたもの、前提として残る制約 |
