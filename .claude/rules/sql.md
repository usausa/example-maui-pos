---
paths:
  - "**/*.sql"
  - "server/src/Pos.Server.Core/Accessors/SqlHelper.cs"
---
# SQL の書き方

Accessor のメソッド名は accessor.md に置く。

## ファイル

- 2-way SQL は `Sql/{Accessor}.{Method}.sql` にし、ファイル名をメソッド名に合わせる (`CustomerAccessor.QueryListAsync.sql`)
- スキーマはサーバが `Host/Assets/Data/Schema.sql`、端末が `Resources/Raw/Schema.sql`、初期データは `Host/Assets/Data/InitialData.sql` に置く

## 2-way SQL

- 句 (`SELECT`、`FROM`、`WHERE`、`GROUP BY`、`ORDER BY`、`UPDATE`、`SET`、`INSERT INTO`、`VALUES`、`DELETE FROM`、`RETURNING`、`UNION`) は行頭に置き、表名・列・条件・値は次の行に 4 桁字下げする
- `AND`、`OR`、`JOIN` は字下げした位置の行頭に置く
- `LIMIT` と `OFFSET` は値と同じ行に書く
- `ON CONFLICT (キー) DO UPDATE SET` は 1 行にし、`列 = excluded.列` を次の行から字下げして並べる
- `INSERT INTO` の列の一覧は、表名と同じ行に収まれば (目安 80 桁) その行に、収まらなければ次の行に `(列, 列, …)` で 1 行に書く (列の一覧と `VALUES` の値の行は 120 桁を超えてよい)
- 1 行に収まる短い条件 (`AND (StoreId = /*@ storeId */'' OR StoreId IS NULL)`) は 1 行でよい。条件が 3 つ以上か行が長くなる (目安 120 桁) なら `AND (` の中で 1 条件 1 行にする
- 副問い合わせ・派生表も同じ目安で判断し、長ければ括弧の中で同じ書き方 (句を字下げした位置の行頭) にする
- 複数の表の件数・合計を 1 行にまとめるときは、長いスカラー副問い合わせを列に並べるより、表ごとの派生表を `CROSS JOIN` する
- 内部結合は `JOIN` と書き、`INNER` を付けない
- パラメータは `/*@ name */` (メソッドの引数名) で書く
- パラメータの後ろの仮の値は、SQL を単体で実行できるように型に合わせる (文字列と列挙型は `''`、数値は `0`、真偽は `1`、`LIMIT` は件数の目安 (`20` など)、IN の一覧は `IN /*@ ids */('')`、端末の日時は `0`、BLOB と、NULL であることに意味がある値 (`COALESCE` で既定に戻す値など) は `NULL`)
- 条件の有無は `/*% if (x != null) { */` … `/*% } */` で切り替え、`/*% */` の行は字下げせず行頭に置く。条件がすべて任意のときは `WHERE` に `1 = 1` を置く
- 句のキーワード (`WHERE`、`ORDER BY`) は `/*% */` の分岐の外に置き、分岐の中には条件と列だけを書く (副問い合わせを含む条件は分岐の中でよい)
- 並び替えの列は、列挙名 = 列名の列挙型を `/*# sort */` で展開する
- ページングする一覧は、`/*# sort */` の後に一意の列 (コード・番号、なければ `Id`) を同じ向きで足す (同じ値の行の順が決まらないと、ページの境目で重なったり抜けたりする)
- 列名と一致しない軸 (売上集計の GROUP BY、商品別売上の並び) は、サーバの `SqlHelper` を `/*!helper */` で取り込み、関数を `/*# GroupKey(groupBy) */` のように展開する
- `/*# */` の生 SQL は次の 1 トークンだけを置き換える
- `DESC` や差分同期の `UpdatedAt, Id` のような固定の並びは `/*% if (desc) { */` … `/*% } else { */` … `/*% } */` の分岐で書き、文字列を組み立てて渡さない
- LIKE には `ESCAPE '\'` を付ける (パターンは呼び出し側でエスケープ済み)
- 2-way SQL にはコメントを書かず、意図や制約はメソッドのコメントに書く
- decimal の引数を集計や副問い合わせの式と比べるときは `CAST(/*@ x */0 AS NUMERIC)` にする (Microsoft.Data.Sqlite は decimal を TEXT で束縛し、列の親和性がない比較では数値にならない)
- サーバの編集の更新は `UPDATE ... RETURNING *` で返し、`Version = Version + 1` と `AND Version = /*@ version */0` で楽観ロックし、論理削除のある表は `AND IsDeleted = 0` を付ける
- サーバの状態を変える更新は、遷移元の状態を `AND Status = '...'` で条件にし、業務ルールで確かめた、ほかの要求が変える記録 (取消の返品・精算・締め、受注の前受金の残り) も条件に入れる (検証のあとに先に通った要求を破らない。担当・承認者は入れない)
- 論理削除は `SET IsDeleted = 1, UpdatedAt = …, Version = Version + 1` と `AND IsDeleted = 0` で行い、件数で NotFound を判定する
- 店舗ごとの連番は 1 文の `INSERT ... SELECT COALESCE(MAX(Seq), 0) + 1 ... RETURNING *` で採番し、(親, Seq) の一意索引で守る
- サーバのページングする一覧は、件数と一覧の 2 つの SQL の対にし (名前は accessor.md)、一覧は `LIMIT /*@ limit */20 OFFSET /*@ offset */0` でページを取る
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
- 索引は表の直後に空行なしで `CREATE INDEX IF NOT EXISTS IX_表_列` (一意は `CREATE UNIQUE INDEX IF NOT EXISTS UX_表_列`) と書き、名前には索引の列をすべて入れる
- 入力されるキー (コード、名前、端末番号、レシート番号、取引の中の明細番号) の一意は表の制約 (`UNIQUE (...)`) にし、条件付きの一意 (NULL を除く、状態で絞る)、採番した番号 (店舗ごとの連番と番号)、業務ルールで重なりを防ぐ組 (店舗 × 営業日の締め) は一意索引にする
- 索引の名前を変えたら、新しい名前で作り、スキーマの末尾で古い名前を `DROP INDEX IF EXISTS` で消す (前の版の DB に残る)
- サーバの表のうち端末に差分同期する表は `IsDeleted`、`CreatedAt`、`UpdatedAt`、`Version` と `IX_表_UpdatedAt` を持つ (在庫の `InventoryLevels` は店舗ごとの `UpdatedAt` だけ、1 行の `Settings` は `UpdatedAt` だけで同期する)
- 初期データは `INSERT INTO`、表、(列)、`VALUES` をそれぞれの行に分け、1 行 1 レコードで書く
- 初期データの日時は `@now`、ID は表の番号を入れた固定値 (`00000000-0000-0000-0001-…`。1 行の `Settings` は `1`) にする
