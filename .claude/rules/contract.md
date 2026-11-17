---
paths:
  - "shared/Pos.Contract/**"
---
# Pos.Contract

## 名前

- 通信データの名前は `XxxRequest` / `XxxResponse` にし、`Xxx` は `Endpoints` を除いたクラス名 + ハンドラ名 (`HandleXxxAsync` の `Xxx`) にする (`TransactionCreateRequest`、`CustomerPointHistoryResponse`、`ReportSalesSummaryResponse`)
- 一覧の Response は `ListResponse<T>` を継承する
- 一覧の要素の型は、一覧の型の名前 + `Item` にする (`CategoryListResponse` の要素は `CategoryListResponseItem`)
- 1 件の取得・登録・更新は、一覧の要素の型 (`XxxListResponseItem`) を返す (集計を添える日次締めは `DailyClosingSummaryResponse`、一覧のない会社設定は `SettingsResponse`)
- 要素や Request の中の子要素 (明細、支払、配送など) は `Item` を重ねず、親の名前 + 要素名にする (`OrderListResponseLine`、`ShiftListResponseDenomination`、`TransactionCreateRequestLine`)

## ファイルと名前空間

- リソースごとに名前空間とフォルダを分ける (`Pos.Contract.Customers` など)
- URL がほかのリソースの下にあっても (`/inventory/suppliers`)、一覧と登録・更新を持つリソースは独自の名前空間・フォルダ・エンドポイントにする
- Request と Response ごとにファイルを分け、子要素と一覧の要素の型は同じファイルに置く
- Contract には通信データと、その型への判定の拡張メソッドだけを置く
- JSON のコンバータと Problem Details の型は、使う側 (サーバ、端末、ツール) がそれぞれ持つ (テストは Problem Details の型だけを持つ)

## 型の定義

- 通信データは `sealed class` と `{ get; set; }` で書く (record と `required` は使わない)
- null にならない参照型は `= default!`、省略できる一覧は `= []` にする
- Request の null にならない参照型と一覧には `[Required]` を付ける (null を送られたら 400 にする)
- JSON の属性は使わず、JSON の名前は各側の設定で camelCase にする
- 日付は `DateOnly` (`yyyy-MM-dd`)、日時は UTC の `DateTime` (`yyyy-MM-ddTHH:mm:ss.fffZ`)、列挙型は文字列にする
- 更新の Request は楽観ロックの `Version` を持つ
- 端末が記録する登録 (シフトの開設、入出金、取引、棚卸・調整、受注、前受金の受取と返金) は、端末が採番した `Id` (サーバと同じ `Guid.CreateVersion7()`) を持つ (会員の登録はサーバが採番する)

## 検証

- 項目をまたぐ検証は Request の `IValidatableObject` で 400 にし、DB の事実が要る業務ルールは `Pos.Domain.Logic` で 422 にする
- 1 つのリクエストの中で重なってはいけない値 (明細・値引・支払の ID と番号、シリアル番号、精算の金種、受領の明細) は、Request の `IValidatableObject` で 400 にする (一意の制約の違反や辞書の例外の 500 にしない)
- 一覧を持つ Request は、`IValidatableObject` の先頭で一覧 (要素の中の一覧も含む) の null の要素 (`[null]`) を 400 にし、要素を読む検証の前に `yield break` する (項目の検証は null の要素を飛ばすので、要素を読む処理の例外で 500 になる)
- 一覧そのものの null は `[Required]` で 400 になり、`Validate` は呼ばれない
- null の要素は `Cast<object?>().Contains(null)` で確認する (要素の型は null を許さない)
