---
paths:
  - "shared/Pos.Domain.Tests/**"
  - "server/tests/**"
---
# テスト

- xunit v3。キャンセルは `TestContext.Current.CancellationToken`
- テストは実行順に依存させない (実行順は環境によって変わる)。同じフィクスチャ (DB) を使うテストが登録・削除した行を、件数の検証に含めない
- テスト名はアンダースコアなしの PascalCase (`SyncMastersReturnsAllThenDelta`) にし、目的はメソッドの上に日本語のコメントで書く
- 本文は `// Arrange` / `// Act` / `// Assert` で区切る。準備がなければ `// Arrange` を省き、段階を追うシナリオは `// Act / Assert: 確かめること` を重ねる (既存のテストは触ったときに揃える)
- `Pos.Domain.Tests`: UI / DB / HTTP に依存しない純粋なロジックを検証する
- `Pos.Server.UnitTests`: Host の部品を対象と同じフォルダ構成で置く。Blazor は bUnit (`MudBlazorTestBase` を継承)、役割は `AddAuthorization().SetAuthorized(...).SetPolicies(...)`、差し替えが要るときは NSubstitute を使う
- `Pos.Server.IntegrationTests`: `IClassFixture<TestApplicationFactory>` でクラスごとに新しい SQLite DB を使い、Accessor と Service は `factory.Services` から解決する
- API は `factory.CreateAdminClientAsync()` で作ったクライアントから `ApiTestExtensions` で呼び、状態コードと Problem Details のエラーコード (`"DUPLICATE_CODE"` のような文字列) を確かめる。経路は `ApiRoutes` で書く
- 端末の要求は `CreateTerminalClientAsync(TestData.XxxTerminalId)`、他のアカウントは `CreateLoginClientAsync`、認証を無効にした動きは `AuthDisabledApplicationFactory` で確かめる。素の `CreateClient()` は匿名の確認だけに使う
- 固定 ID が必要なら、初期データ (`InitialData.sql`) の値に合わせた定数を `TestData` に `Id(種別, 番号)` で定義する (製品コードには持たない)
- テストで作る行のコードは `Guid` から作り、他のテストと重複させない (`$"T{Guid.NewGuid():N}"[..10]`)
