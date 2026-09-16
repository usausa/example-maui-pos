# 全体と共有プロジェクト

## 全体

- モノレポ。`server/Pos.Server.slnx` (ASP.NET Core) と `terminal/Pos.Terminal.slnx` (MAUI) は別々に開き、どちらも `shared/` (`Pos.Domain` / `Pos.Contract`) を含む。構成は `docs/architecture.md`
- SQL は Accessor だけが持つ。業務の手順はサーバは `Services/` の `XxxService`、端末は `Usecases/` の `XxxUsecase` に置き、Endpoints / Blazor ページ / ViewModel は入力の検証と表示だけを担う
- フォルダ直下には共通の部品だけを置く。機能に属するもの (名称の辞書、絞り込みの状態、URL の生成、帳票) はサブフォルダか専用のフォルダに、あるフォルダの中でしか使わないものはそのフォルダに置く
- 1 つの Service だけが返す結果型 (`XxxResult`) は、その Service のファイルの先頭で定義する。複数で使う型 (`DataWriteStatus` / `DataWriteResult<T>`) は独自のファイルにする
- `Builder` はテキストや画像の組み立てだけに使う。データの変換は `XxxMapper`、計算は `XxxCalculator`
- 拡張メソッドは複数の項目を意味でまとめて判定するもの (`IsReturnable`) だけに使い、型と同じファイルに書く。単一の値との比較 (`status == TransactionStatus.Voided`) は拡張メソッドにしない
- 文言が入り得るプロパティは `EmptyText` ではなく `Message`
- `ArgumentNullException.ThrowIfNull` は書かない (CA1062 は無効)
- 「DTO」という語はコード・名前空間・文書に使わない
- UI の文言は日本語だけ (ローカライズ資源は持たない)

## Pos.Domain

- 業務ルールは `Pos.Domain.Logic` に `SalesLogic` / `ReturnLogic` / `TaxLogic` / `TransactionLogic` のようなクラスで置く (業務ごとのフォルダには分けない)。列挙型は `Pos.Domain.Enums` (1 型 1 ファイル)、長さは `Pos.Domain.Length`
- Domain は I/O やフレームワークに依存しない (`Usa.Smart.Core` 程度)
- Domain は文言を持たず、エラーは `RuleReason` だけを返す (文言はサーバと端末が付ける)
- 文字列の長さ (DB の列長、Request の `MaxLength`、フォームの `MaximumLength`) と入力桁数 (端末の電卓) は `Length` の定数を使い、数値を直接書かない

## Pos.Contract

- 通信データは `XxxRequest` / `XxxResponse`。名前はエンドポイントのクラス名 + メソッド名 (`TransactionCreateRequest`、`CustomerPointHistoryResponse`、`ReportSalesSummaryResponse`)
- 一覧は `XxxResponse` で要素は `XxxResponseItem`。入れ子の要素は親の名前に要素名を続ける (`TransactionResponseItemLine`)
- 資源ごとに名前空間とフォルダを分ける (`Pos.Contract.Customers` など)。在庫調整理由のように独立した資源は独自の名前空間とエンドポイントを持つ。`ListResponse<T>` は直下
- JSON は camelCase、`null` のプロパティは省略、列挙型は文字列、UTC の日時は `yyyy-MM-ddTHH:mm:ss.fffZ`
- Request の検証属性 (`Required` / `MaxLength` / `Range`) の値は `Length` の定数から取る
- 契約でないもの (`JsonDateTimeConverter` / `ProblemResponse`) は Contract に置かず、サーバと端末がそれぞれ持つ
- Contract の型に対する判定 (`IsReturnable` など) は、その型と同じファイルの拡張メソッドにする
