---
paths:
  - "**/*.sql"
  - "server/src/Pos.Server.Core/Accessors/SqlHelper.cs"
---
# SQL の書き方

## ファイル

- 2-way SQL は `Sql/{Accessor}.{Method}.sql` にし、ファイル名をメソッド名に合わせる (`CustomerAccessor.QueryListAsync.sql`)
- スキーマはサーバが `Host/Assets/Data/Schema.sql`、端末が `Resources/Raw/Schema.sql`、初期データは `Host/Assets/Data/InitialData.sql` に置く

## 2-way SQL

### 書式

- 句 (`SELECT`、`FROM`、`WHERE`、`GROUP BY`、`ORDER BY`、`UPDATE`、`SET`、`INSERT INTO`、`VALUES`、`DELETE FROM`、`RETURNING`、`UNION`) は行頭に置き、テーブル名・列・条件・値は次の行に 4 桁字下げする
- `AND`、`OR`、`JOIN` は字下げした位置の行頭に置く
- `LIMIT` と `OFFSET` は値と同じ行に書く
- `ON CONFLICT (キー) DO UPDATE SET` は 1 行にし、`列 = excluded.列` を次の行から字下げして並べる
- `INSERT INTO` の列の一覧は、テーブル名と同じ行に収まれば (目安 80 桁) その行に、収まらなければ次の行に `(列, 列, …)` で 1 行に書く
- `INSERT INTO` の列の一覧と `VALUES` の値の行は、120 桁を超えてよい
- 1 行に収まる短い条件 (`AND (StoreId = /*@ storeId */'' OR StoreId IS NULL)`) は 1 行で書いてよい
- `AND (` でまとめる条件が 3 つ以上か、行が長くなる (目安 120 桁) ときは、括弧の中で 1 条件 1 行にする
- サブクエリと派生テーブルも同じ目安で判断し、長ければ括弧の中で同じ書き方 (句を字下げした位置の行頭に置く) にする
- 複数のテーブルの件数・合計を 1 行にまとめるときは、長いスカラーサブクエリを列に並べるより、テーブルごとの派生テーブルを `CROSS JOIN` する
- 内部結合は `JOIN` と書き、`INNER` を付けない
- 2-way SQL にはコメントを書かず、意図や制約はメソッドのコメントに書く

### パラメータと条件

- パラメータは `/*@ name */` (メソッドの引数名) で書く
- パラメータの後ろの仮の値は、SQL を単体で実行できるように型に合わせる

| パラメータ | 仮の値 |
| --- | --- |
| 文字列、列挙型 | `''` |
| 数値 | `0` |
| 真偽 | `1` |
| `LIMIT` | 件数の目安 (`20` など) |
| IN の一覧 | `IN /*@ ids */('')` |
| 端末の日時 | `0` |
| BLOB、NULL であることに意味がある値 (`COALESCE` で既定に戻す値など) | `NULL` |

- 条件の有無は `/*% if (x != null) { */` … `/*% } */` で切り替える
- `/*% */` の行は字下げせず行頭に置く
- 条件がすべて任意のときは `WHERE` に `1 = 1` を置く
- 句のキーワード (`WHERE`、`ORDER BY`) は `/*% */` の分岐の外に置き、分岐の中には条件と列だけを書く (サブクエリを含む条件は分岐の中でよい)
- LIKE には `ESCAPE '\'` を付ける (パターンは呼び出し側でエスケープ済み)
- decimal の引数を集計やサブクエリの式と比べるときは `CAST(/*@ x */0 AS NUMERIC)` にする (Microsoft.Data.Sqlite は decimal を TEXT でバインドし、列の親和性がない比較では数値にならない)

### 並びとページング

- 並び替えの列は、列挙名 = 列名の列挙型を `/*# sort */` で展開する
- ページングする一覧は、`/*# sort */` の後に一意の列 (コードか番号、なければ `Id`) を同じ向きで足す (同じ値の行の順が決まらないと、ページの境目で重なったり抜けたりする)
- 列名と一致しない軸 (売上集計の GROUP BY、商品別売上の並び) は、サーバの `SqlHelper` を `/*!helper */` で取り込み、関数を `/*# GroupKey(groupBy) */` のように展開する
- `/*# */` の生 SQL は次の 1 トークンだけを置き換える
- `DESC` や差分同期の `UpdatedAt, Id` のような固定の並びは `/*% if (desc) { */` … `/*% } else { */` … `/*% } */` の分岐で書き、文字列を組み立てて渡さない
- サーバのページングする一覧は、件数と一覧の 2 つの SQL の対にする (名前は accessor.md)
- 件数と一覧の対のうち、一覧の SQL は `LIMIT /*@ limit */20 OFFSET /*@ offset */0` でページを取る

### 更新

- サーバの編集の更新は、`UPDATE ... RETURNING *` で更新後の行を返す
- サーバの編集の更新は、`Version = Version + 1` と `AND Version = /*@ version */0` で楽観ロックする
- サーバの編集の更新は、論理削除のあるテーブルでは `AND IsDeleted = 0` を付ける
- サーバの状態を変える更新は、遷移元の状態を `AND Status = '...'` で条件にする
- サーバの状態を変える更新は、業務ルールで確認した、ほかのリクエストが変える記録 (取消の返品・精算・締め、受注の前受金の残り) も条件に入れる (検証のあとに先に通ったリクエストを破らない)
- 担当・承認者は、サーバの状態を変える更新の条件に入れない
- サーバの記録への加減算も、業務ルールで確認した、ほかのリクエストが変える事実を条件に入れる (元の明細に返品済みの数量を足すのは、元の取引が完了のときだけ)
- 論理削除は `SET IsDeleted = 1, UpdatedAt = …, Version = Version + 1` と `AND IsDeleted = 0` で行い、件数で NotFound を判定する
- 店舗ごとの連番は 1 文の `INSERT ... SELECT COALESCE(MAX(Seq), 0) + 1 ... RETURNING *` で採番し、(親, Seq) の一意インデックスで守る
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

- スキーマは `CREATE TABLE IF NOT EXISTS` で書き、列名・型・NOT NULL を桁揃えする
- 主キー・一意・外部キーは、テーブルの末尾に制約として書く
- インデックスはテーブルの直後に空行なしで `CREATE INDEX IF NOT EXISTS IX_テーブル_列` (一意は `CREATE UNIQUE INDEX IF NOT EXISTS UX_テーブル_列`) と書く
- インデックスの名前には、インデックスの列をすべて入れる
- 入力されるキー (コード、名前、端末番号、レシート番号、取引の中の明細番号) の一意は、テーブルの制約 (`UNIQUE (...)`) にする
- 条件付きの一意 (NULL を除く、状態で絞る)、採番した番号 (店舗ごとの連番と番号)、業務ルールで重なりを防ぐ組 (店舗 × 営業日の締め) は、一意インデックスにする
- インデックスの名前を変えたら、新しい名前で作り、スキーマの末尾で古い名前を `DROP INDEX IF EXISTS` で消す (前のバージョンの DB に残る)
- 端末に差分同期するサーバのテーブルは、`IsDeleted`、`CreatedAt`、`UpdatedAt`、`Version` と `IX_テーブル_UpdatedAt` を持つ (在庫の `InventoryLevels` は店舗ごとの `UpdatedAt` だけ、1 行の `Settings` は `UpdatedAt` だけで同期する)
- 初期データは `INSERT INTO`、テーブル、(列)、`VALUES` をそれぞれの行に分け、1 行 1 レコードで書く
- 初期データの日時は `@now` にする
- 初期データの ID は、テーブルの番号を入れた固定値 (`00000000-0000-0000-0001-…`) にする (1 行の `Settings` は `1`)
