---
paths:
  - "**/*.sql"
---
# SQL の書き方

Accessor のメソッド名 (= 2-way SQL のファイル名) は accessor.md に置く。

## ファイル

- 2-way SQL は `Sql/{Accessor}.{Method}.sql` にする (`CustomerAccessor.QueryListAsync.sql`)
- スキーマ (サーバは `Host/Assets/Data/Schema.sql`、端末は `Resources/Raw/Schema.sql`) と初期データ (`Host/Assets/Data/InitialData.sql`) は、起動時に実行する SQL ファイルに書く

## 2-way SQL

- 句 (`SELECT` / `FROM` / `WHERE` / `GROUP BY` / `ORDER BY` / `UPDATE` / `SET` / `INSERT INTO` / `VALUES` / `DELETE FROM` / `RETURNING` / `UNION`) は行頭に置き、表名・列・条件・値は次の行に 4 桁字下げする。`AND` / `OR` と `JOIN` は字下げした位置の行頭
- `LIMIT` / `OFFSET` は値と同じ行に書く。`ON CONFLICT (キー) DO UPDATE SET` は 1 行にし、`列 = excluded.列` を次の行から字下げして並べる
- 1 行に収まる短い条件 (`AND (StoreId = /*@ storeId */'' OR StoreId IS NULL)`) は 1 行でよい。条件が 3 つ以上か行が長くなる (目安 120 桁) なら `AND (` の中で 1 条件 1 行にする
- 副問い合わせ・派生表も同じ目安で判断し、長ければ括弧の中で同じ書き方 (句を字下げした位置の行頭) にする。複数の表の件数・合計を 1 行にまとめるときは、長いスカラー副問い合わせを列に並べるより表ごとの派生表を `CROSS JOIN` する
- 内部結合は `JOIN` と書き、`INNER` を付けない
- パラメータは `/*@ name */` (メソッドの引数名)。後ろの仮の値は、SQL を単体で実行できるように型に合わせる (文字列は `''`、数値と真偽は `0` / `1`、BLOB と NULL を渡す値は `NULL`、IN の一覧は `IN /*@ ids */('')`、列挙値は列挙名の文字列、端末の日時は `0`)
- 条件の有無は `/*% if (x != null) { */` … `/*% } */` で切り替え、`/*% */` の行は字下げせず行頭に置く。条件がすべて任意のときは `WHERE` に `1 = 1` を置く
- 並び替えの列は、列挙名 = 列名の列挙型を `/*# sort */` で展開する。列名と一致しない軸 (売上集計の GROUP BY、商品別売上の並び) は、サーバの `SqlHelper` の関数を `/*!helper */` で参照して列にする
- `/*# */` の生 SQL は次の 1 トークンだけを置き換える
- `DESC` や差分同期の `UpdatedAt, Id` のような固定の並びは `/*% if (desc) { */` … `/*% } else { */` … `/*% } */` の分岐で書き、文字列を組み立てて渡さない
- LIKE には `ESCAPE '\'` を付ける (パターンは呼び出し側でエスケープ済み)
- decimal の引数を集計や副問い合わせの式と比べるときは `CAST(/*@ x */0 AS NUMERIC)` にする (Microsoft.Data.Sqlite は decimal を TEXT で束縛し、列の親和性がない比較では数値にならない)
- サーバの編集の更新は `UPDATE ... RETURNING *` で返し、`Version = Version + 1` と `AND Version = /*@ version */0` で楽観ロックし、論理削除のある表は `AND IsDeleted = 0` を付ける
- 状態を変える更新は、遷移元の状態を `AND Status = '...'` で条件にする
- 論理削除は `SET IsDeleted = 1, UpdatedAt = …, Version = Version + 1` と `AND IsDeleted = 0` で行い、件数で NotFound を判定する
- 店舗ごとの連番は 1 文の `INSERT ... SELECT COALESCE(MAX(Seq), 0) + 1 ... RETURNING *` で採番し、(親, Seq) の一意索引で守る
- サーバの一覧は `CountXxx` と `QueryXxxList` の対にし、`LIMIT /*@ limit */20 OFFSET /*@ offset */0` でページを取る
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

## スキーマと初期データ

- スキーマは `CREATE TABLE IF NOT EXISTS` で列名・型・NOT NULL を桁揃えし、主キー・一意・外部キーは表の末尾に制約として書く
- 索引は表の直後に空行なしで `CREATE INDEX IF NOT EXISTS IX_表_列` (一意は `CREATE UNIQUE INDEX IF NOT EXISTS UX_表_列`) と書く
- 端末に差分同期する表は `IsDeleted`、`CreatedAt`、`UpdatedAt`、`Version` と `IX_表_UpdatedAt` を持つ
- 初期データは `INSERT INTO` / 表 / (列) / `VALUES` の行に分けて 1 行 1 レコードで書き、日時は `@now`、ID は表の番号を入れた固定値 (`00000000-0000-0000-0001-…`) にする
