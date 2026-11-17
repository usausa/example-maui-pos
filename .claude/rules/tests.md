---
paths:
  - "shared/Pos.Domain.Tests/**"
  - "server/tests/**"
  - "server/src/Pos.Server.Host/Assets/Data/InitialData.sql"
---
# テスト

## 共通

- xunit v3 にする
- キャンセルはクラスに `private static CancellationToken Token => TestContext.Current.CancellationToken;` を置いて渡す
- テストは実行順に依存させない (実行順は環境によって変わる)
- 同じフィクスチャ (DB) を使うテストが登録・削除した行を、件数の検証に含めない
- テスト名はアンダースコアなしの PascalCase (`SyncMastersReturnsAllThenDelta`) にする
- テストの目的は、メソッドの上に日本語のコメントで書く

## 区切りのコメント

- テストの中は `// Arrange`、`// Act`、`// Assert` で区切る
- 準備がなければ `// Arrange` を省く
- 段階を追うシナリオは `// Act / Assert: 確認すること` を重ね、段階の間に `// Arrange` を挟まない
- 段階を追うシナリオの準備と変更前の値の記録は、最初の `// Arrange` か段階の中に書く
- 段階のないテストで、対象の呼び出しをすべて Assert の式の中に書くとき (複数行でもよい) は、説明のない `// Act / Assert` にする
- `// Act` と `// Assert` を分けるテストでは、状態コードを確認するレスポンスの読み取り (`ReadAsAsync`、`ReadProblemAsync`) を `// Assert` に置く
- リクエストと一体の `GetJsonAsync` は `// Act` に置いてよい
- 例外として、続きの前提 (書き込みの成否、次のリクエストや呼び出しに使う値) は、準備と `// Act` の途中で確認してよい

## 単体テスト

- `Pos.Domain.Tests` は UI、DB、HTTP に依存しない純粋なロジックを検証する
- `Pos.Server.UnitTests` は Host の部品を、対象と同じフォルダ構成で置く
- Blazor の部品は bUnit (`MudBlazorTestBase` を継承) でテストする
- bUnit で利用者の役割を与えるときは `AddAuthorization().SetAuthorized(...).SetPolicies(...)` を使う (ポリシーを持たない利用者は `SetPolicies` を省く)
- 部品が使うサービスの差し替えが要るときは NSubstitute を使う

## 統合テスト

- `Pos.Server.IntegrationTests` は `IClassFixture<TestApplicationFactory>` でクラスごとに新しい SQLite DB を使う
- 統合テストの Accessor と Service は `factory.Services` から解決する

### API の呼び出し

- API は管理者のクライアント (`factory.CreateAdminClientAsync()`) から呼ぶ (端末が記録する登録も)
- 端末のトークンでテストするとき (端末だけの API、端末の照合と失効、端末では拒否される管理だけの API、端末の操作の承認など) は、`CreateTerminalClientAsync(TestData.XxxTerminalId)` で作ったクライアントから呼ぶ
- ほかのアカウントは、`CreateLoginClientAsync` で作ったクライアントから呼ぶ
- 素の `CreateClient()` は、ログインの要らないリクエスト (OpenAPI、ヘルスチェック、ペアリング、匿名の確認など) に使う
- リダイレクトを確認するリクエスト (ログイン、ログイン画面へのリダイレクト) は、`AllowAutoRedirect = false` の `CreateClient(options)` で作ったクライアントから呼ぶ
- 認証を無効にしたときの動作は `AuthDisabledApplicationFactory` でテストする
- API のルートは `ApiRoutes` で書く
- API は `ApiTestExtensions` で呼ぶ (JSON を送る POST は `PostJsonAsync`、PUT は `PutJsonAsync`、成功の JSON を読む GET は `GetJsonAsync`、DELETE は `DeleteUrlAsync`)
- 例外として、本文のない POST、JSON でない本文 (PDF、CSV、画像)、手で組んだ JSON (壊れた JSON、null を入れた JSON など)、失敗や状態コードだけを確認する GET、ヘッダの要るリクエストは `HttpClient` を直接呼ぶ

### 結果の確認

- API の結果は、状態コードと Problem Details のエラーコード (`"DUPLICATE_CODE"` のような文字列) で確認する
- 本文のないレスポンス (204、401、ポリシーの 403、画像の 304 と 412、ない API の 404 など) は、Problem Details を読まずに状態コードで確認する
- PDF は `application/pdf` と先頭の `%PDF` を確認する
- PDF は `TestResults` に保存する

### テストデータ

- 統合テストで固定 ID が要るときは、初期データ (`InitialData.sql`) の値に合わせた定数を `TestData` に `Id(テーブルの番号, 番号)` で定義する (製品コードには持たない)
- 初期データを変えたら、`TestData` と、件数や値を検証するテスト (`DatabaseTests`、`ApiMasterTests` の同期の件数など) を合わせる
- テストで作る行の一意の列 (コード、アカウントの名前) と、作った行をキーワードで絞り込む語は `Guid` から作り、ほかのテストと重複させない
- `Guid` から作る値は、列の上限に収まる長さにする (`$"T{Guid.NewGuid():N}"[..10]`)
- 登録する取引に載せる番号 (レシート番号、シリアル番号) は、クラスの中で重ならない値にする (拒否されて書き込まれないリクエストは同じ値でよい)
- 同じクラスでシフトを開設するテストは端末を分ける (拒否される開設は除く)
- 日次締めのテストは、店舗 × 営業日の組も分ける
- API で登録する取引 (`TransactionCreateRequest`) は、`TransactionRequests` で計算 API の結果をコピーして組む (計算の前に拒否されるリクエストは除く)

### 同時のリクエスト

- 同時に届くリクエストは、`IDbProvider` の接続でトランザクションを始めて (`BeginTransactionAsync`) 書き込みを止め、その間に送って重ねる (読み取りは通る)
- 同時に届くリクエストは、どれが先に書いても変わらない結果 (201 の件数、ほかの状態コード、記録の件数) で確認する
- リクエストの検証と書き込みの間に先に通る別の書き込み (日次締めなど) は、書き込みを止めたトランザクションでその書き込みをしてからコミットして重ねる
- 止めたトランザクションでの書き込みは、Accessor に止めたトランザクションを渡して行う (Service と API は止めた書き込みを待つ)
