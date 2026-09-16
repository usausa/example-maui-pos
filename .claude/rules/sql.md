---
paths:
  - "**/*.sql"
---
# 2-way SQL の書き方

## ファイル

- 2-way SQL は `Sql/{Accessor}.{Method}.sql`。ファイル名は Accessor のメソッド名に合わせる (`CustomerAccessor.QueryListAsync.sql`)
- DDL は 2-way SQL ではなく外部ファイル (サーバは `Host/Assets/Data/Schema.sql`、端末は `Resources/Raw/Schema.sql`) に `CREATE TABLE IF NOT EXISTS` と `CREATE INDEX IF NOT EXISTS` をまとめ、起動時に `ExecuteSchemaAsync` (`[DirectSql]`) で実行する。初期データも同様に `Host/Assets/Data/InitialData.sql` (複数の `INSERT`。`@now` は投入時刻)

## メソッド名 (= ファイル名)

DB の操作で付ける。取消・精算のような業務の動詞は Service / Usecase の名前にする。

| 操作 | 名前 | 例 |
| --- | --- | --- |
| 件数 | `CountXxx` | `Count`、`CountProducts` |
| 1 件取得 | `QueryXxx` (キー以外の条件は `QueryXxxByCode`) | `Query`、`QueryStore`、`QueryByCode` |
| 複数件取得 | `QueryXxxList` (条件は `QueryXxxListByProduct`、全件は `QueryXxxAll`) | `QueryList`、`QueryStoreList`、`QueryLineList`、`QueryStoreAll` |
| 集計 | `QueryXxxTotals` / `QueryXxxSummary` | `QueryTotals`、`QuerySalesSummary` |
| 登録 | `InsertXxx` | `Insert`、`InsertLine` |
| 更新 | `UpdateXxx` (状態を変える更新は結果の状態で `UpdateXxxed`) | `Update`、`UpdateVoided`、`UpdateClosed` |
| 加減算 | `AddXxx` | `AddPoints`、`AddQuantity` |
| 登録または更新 | `UpsertXxx` | `UpsertSyncState` |
| 削除 (論理削除も) | `DeleteXxx` | `Delete`、`DeleteStore` |
| PRAGMA・スクリプトの実行 | `ExecuteXxx` | `ExecutePragma`、`ExecuteSchema`、`ExecuteScript` |

## 書き方

- 句 (`SELECT` / `FROM` / `WHERE` / `GROUP BY` / `ORDER BY` / `UPDATE` / `SET` / `INSERT INTO` / `VALUES` / `RETURNING` / `LIMIT`) は行頭に置き、表名・列・条件・値は次の行に 4 桁字下げする。`AND` / `OR` と `JOIN` は字下げした位置の行頭
- パラメータは `/*@ name */` (メソッドの引数名)。後ろのリテラル (`''` / `0` / `1`) は SQL を単体で実行できるようにするための仮の値で、型に合わせる
- 条件の有無は `/*% if (x != null) { */` … `/*% } */` で切り替える。条件がすべて任意のときは `WHERE` に `1 = 1` を置く
- 並び替えの列や集計の軸は列挙型 (列挙名 = 列名) を `/*# sort */` で展開する。`DESC` や差分同期の `UpdatedAt, Id` のような固定の並びは `/*% if (desc) { */` … `/*% } else { */` … `/*% } */` の分岐で書き、文字列を組み立てて渡さない
- `/*# */` の生 SQL は次の 1 トークンだけを置き換える。呼べるのは `SqlHelper` の断片だけで、`/*!helper */` で参照する
- 更新は `UPDATE ... SET ... WHERE ... RETURNING *` で更新後の行を返す。`Version = Version + 1` と `AND Version = /*@ version */0` で楽観ロック、`AND IsDeleted = 0` で論理削除済みを除く
- 一覧は `CountXxx` と `QueryXxxList` の対で、`LIMIT /*@ limit */20 OFFSET /*@ offset */0`
- 例:

  ```sql
  UPDATE
      Customers
  SET
      Code = /*@ code */'',
      Name = /*@ name */'',
      UpdatedAt = /*@ updatedAt */'',
      Version = Version + 1
  WHERE
      Id = /*@ id */''
      AND Version = /*@ version */0
      AND IsDeleted = 0
  RETURNING
      *
  ```
