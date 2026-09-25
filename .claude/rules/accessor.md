---
paths:
  - "server/src/Pos.Server.Core/Accessors/**"
  - "terminal/Pos.Terminal/Services/DataAccessor.cs"
  - "terminal/Pos.Terminal/Services/DataProfile.cs"
  - "terminal/Pos.Terminal/Helpers/Data/**"
---
# Accessor (Smart.Data.Accessor)

SQL ファイルの書き方は sql.md に置く。

## 共通

- メソッド名は DB の操作で付け、2-way SQL のファイル名 (`{Accessor}.{Method}.sql`) をメソッド名に合わせる。取消・精算のような業務の動詞は Service / Usecase の名前にする

| 操作 | 名前 (末尾の `Async` は省略) | 例 |
| --- | --- | --- |
| 件数 | `CountXxx` | `Count`、`CountProducts` |
| 1 件取得 | `QueryXxx` (キー以外の条件は `QueryXxxBy{条件}`) | `Query`、`QueryStore`、`QueryByCode` |
| 複数件取得 | `QueryXxxList` (条件は `QueryXxxListBy{条件}`、ページングしない全件は `QueryXxxAll`) | `QueryList`、`QueryStoreList`、`QueryLineList`、`QueryStoreAll` |
| 集計 | `QueryXxxSummary` | `QuerySalesSummary` |
| 登録 | `InsertXxx` | `Insert`、`InsertLine` |
| 更新 | `UpdateXxx` (状態を変える更新は結果の状態で `UpdateXxxed`) | `Update`、`UpdateVoided`、`UpdateClosed` |
| 加減算 | `AddXxx` | `AddPoints`、`AddQuantity` |
| 登録または更新 | `UpsertXxx` | `UpsertSyncState` |
| 削除 (論理削除も) | `DeleteXxx` | `Delete`、`DeleteStore` |
| PRAGMA・スクリプトの実行 | `ExecuteXxx` | `ExecutePragma`、`ExecuteSchema`、`ExecuteScript` |

- キーによる取得と削除は `[SelectSingle]` / `[Delete]` の Builder を使い、SQL ファイルを書かない
- 状態を条件にした `[Execute]` の更新は戻り値 (更新した件数) を判定し、0 件 (先に別の要求が状態を変えた) なら続きの書き込みをしない
- 列挙型と日付の変換は `DataProfile` に登録する (列挙型は `EnumTextConverter<T>` で文字列にする。新しい列挙型も登録する)
- 後から増えた列は `SchemaHelper.EnsureColumnAsync` で起動時に足して初期値を補完し、既存の DB を壊さない。列はスキーマの SQL ファイルの `CREATE TABLE` にも書く

## サーバ

- Accessor は処理の単位でまとめる。会社設定と小さなマスタ (店舗、端末、スタッフ、部門、税率、値引、支払方法、理由、仕入先) は `MasterAccessor` に節で分けて置き、検索条件の多い資源 (商品、会員) とそれ以外は資源ごとの Accessor にする
- テーブルに紐付かない処理 (PRAGMA、後から増えた列、スキーマと初期データの SQL ファイルの実行) は `GenericAccessor` に置く
- テーブル名は Entity クラスの `[Name("Stores")]` で持ち、Builder 属性 (`[SelectSingle]` / `[Insert]` / `[Delete]`) に `Table` を書かない
- 更新後の行を使う更新は `UPDATE ... RETURNING *` を `[QueryFirst]` で受け、更新してから読み直さない (読み直す間に削除される余地がある)
- 更新の引数は列ごとに渡す (`/*@ entity.Prop */` にはコンバータが効かない)
- 日付・日時は `DateOnlyTextConverter` / `DateTimeTextConverter` で文字列にする
- `SqlHelper` に置くのは 2-way SQL の `/*# */` から呼ぶ SQL 断片 (集計の GROUP BY 式など) だけにする

## 端末 (ローカル DB)

- マスタは `Pos.Contract` の Response をそのままエンティティにする (この場合だけ Builder 属性に `Table` を書く)。それ以外のエンティティはテーブル名を `[Name]` で持つ
- ローカル DB は端末だけが使うので、更新は `RETURNING` と `Version` を使わず件数を返し、一覧はページングせず `LIMIT` だけにする
- 1 文だけの書き込みにはトランザクションを使わない
- 日時は UTC の ticks (INTEGER、`DateTimeTicksConverter`)、日付は TEXT で持つ
- 列を足したときに既存の行を埋めるには、同期の `ServerTimeKey` を消して全件同期し直す
