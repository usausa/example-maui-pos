---
paths:
  - "shared/Pos.Domain.Tests/**"
  - "server/tests/**"
---
# テスト

- xunit v3。キャンセルは `TestContext.Current.CancellationToken`。実行は `dotnet run --project` (`dotnet test` は使わない)
- テストは実行順に依存させない。xunit v3 の実行順はアセンブリのパスを含む ID で決まり、環境によって変わる。同じフィクスチャ (DB) を使うテストが登録・削除した行を、件数の検証に含めない
- テストの目的はメソッドの上に日本語のコメントで書く (`// 全件同期 → serverTime 以降の差分は空`)
- `Pos.Domain.Tests`: UI / DB / HTTP に依存しない純粋なロジックの検証。`DependencyTests` で Domain が他に依存しないことも検証する
- `Pos.Server.UnitTests`: Blazor の部品は bUnit (`MudBlazorTestBase`)、差し替えは NSubstitute
- `Pos.Server.IntegrationTests`: `IClassFixture<TestApplicationFactory>` でクラスごとに新しい SQLite DB を使う。Accessor / Service は `factory.Services` から解決し、API は `ApiTestExtensions` (`GetJsonAsync` / `PostJsonAsync` / `PutJsonAsync` / `DeleteUrlAsync` / `ReadAsAsync` / `ReadProblemAsync`) で呼んで状態コードと Problem Details のエラーコードを検証する
- 固定 ID が必要なら、初期データ (`InitialData.sql`) の値に合わせた定数をテスト側 (`TestData`) に定義する。製品コードには持たない。テストで作る行のコードは `Guid` から作り、他のテストと重複させない (`$"T{Guid.NewGuid():N}"[..10]`)
