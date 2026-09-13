# template-maui-pos

MAUI (レジ端末アプリ) + ASP.NET Core (POS サーバ: API + Blazor 管理画面) の POS サンプル。設計は [docs/](docs/README.md) を参照。

## 構成

```
shared/    Pos.Domain (ドメインロジック) / Pos.Domain.Tests / Pos.Shared (通信データ)
server/    Pos.Server.slnx: Pos.Server.AppHost (Aspire) / Pos.Server.Core (Accessor) / Pos.Server.Host (API + Blazor) / tests
terminal/  Pos.Terminal.slnx: Pos.Terminal (MAUI, Android)
```

サーバと端末は別ソリューションとして Visual Studio で個別に開く。どちらも `shared/` のプロジェクトを含む。

## 起動

| 対象 | 方法 |
| --- | --- |
| サーバ | `server/Pos.Server.slnx` を開いて `Pos.Server.Host` を実行、または `dotnet run --project server/src/Pos.Server.Host` (http://localhost:8080) |
| サーバ (Aspire) | `dotnet run --project server/src/Pos.Server.AppHost` |
| 端末 | `terminal/Pos.Terminal.slnx` を開いて Android エミュレータまたは実機で実行。エミュレータからサーバへは `http://10.0.2.2:8080/` |

開発時は `/swagger` と `/redoc` で API 仕様を確認できる。データベース (SQLite `pos.db`) は起動時に自動作成される。

テストはテストプロジェクトごとに `dotnet run --project` で実行する (`shared/Pos.Domain.Tests`、`server/tests/Pos.Server.UnitTests`、`server/tests/Pos.Server.IntegrationTests`)。`dotnet test` は使わない (CI の Jenkins も同じコマンド)。
