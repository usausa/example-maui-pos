# template-maui-pos

MAUI (レジ端末アプリ) + ASP.NET Core (POS サーバ: API + Blazor 管理画面) の POS サンプル。  
家電・カメラ・ホームセンターの物販を想定し、販売・会計・レシート、レジ開閉と精算、返品・取消、会員ポイント、在庫 (棚卸・調整)、売上レポートを扱う。

## 端末 (Android)

| | | | |
| --- | --- | --- | --- |
| ![ホーム](docs/images/terminal-home.png) | ![販売](docs/images/terminal-sales.png) | ![会計](docs/images/terminal-payment.png) | ![レシート](docs/images/terminal-receipt.png) |
| ホーム | 販売 | 会計 | レシート |
| ![初期設定](docs/images/terminal-setup.png) | ![商品検索](docs/images/terminal-product-search.png) | ![精算](docs/images/terminal-shift-close.png) | ![売上照会](docs/images/terminal-sales-report.png) |
| 初期設定 | 商品検索 | 精算 | 売上照会 |

## 管理画面 (Blazor + MudBlazor)

| |
| --- |
| ![ダッシュボード](docs/images/server-dashboard.png) |
| ダッシュボード: 本日の売上・要確認、店舗別売上、開設中シフト、端末の通信状態、マイナス在庫 |
| ![売上集計](docs/images/server-sales-report.png) |
| 売上集計: 期間・店舗・集計単位 (日別 / 店舗別 / 時間帯別 / 端末別 / 担当別 / 支払方法別 / 税率別 / 部門別)、CSV |
| ![取引](docs/images/server-transactions.png) |
| 取引: 営業日・店舗・端末・種別・状態・レシート番号で検索 |

## ドキュメント

| 文書 | 内容 |
| --- | --- |
| [docs/getting-started.md](docs/getting-started.md) | 構成、サーバと端末の起動、サンプル取引の生成、テストと静的解析 |
| [docs/architecture.md](docs/architecture.md) | ソリューション構成。プロジェクト・層・パッケージ、初期データ |
| [docs/api-design.md](docs/api-design.md) | サーバ API 設計。共通仕様、リソース別エンドポイントと Request / Response、金額・税・ポイント計算仕様、エラーコード、端末の同期フロー |
| [docs/db-design.md](docs/db-design.md) | サーバ DB 設計 (SQLite)。ER 図、テーブル定義、DDL 例、更新の単位、端末ローカル DB |
| [docs/screen-design.md](docs/screen-design.md) | 画面設計。端末と管理画面の画面一覧・遷移・レイアウト |
| [docs/decisions.md](docs/decisions.md) | 前提の要約と設計判断の記録 |
| [docs/implementation-plan.md](docs/implementation-plan.md) | 実装プラン。フェーズごとのチェックリストと完了条件、後回し項目の計画 |
| [AGENTS.md](AGENTS.md) | AI 向けの規則 (仕事の進め方と全体に共通する規則)。コードの書き方は領域別に `.claude/rules/` |
