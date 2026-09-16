---
paths:
  - "**/*.sql"
---
# 2-way SQL の書き方

- ファイルは `Sql/{Accessor}.{Method}.sql`。  
  DDL は `{Accessor}.Create.sql` (端末は `DataAccessor.CreateTablesAsync.sql`) に `CREATE TABLE IF NOT EXISTS` と索引をまとめる
- 句 (`SELECT` / `FROM` / `WHERE` / `GROUP BY` / `ORDER BY` / `UPDATE` / `SET` / `INSERT INTO` / `VALUES` / `RETURNING` / `LIMIT`) は行頭に置き、表名・列・条件・値は次の行に 4 桁字下げする。  
  `AND` / `OR` と `JOIN` は字下げした位置の行頭
- パラメータは `/*@ name */` (メソッドの引数名)。  
  後ろのリテラル (`''` / `0` / `1`) は SQL を単体で実行できるようにするための仮の値で、型に合わせる
- 条件の有無は `/*% if (x != null) { */` … `/*% } */` で切り替える。  
  条件がすべて任意のときは `WHERE` に `1 = 1` を置く
- 並び替えの列や集計の軸は列挙型 (列挙名 = 列名) を `/*# sort */` で展開する。  
  `DESC` や差分同期の `UpdatedAt, Id` のような固定の並びは `/*% if (desc) { */` … `/*% } else { */` … `/*% } */` の分岐で書き、文字列を組み立てて渡さない
- `/*# */` の生 SQL は次の 1 トークンだけを置き換える。  
  呼べるのは `SqlHelper` の断片だけで、`/*!helper */` で参照する
- 更新は `UPDATE ... SET ... WHERE ... RETURNING *` で更新後の行を返す。  
  `Version = Version + 1` と `AND Version = /*@ version */0` で楽観ロック、`AND IsDeleted = 0` で論理削除済みを除く
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
