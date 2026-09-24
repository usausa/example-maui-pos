---
paths:
  - "shared/Pos.Domain.Tests/**"
  - "server/tests/**"
---
# テスト

- xunit v3。キャンセルは `TestContext.Current.CancellationToken`
- テストは実行順に依存させない。xunit v3 の実行順はアセンブリのパスを含む ID で決まり、環境によって変わる。同じフィクスチャ (DB) を使うテストが登録・削除した行を、件数の検証に含めない
- テスト名はアンダースコアなしの PascalCase (`SyncMastersReturnsAllThenDelta`)。目的はメソッドの上に日本語のコメントで書き、本文は `// Arrange` / `// Act` / `// Assert` で区切る
- `Pos.Domain.Tests`: UI / DB / HTTP に依存しない純粋なロジックの検証
- `Pos.Server.UnitTests`: Blazor の部品は bUnit (`MudBlazorTestBase`)、差し替えは NSubstitute
- `Pos.Server.IntegrationTests`: `IClassFixture<TestApplicationFactory>` でクラスごとに新しい SQLite DB を使う。Accessor / Service は `factory.Services` から解決し、API は `ApiTestExtensions` (`GetJsonAsync` / `PostJsonAsync` / `PutJsonAsync` / `DeleteUrlAsync` / `ReadAsAsync` / `ReadProblemAsync`) で呼んで状態コードと Problem Details のエラーコードを検証する
- 固定 ID が必要なら、初期データ (`InitialData.sql`) の値に合わせた定数をテスト側 (`TestData`) に定義する。製品コードには持たない。テストで作る行のコードは `Guid` から作り、他のテストと重複させない (`$"T{Guid.NewGuid():N}"[..10]`)
