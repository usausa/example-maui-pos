# 全体と共有プロジェクト

## 全体

- SQL は Accessor の 2-way SQL と、スキーマ・初期データの SQL ファイルだけに書く (2-way SQL が取り込むサーバの `SqlHelper` の断片、後から増えた列を足す `SchemaHelper`、保存形式を確かめるテストは除く)
- フォルダの直下には共通の部品だけを置き、1 つの機能に属するもの (機能のモデル、名称の辞書、絞り込みの状態、URL、帳票) は機能のサブフォルダか専用のフォルダに置く (型の種類で分けたフォルダ (`Models/Entity`、`Models/Views` など) と、領域の規則が置き場所を決めたもの (端末の `XxxBuilder`) は除く)
- `XxxBuilder` はテキストや画像 (帳票、レシート) の組み立てだけに使い、データの変換や計算のクラスには付けない
- モデルと列挙型への拡張メソッドは、複数の条件を意味でまとめた判定 (`IsReturnable`) だけにする。単一の値との比較 (`status == TransactionStatus.Voided`) は拡張メソッドにしない
- 判定の拡張メソッドの静的クラスは、対象の型と同じファイルに置く (フレームワークの型 (`NetworkAccess`) への判定は、それを使う型のファイルに置く)
- 例外として、管理画面の表示用の加工 (書式の `ToXxxText`、列挙型の文言の `ToDisplayName`) は、サーバの `Application/ViewExtensions` に拡張メソッドとしてまとめる
- 例外として、列挙型を通信の文字列 (UPPER_SNAKE_CASE) にする `ToCode` は `ErrorCodeExtensions` にまとめる
- `ArgumentNullException.ThrowIfNull` は書かない (CA1062 は無効)
- 「DTO」という語はコード・名前空間・文書に使わない
- 文字列の長さと入力の桁数は `Length` に定数を宣言して使い、数値を直接書かない (Request の `MaxLength`、フォームの `MaximumLength`、電卓の桁数)。`Range` の境界と `MinLength(1)` は数値でよい
- 業務のコードを足したら、確かめと文言を同時に足す (既定の分岐があるので、漏れてもビルドは通る)。`ErrorCode` は `ErrorCodeTests`、`WarningCode` は `ErrorCodeTests` とサーバの `ApiRuleText` (端末は警告を出さない)、`RuleReason` は `ApiRuleText` と `ViewHelper` に足す
- UI の文言は日本語だけにする (ローカライズ資源は持たない)
- 画面の文言 (端末・管理画面の表示、シート、トースト、入力の検証) は「。」が要らない短い句にし、文を重ねず、最後に「。」を付けない (帳票は対象外)
- 画面の作りやボタン・F キーの文言でわかる自明な案内 (端末の「QR を枠に合わせてください」「読み取ると登録します」、管理画面の「[保存] で反映します」のような手順や操作の説明、ヒントの帯) を出さない
- 説明や知らせの文言は、異常や注意が要る状態 (読めない、見つからない、取得できない、権限がない、要確認)、画面からわからない業務の規則 (役割でできること、登録できる件数、入力の決まり)、操作の結果や事実 (追加した商品、「〜がありません」、取消できなくなる) を伝えるために使う
- 確認は「〜しますか？」にし、取り消しにくい影響を短い句で 1 つだけ添える (端末は次の行、管理画面は「？」の後に空白を置いて続ける)
- 確かめる値を見せる確認 (精算) は、値を 1 行ずつ質問の前に並べる (注意は先頭に置く)
- 画面で文言に添える記号 (見出し、一覧と表のセル、メニュー、知らせ、端末のホームと設定のボタンの文言) は、明るい背景では色付きの絵文字にする
- 標準で文字の形になる記号 (ℹ、⚙、↩、▶、⏸、⚠、☁、🏷、🗑 など) を絵文字にするときは U+FE0F を付ける
- 濃い背景 (塗りつぶしのチップ、色付きのボタン、濃い色の帯) の記号は色付きの絵文字にせず、白い単色の記号にする (絵文字は背景に溶ける)
- 部品のアイコン (チップの `Icon`、ボタンの `ImageSource` と `StartIcon`、アイコンだけのボタン、管理画面のナビと KPI のカード) は Material Icons のグリフにする
- 帳票には絵文字を使わない (フォントに依存する)

## Pos.Domain

- 業務ルールは `Pos.Domain.Logic` に `SalesLogic`、`ReturnLogic` のような業務ごとの静的クラスで置く (フォルダには分けない)
- 列挙型は `Pos.Domain.Enums` に 1 型 1 ファイルで置く (通信データで使う列挙型も含む)
- Domain は I/O やフレームワークに依存せず、パッケージは `Usa.Smart.Core` だけを参照する (分析器を除く)
- Logic は時刻を引数で受ける (`DateTime.Now` を使わない)
- 検証は `ValidateXxx` が `RuleError?` (null なら正常。取引は `TransactionValidation`) を返す。入力の検証 (`TransactionLogic.ValidateInput`) だけは誤りの一覧を返す
- DB の事実は、呼び出し側が `XxxFact` / `XxxContext` に詰めて渡す
- 明細を持つ計算 (販売、返品) は `XxxInput` を受けて `XxxResult` を返す (どちらも record)
- 業務ルールの違反は `RuleError` (`ErrorCode` + `RuleReason`) で返し、Domain に文言を置かない (文言は使う側が `RuleReason` から引く)

## Pos.Contract

- 通信データは `XxxRequest` / `XxxResponse`。名前は `Endpoints` を除いたクラス名 + ハンドラ名 (`HandleXxxAsync` の `Xxx`) にする (`TransactionCreateRequest`、`CustomerPointHistoryResponse`、`ReportSalesSummaryResponse`)
- 一覧は `ListResponse<T>` を継承し、要素は一覧の型の名前 + `Item` にする (`CategoryListResponse` の要素は `CategoryListResponseItem`)
- 1 件の取得・登録・更新は、一覧の要素の型 (`XxxListResponseItem`) を返す (集計を添える日次締めは `DailyClosingSummaryResponse`、一覧のない会社設定は `SettingsResponse`)
- 要素や Request の中の子要素 (明細、支払、配送など) は `Item` を重ねず、親の名前 + 要素名にする (`OrderListResponseLine`、`ShiftListResponseDenomination`、`TransactionCreateRequestLine`)
- 資源ごとに名前空間とフォルダを分ける (`Pos.Contract.Customers` など)。URL が他の資源の下にあっても (`/inventory/suppliers`)、一覧と登録・更新を持つ資源は独自の名前空間・フォルダ・エンドポイントにする
- Request と Response ごとにファイルを分け、子要素と一覧の要素の型は同じファイルに置く
- 通信データは `sealed class` と `{ get; set; }` で書き、null にならない参照型は `= default!`、省略できる一覧は `= []` にし、Request の null にならない参照型と一覧には `[Required]` を付ける (null を送られたら 400 にする。record、`required`、JSON の属性は使わない。JSON の名前は各側の設定で camelCase にする)
- 日付は `DateOnly` (`yyyy-MM-dd`)、日時は UTC の `DateTime` (`yyyy-MM-ddTHH:mm:ss.fffZ`)、列挙型は文字列にする
- 更新の Request は楽観ロックの `Version` を持つ
- 端末が記録する登録 (シフトの開設、入出金、取引、棚卸・調整、受注、前受金の受取と返金) は、端末が採番した `Id` (サーバと同じ `Guid.CreateVersion7()`) を持つ (会員の登録はサーバが採番する)
- 項目をまたぐ検証は Request の `IValidatableObject` で 400 にし、DB の事実が要る業務ルールは `Pos.Domain.Logic` で 422 にする
- 1 つの要求の中で重なってはいけない値 (明細・値引・支払の ID と番号、シリアル番号、精算の金種、受領の明細) は、Request の `IValidatableObject` で 400 にする (一意の制約の違反や辞書の例外の 500 にしない)
- 一覧を持つ Request は、`IValidatableObject` の先頭で一覧 (要素の中の一覧も含む) の null の要素 (`[null]`) を 400 にし、要素を読む検証の前に `yield break` する (項目の検証は null の要素を飛ばすので、要素を読む処理の例外の 500 になる。一覧そのものの null は `[Required]` で 400 になり、`Validate` は呼ばれない。要素の型は null を許さないので `Cast<object?>().Contains(null)` で確かめる)
- Contract には通信データと、その型への判定の拡張メソッドだけを置く。JSON の変換器と Problem Details の型は、使う側 (サーバ、端末、ツール) がそれぞれ持つ (テストは Problem Details の型だけを持つ)
