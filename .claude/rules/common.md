# 全体と共有プロジェクト

## 全体

- Accessor 以外の場所に SQL を書かない
- 機能に属するものは、機能ごとのサブフォルダか専用のフォルダに置く
- `Builder` はテキストや画像の組み立てだけに使う。データの変換は `XxxMapper`、計算は `XxxCalculator`
- 拡張メソッドは複数の項目を意味でまとめて判定するもの (`IsReturnable`) だけに使い、対象の型 (列挙型を含む) の宣言に続けて同じファイルに書く。単一の値との比較 (`status == TransactionStatus.Voided`) は拡張メソッドにしない
- `ArgumentNullException.ThrowIfNull` は書かない (CA1062 は無効)
- 「DTO」という語はコード・名前空間・文書に使わない
- UI の文言は日本語だけ (ローカライズ資源は持たない)
- 明るい背景の記号は色付きの絵文字にし、標準で文字の形になる記号 (ℹ ⚙ ↩ ▶ ⏸ ⚠ ☁ 🏷 🗑 など) には U+FE0F を付ける。塗りつぶしのチップや色付きのボタンの上には色付きの絵文字を置かず、白い単色のアイコン (Material Icons) にする

## Pos.Domain

- 業務ルールは `Pos.Domain.Logic` に `SalesLogic` / `ReturnLogic` のような業務ごとのクラスで置く (フォルダには分けない)。列挙型は `Pos.Domain.Enums` に 1 型 1 ファイル
- Domain は I/O やフレームワークに依存しない (`Usa.Smart.Core` 程度)。Logic は時刻を引数で受ける (`DateTime.Now` を使わない)
- 業務ルールの違反は `RuleReason` (列挙型) で返し、Domain に文言を置かない (文言はサーバの `ApiRuleText`、端末の `ViewHelper` が `RuleReason` から引く)
- データ長 (文字列の長さ、入力の桁数) は `Length` に定数を宣言して使い、数値を直接書かない

## Pos.Contract

- 通信データは `XxxRequest` / `XxxResponse`。名前はエンドポイントのクラス名 + メソッド名 (`TransactionCreateRequest`、`CustomerPointHistoryResponse`、`ReportSalesSummaryResponse`)
- 一覧の要素は `XxxResponseItem`。要素や Request の中の子要素 (明細・支払・配送など) は `Item` を重ねず、親の名前 + 要素名 (`TransactionResponseLine`、`TransactionResponseDelivery`、`TransactionCreateRequestLine`)
- 資源ごとに名前空間とフォルダを分ける (`Pos.Contract.Customers` など)。独立した資源は独自の名前空間とエンドポイントを持つ
- 日時は UTC の `DateTime` (JSON では `yyyy-MM-ddTHH:mm:ss.fffZ`)、列挙型は文字列
- Request の検証属性 (`Required` / `MaxLength` / `Range`) の値は `Length` の定数から取る
