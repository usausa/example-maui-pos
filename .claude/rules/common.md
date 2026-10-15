# 全体と共有プロジェクト

## 全体

- SQL は Accessor の 2-way SQL と、スキーマ・初期データの SQL ファイルだけに書く (後から増えた列を足す `SchemaHelper` と、保存形式を確かめるテストは除く)
- フォルダの直下には共通の部品だけを置き、1 つの機能に属するもの (機能のモデル、名称の辞書、絞り込みの状態、URL、帳票) は機能のサブフォルダか専用のフォルダに置く
- `XxxBuilder` はテキストや画像 (帳票、レシート) の組み立てだけに使う。データの変換は `XxxMapper`、計算は `XxxCalculator` にし、Builder を付けない
- モデルと列挙型への拡張メソッドは、複数の条件を意味でまとめた判定 (`IsReturnable`) だけにし、対象の型の宣言に続けて同じファイルに書く。単一の値との比較 (`status == TransactionStatus.Voided`) は拡張メソッドにしない
- 例外として、管理画面の表示用の加工 (書式の `ToXxxText`、列挙型の文言の `ToDisplayName`) は、サーバの `Application/ViewExtensions` に拡張メソッドとしてまとめる
- 例外として、列挙型を通信の文字列 (UPPER_SNAKE_CASE) にする `ToCode` は `ErrorCodeExtensions` にまとめる
- `ArgumentNullException.ThrowIfNull` は書かない (CA1062 は無効)
- 「DTO」という語はコード・名前空間・文書に使わない
- 文字列の長さと入力の桁数は `Length` に定数を宣言して使い、数値を直接書かない (Request の `MaxLength`、フォームの `MaximumLength`、電卓の桁数)。`Range` の境界と `MinLength(1)` は数値でよい
- 業務のコードを足したら、確かめと文言を同時に足す (既定の分岐があるので、漏れてもビルドは通る)。`ErrorCode` は `ErrorCodeTests`、`WarningCode` は `ErrorCodeTests` とサーバの `ApiRuleText` (端末は警告を出さない)、`RuleReason` は `ApiRuleText` と `ViewHelper` に足す
- UI の文言は日本語だけにする (ローカライズ資源は持たない)
- 画面の文言 (端末・管理画面の表示、シート、トースト、入力の検証) は「。」が要らない短い句にし、文を重ねず、最後に「。」を付けない (帳票は対象外)
- 確認は「〜しますか？」にし、取り消しにくい影響を短い句で 1 行だけ添える
- 画面で文言に添える記号 (見出し、一覧と表のセル、メニュー、知らせ、端末のホームと設定のボタンの文言) は、明るい背景では色付きの絵文字にする
- 標準で文字の形になる記号 (ℹ、⚙、↩、▶、⏸、⚠、☁、🏷、🗑 など) を絵文字にするときは U+FE0F を付ける
- 濃い背景 (塗りつぶしのチップ、色付きのボタン、濃い色の帯) の記号は色付きの絵文字にせず、白い単色の記号にする (絵文字は背景に溶ける)
- 部品のアイコン (チップの `Icon`、ボタンの `ImageSource` と `StartIcon`、アイコンだけのボタン、管理画面のナビと KPI のカード) は Material Icons のグリフにする
- 帳票には絵文字を使わない (フォントに依存する)

## Pos.Domain

- 業務ルールは `Pos.Domain.Logic` に `SalesLogic`、`ReturnLogic` のような業務ごとの静的クラスで置く (フォルダには分けない)
- 列挙型は `Pos.Domain.Enums` に 1 型 1 ファイルで置く (通信データで使う列挙型も含む)。判定の拡張メソッドの静的クラスは、同じファイルに入れる
- Domain は I/O やフレームワークに依存しない (`Usa.Smart.Core` 程度)
- Logic は時刻を引数で受ける (`DateTime.Now` を使わない)
- 検証は `ValidateXxx` が `RuleError?` (null なら正常。取引は `TransactionValidation`) を返す。入力の検証 (`TransactionLogic.ValidateInput`) だけは誤りの一覧を返す
- DB の事実は、呼び出し側が `XxxFact` / `XxxContext` に詰めて渡す
- 明細を持つ計算 (販売、返品) は `XxxInput` を受けて `XxxResult` を返す (どちらも record)
- 業務ルールの違反は `RuleError` (`ErrorCode` + `RuleReason`) で返し、Domain に文言を置かない (文言はサーバの `ApiRuleText`、端末の `ViewHelper` が `RuleReason` から引く)

## Pos.Contract

- 通信データは `XxxRequest` / `XxxResponse`。名前は `Endpoints` を除いたクラス名 + ハンドラ名 (`HandleXxxAsync` の `Xxx`) にする (`TransactionCreateRequest`、`CustomerPointHistoryResponse`、`ReportSalesSummaryResponse`)
- 一覧は `ListResponse<T>` を継承し、要素は一覧の型の名前 + `Item` にする (`CategoryListResponse` の要素は `CategoryListResponseItem`)
- 1 件の取得・登録・更新は、一覧の要素の型 (`XxxListResponseItem`) を返す (集計を添える日次締めは `DailyClosingSummaryResponse`、一覧のない会社設定は `SettingsResponse`)
- 要素や Request の中の子要素 (明細、支払、配送など) は `Item` を重ねず、親の名前 + 要素名にする (`OrderListResponseLine`、`ShiftListResponseDenomination`、`TransactionCreateRequestLine`)
- 資源ごとに名前空間とフォルダを分ける (`Pos.Contract.Customers` など)。URL が他の資源の下にあっても (`/inventory/suppliers`)、一覧と登録・更新を持つ資源は独自の名前空間・フォルダ・エンドポイントにする
- Request と Response ごとにファイルを分け、子要素と一覧の要素の型は同じファイルに置く
- 通信データは `sealed class` と `{ get; set; }` で書き、null にならない参照型は `= default!` にする (record、`required`、JSON の属性は使わない)
- 日付は `DateOnly` (`yyyy-MM-dd`)、日時は UTC の `DateTime` (`yyyy-MM-ddTHH:mm:ss.fffZ`)、列挙型は文字列にする
- JSON の名前は既定の camelCase に任せる
- 更新の Request は楽観ロックの `Version` を持つ
- 端末から送る登録は、端末が採番した `Id` (サーバと同じ `Guid.CreateVersion7()`) を持つ
- 項目をまたぐ検証は Request の `IValidatableObject` で 400 にし、DB の事実が要る業務ルールは `Pos.Domain.Logic` で 422 にする
- Contract には通信データと、その型への判定の拡張メソッドだけを置く。JSON の変換器や Problem Details の型は、サーバ・端末・ツールがそれぞれ持つ
