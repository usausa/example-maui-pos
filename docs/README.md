# 設計ドキュメント

MAUI (レジ端末アプリ) + ASP.NET Core (POS サーバ: API + Blazor 管理画面) の POS サンプルの設計。

| 文書 | 内容 |
| --- | --- |
| [decisions.md](decisions.md) | **設計判断の記録**。検討した選択肢・採用した案・意図・設計への反映 (D-00 〜 D-35) |
| [architecture.md](architecture.md) | ソリューション構成。参考プロジェクトとの対応、プロジェクト・層・パッケージ、初期データ、実装時の確認事項 |
| [implementation-plan.md](implementation-plan.md) | 実装プラン。フェーズごとのチェックリストと完了条件 |
| [api-survey.md](api-survey.md) | 公開 POS API (Square / Clover / Lightspeed / Loyverse / スマレジ) の調査と共通モデル |
| [api-design.md](api-design.md) | サーバ API 設計。共通仕様、リソース別エンドポイントと Request / Response、金額・税・ポイント計算仕様、エラーコード、端末の同期フロー |
| [db-design.md](db-design.md) | サーバ DB 設計 (SQLite)。ER 図、テーブル定義、DDL 例、更新の単位、端末ローカル DB |
| [screen-design.md](screen-design.md) | 画面設計。端末 (スマートフォン、F1〜F4 シェル) と管理画面 (Blazor + MudBlazor) の画面一覧・遷移・レイアウト案 |

## 前提の要約

| 項目 | 決定 | 参照 |
| --- | --- | --- |
| 業種 | 家電・カメラ・ホームセンター (物販) | [D-00](decisions.md#d-00-業種前提) |
| 技術スタック | 既存テンプレート準拠: SQLite + Smart.Data.Accessor、Minimal API + Blazor Server (MudBlazor) + Aspire、MAUI (Android) + Smart.Navigation。Service / Usecase の層は置かない | [D-19](decisions.md#d-19-技術スタックプロジェクト構成-テンプレート準拠) |
| プロジェクト名 | `Pos.Server.*` (Core / Host / AppHost) / `Pos.Terminal` / `Pos.Shared` (通信データ) / `Pos.Domain` (ドメインロジック) | [D-22](decisions.md#d-22-共有プロジェクト-通信データとドメインロジックは別プロジェクト), [D-26](decisions.md#d-26-命名-posserver--posterminal--posshared--posdomain) |
| 取引モデル | 一体型 (会計完了後に 1 回で送信)。受注は将来 | [D-01](decisions.md#d-01-取引モデル-一体型-vs-分離型) |
| 金額計算 | 端末計算 + サーバ検証 (`Pos.Domain`) | [D-02](decisions.md#d-02-金額計算の主体-端末計算--サーバ検証-vs-サーバ計算のみ) |
| MVP | 販売・レジ開閉精算 + 顧客ポイント・在庫・返品交換・売上レポート | [D-03](decisions.md#d-03-mvp-の範囲) |
| ポイント | 商品別還元率 + 1pt = 1 円充当 (支払方法として扱う) | [D-07](decisions.md#d-07-ポイント制度) |
| シリアル番号 | モデルに含める。入力 UI は Phase 2 | [D-04](decisions.md#d-04-シリアル番号-製造番号) |
| 配送 | 取引に `delivery` を任意で持つ | [D-08](decisions.md#d-08-配送情報) |
| 管理系 | サーバ同居の Blazor 管理画面 (MudBlazor) + 管理 API | [D-05](decisions.md#d-05-管理系-crud-の置き場所), [D-18](decisions.md#d-18-管理画面の構成) |
| テナント | 単一 | [D-06](decisions.md#d-06-テナント構成) |
| JSON / ページング | camelCase、`page` / `size` + 総件数 | [D-20](decisions.md#d-20-json-契約-camelcase), [D-21](decisions.md#d-21-ページング-page--size--総件数) |
| 金額・数量・率 | `decimal` (SQLite は NUMERIC 列)、ID は端末採番の GUID | [D-13](decisions.md#d-13-金額数量率の表現-decimal), [D-10](decisions.md#d-10-冪等性-クライアント採番-id) |
| 用語 | 通信データは `XxxRequest` / `XxxResponse`。「DTO」は使わない | [D-27](decisions.md#d-27-用語-dto-は使わない) |
| 端末 UI | スマートフォン縦持ち、カメラスキャン、メニュー型、テンプレートのシェル (タイトル + F1〜F4) | [D-17](decisions.md#d-17-端末のナビゲーション構成), [D-23](decisions.md#d-23-端末の画面骨格-template-maui-のシェル準拠) |
| 認証・端末登録 | 後回し (MVP は認証なし。Phase 2 で template-maui-server を参考に追加) | [D-09](decisions.md#d-09-認証端末登録-後回し) |
| リポジトリ | モノレポ。`server/Pos.Server.slnx` と `terminal/Pos.Terminal.slnx` を VS で個別に開く。共有は `shared/` | [D-32](decisions.md#d-32-リポジトリ構成-モノレポ--2-ソリューション) |
| 進め方 | フェーズ単位のチェックリスト (implementation-plan.md) | [D-33](decisions.md#d-33-実装の進め方-フェーズ単位のチェックリスト) |
