---
paths:
  - "shared/Pos.Domain.Tests/**"
  - "server/tests/**"
---
# テスト

- xunit v3 にする
- キャンセルはクラスに `private static CancellationToken Token => TestContext.Current.CancellationToken;` を置いて渡す
- テストは実行順に依存させない (実行順は環境によって変わる)。同じフィクスチャ (DB) を使うテストが登録・削除した行を、件数の検証に含めない
- テスト名はアンダースコアなしの PascalCase (`SyncMastersReturnsAllThenDelta`) にし、目的はメソッドの上に日本語のコメントで書く
- 本文は `// Arrange`、`// Act`、`// Assert` で区切る。準備がなければ `// Arrange` を省き、段階を追うシナリオは `// Act / Assert: 確かめること` を重ねる (既存のテストは触ったときに揃える)
- `Pos.Domain.Tests` は UI、DB、HTTP に依存しない純粋なロジックを検証する
- `Pos.Server.UnitTests` は Host の部品を、対象と同じフォルダ構成で置く
- Blazor の部品は bUnit (`MudBlazorTestBase` を継承) で確かめ、役割は `AddAuthorization().SetAuthorized(...).SetPolicies(...)` で与える。差し替えが要るときは NSubstitute を使う
- `Pos.Server.IntegrationTests` は `IClassFixture<TestApplicationFactory>` でクラスごとに新しい SQLite DB を使い、Accessor と Service は `factory.Services` から解決する
- API は `factory.CreateAdminClientAsync()` で作ったクライアントから `ApiTestExtensions` で呼び、経路は `ApiRoutes` で書く
- API の結果は、状態コードと Problem Details のエラーコード (`"DUPLICATE_CODE"` のような文字列) で確かめる
- 端末の要求は `CreateTerminalClientAsync(TestData.XxxTerminalId)`、他のアカウントは `CreateLoginClientAsync` で作る
- 認証を無効にした動きは `AuthDisabledApplicationFactory` で確かめ、素の `CreateClient()` は匿名の確認だけに使う
- 統合テストで固定 ID が要るときは、初期データ (`InitialData.sql`) の値に合わせた定数を `TestData` に `Id(種別, 番号)` で定義する (製品コードには持たない)
- テストで作る行のコードは `Guid` から作り、他のテストと重複させない (`$"T{Guid.NewGuid():N}"[..10]`)
