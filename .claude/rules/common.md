# 共有プロジェクト (`Pos.Domain` / `Pos.Contract`)

## Pos.Domain

- 業務ルールは `Pos.Domain.Logic` に `SalesLogic` / `ReturnLogic` / `TaxLogic` / `TransactionLogic` のようなクラスで置く (業務ごとのフォルダには分けない)。  
  列挙型は `Pos.Domain.Enums` (1 型 1 ファイル)、長さは `Pos.Domain.Length`
- Domain は I/O やフレームワークに依存しない (`Usa.Smart.Core` 程度)
- Domain は文言を持たず、エラーは `RuleReason` だけを返す (文言はサーバと端末が付ける)
- 文字列の長さ (DB の列長、Request の `MaxLength`、フォームの `MaximumLength`) と入力桁数 (端末の電卓) は `Length` の定数を使い、数値を直接書かない

## Pos.Contract

- 通信データは `XxxRequest` / `XxxResponse`。  
  名前はエンドポイントのクラス名 + メソッド名 (`TransactionCreateRequest`、`CustomerPointHistoryResponse`、`ReportSalesSummaryResponse`)
- 一覧は `XxxResponse` で要素は `XxxResponseItem`。  
  入れ子の要素は親の名前に要素名を続ける (`TransactionResponseItemLine`)
- 資源ごとに名前空間とフォルダを分ける (`Pos.Contract.Customers` など)。  
  在庫調整理由のように独立した資源は独自の名前空間とエンドポイントを持つ。  
  `ListResponse<T>` は直下
- Request の検証属性 (`Required` / `MaxLength` / `Range`) の値は `Length` の定数から取る
- 契約でないもの (`JsonDateTimeConverter` / `ProblemResponse`) は Contract に置かず、サーバと端末がそれぞれ持つ
- Contract の型に対する判定 (`IsReturnable` など) は、その型と同じファイルの拡張メソッドにする
