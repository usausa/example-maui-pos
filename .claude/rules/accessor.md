---
paths:
  - "server/src/Pos.Server.Core/Accessors/**"
  - "server/src/Pos.Server.Core/Models/Entity/**"
  - "server/src/Pos.Server.Core/Infrastructure/Data/**"
  - "terminal/Pos.Terminal/Services/DataAccessor.cs"
  - "terminal/Pos.Terminal/Services/DataProfile.cs"
  - "terminal/Pos.Terminal/Models/Entity/**"
  - "terminal/Pos.Terminal/Helpers/Data/**"
  - "terminal/Pos.Terminal/Services/Sql/**"
  - "terminal/Pos.Terminal/Services/DatabaseService.cs"
  - "terminal/Pos.Terminal/Resources/Raw/Schema.sql"
  - "server/src/Pos.Server.Core/Services/DatabaseService.cs"
  - "server/src/Pos.Server.Host/Assets/Data/Schema.sql"
  - "server/src/Pos.Server.Host/Assets/Data/InitialData.sql"
---
# Accessor (Smart.Data.Accessor)

SQL ファイルの書き方とファイル名は sql.md に置く。

## 共通

- メソッド名は DB の操作で付ける。取消、精算のような業務の動詞は Service と Usecase の名前にする
- `By{…}` は、キーでない条件や集計の軸を名前に出すときに付ける (`QueryByCode`、`QueryListByIds`、`QuerySalesSummaryByHour`、`UpdateReceivedByReceiptId`)。親の ID で選ぶ子の一覧と件数 (`QueryLineList`、`CountCategoryProducts`) には付けない

| 操作 | 名前 (末尾の `Async` は省略) | 例 |
| --- | --- | --- |
| 件数 | `CountXxx` | `Count`、`CountProducts` |
| 1 件取得 | `QueryXxx` (状態で絞った 1 件は `QueryCurrentXxx`) | `Query`、`QueryStore`、`QueryByCode`、`QueryCurrent`、`QueryCurrentShift` |
| 複数件取得 | `QueryXxxList` (ページングしない一覧も List) | `QueryList`、`QueryStoreList`、`QueryLineList` |
| ページングする一覧の全件版 | `QueryXxxAll` | `QueryStoreAll` |
| 集計 | `QueryXxxSummary` (同じ集計を軸で分けた版は `QueryXxxSummaryBy{軸}`、親 (シフト、締め) の中を軸で集めたものは `Query{軸}Summary`) | `QuerySalesSummary`、`QuerySalesSummaryByHour`、`QueryPaymentMethodSummary` |
| 登録 | `InsertXxx` | `Insert`、`InsertLine` |
| 更新 | `UpdateXxx` (状態を変える更新は結果の状態で `UpdateXxxed`) | `Update`、`UpdateVoided`、`UpdateClosed` |
| 加減算 | `AddXxx` | `AddPoints`、`AddQuantity` |
| 登録または更新 | `UpsertXxx` | `UpsertSyncState` |
| 削除 (論理削除も) | `DeleteXxx` | `Delete`、`DeleteStore` |
| 後から増えた列の追加 | `EnsureXxx` | `EnsurePaymentMethodShortName` |
| 既存の行の値の補完 | `BackfillXxx` | `BackfillPaymentMethodShortNames` |
| PRAGMA・スクリプトの実行 | `ExecuteXxx` | `ExecutePragma`、`ExecuteSchema`、`ExecuteScript` |

- Accessor は `[DataAccessor]` と `[ExecuteConfig(typeof(DataProfile))]` を付けた `sealed partial class` にする (`ExecuteConfig` を付け忘れると型の変換が効かない)
- Entity はキーの列に `[Key]` を付ける (複合キーはすべての列)
- キーによる 1 件の取得と物理削除は `[SelectSingle]` と `[Delete]` の Builder を使い、SQL ファイルを書かない (Entity を持たない表 (商品画像)、キーを引数に取らない 1 行の表 (会社設定)、値だけを取る取得 (端末の同期の状態) は SQL ファイルで書く)
- 普通の登録は `[Insert]` の Builder にし、採番、条件付き、冪等の登録 (`INSERT ... SELECT`、`ON CONFLICT`) だけを SQL ファイルで書く
- スキーマと初期データの SQL (複数文) を引数で受けてそのまま流すメソッドは `[DirectSql]` を付ける (2-way SQL のファイルは置かない)
- トランザクションの中で呼ぶメソッドは先頭の引数で `DbTransaction tx` を受ける
- 起動時に開いた接続で流す処理 (PRAGMA、列の追加、スキーマの実行、端末の同期の状態の削除) は先頭の引数で `DbConnection con` を受ける (サーバの値の補完と、後から足した初期データの登録は受けない)
- 同じ SQL をトランザクションの中と外の両方で使うときは、先頭に `DbTransaction tx` を足した同じ名前の多重定義にして、SQL ファイルを共有する (Builder のメソッドは多重定義にできないので、用途の名前にする)
- 戻り値の 0 や null が何を意味するか (先に変えられた、見つからない) は、メソッドのコメントに書く (同じ意味のメソッドが多い Accessor (`MasterAccessor`) は、クラスのコメントにまとめてよい)
- DB に保存する列挙型と日付の変換は `DataProfile` に登録する (列挙型は `EnumTextConverter<T>` で文字列にする。新しい列挙型も登録する)
- 機能は新しい表で足し、既存の表への列の追加は最後の手段にする
- 後から増えた列は `SchemaHelper.EnsureColumnAsync` で起動時に足して既存の DB を壊さず (値の補いが要るときは、サーバは `BackfillXxx`、端末は全件の同期のやり直し)、スキーマの SQL ファイルの `CREATE TABLE` にも書く

## サーバ

- Accessor は処理の単位でまとめる。会社設定と小さなマスタ (店舗、端末、スタッフ、部門、税率、値引、支払方法、理由、仕入先) は `MasterAccessor` に節で分けて置き、検索条件の多い資源 (商品、会員) とそれ以外は資源ごとの Accessor にする
- テーブルに紐付かない処理 (PRAGMA、スキーマと初期データの SQL ファイルの実行) と、既存の DB を補う処理 (列の追加、値の補完、後から足した初期データの登録) は `GenericAccessor` に置き、`DatabaseService.InitializeAsync` から起動時に流す
- テーブル名は Entity クラスの `[Name("Stores")]` で持ち、Builder 属性 (`[SelectSingle]`、`[Insert]`、`[Delete]`) に `Table` を書かない
- サーバの `SqlHelper` には、2-way SQL の `/*# */` から呼ぶ SQL 断片だけを置く
- 1 件の書き込みも Service がトランザクションを張るとき (取込と同じ形の商品の登録と更新) は、`tx` を受ける形だけでよい
- 更新後の行を使う更新は `UPDATE ... RETURNING *` を `[QueryFirst]` で受け、更新してから読み直さない (読み直す間に削除される余地がある)
- 更新の引数は列ごとに渡す (`/*@ entity.Prop */` にはコンバータが効かない)
- 日付は `DateOnlyTextConverter`、日時は `DateTimeTextConverter` で文字列にする
- 差分同期の一覧は `updatedSince` と `includeDeleted` を受けて `UpdatedAt` で絞り、ページングする一覧は `UpdatedAt, Id` の順に返す (ページングしない少数のマスタは画面の並び (`SortOrder, Code`)。在庫は `includeDeleted` を受けず、`UpdatedAt, StoreId, ProductId` の順)

## 端末 (ローカル DB)

- マスタと在庫は `Pos.Contract` の Response をそのままエンティティにする (この場合だけ Builder 属性に `Table` を書く)。それ以外のエンティティはテーブル名を `[Name]` で持つ
- Contract の Response (マスタ、在庫) には `[Key]` がないので、キーによる取得と削除は SQL ファイルで書く
- ローカル DB は端末だけが使うので、更新は `RETURNING` と `Version` を使わず件数を返す
- 一覧はページングしない。件数が増える一覧 (商品、取引、シフト、Outbox) だけ `LIMIT` を付ける
- 日時は UTC の ticks (INTEGER、`DateTimeTicksConverter`)、日付は TEXT で持つ
- 列を足したときに既存の行を埋めるには、同期の `ServerTimeKey` を消して全件同期し直す (在庫は `InventorySyncKey` で別に差分を取るので、在庫を取り直すにはそのキーを消す)
