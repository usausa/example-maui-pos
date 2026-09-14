# template-maui-pos

MAUI (レジ端末アプリ) + ASP.NET Core (POS サーバ: API + Blazor 管理画面) の POS サンプル。家電・カメラ・ホームセンターの物販を想定し、販売・会計・レシート、レジ開閉と精算、返品・取消、会員ポイント、在庫 (棚卸・調整)、売上レポートを扱う。設計は [docs/](docs/README.md) を参照。

| 端末 (Android) | | | |
| --- | --- | --- | --- |
| ![ホーム](docs/images/terminal-home.png) | ![販売](docs/images/terminal-sales.png) | ![会計](docs/images/terminal-payment.png) | ![レシート](docs/images/terminal-receipt.png) |
| ホーム (T-02) | 販売 (T-10) | 会計 (T-20) | レシート (T-22) |
| ![初期設定](docs/images/terminal-setup.png) | ![商品検索](docs/images/terminal-product-search.png) | ![精算](docs/images/terminal-shift-close.png) | ![売上照会](docs/images/terminal-sales-report.png) |
| 初期設定 (T-00) | 商品検索 (T-12) | 精算 (T-51) | 売上照会 (T-80) |

| 管理画面 (Blazor + MudBlazor) |
| --- |
| ![ダッシュボード](docs/images/server-dashboard.png) |
| ダッシュボード (S-01): 本日の売上・要確認、店舗別売上、開設中シフト、端末の通信状態、マイナス在庫 |
| ![売上集計](docs/images/server-sales-report.png) |
| 売上集計 (S-10): 期間・店舗・集計単位 (日別 / 店舗別 / 時間帯別 / 端末別 / 担当別 / 支払方法別 / 税率別 / 部門別)、CSV |
| ![取引](docs/images/server-transactions.png) |
| 取引 (S-20): 営業日・店舗・端末・種別・状態・レシート番号で検索 |

## 構成

```
shared/    Pos.Domain (ドメインロジック) / Pos.Domain.Tests / Pos.Shared (通信データ)
server/    Pos.Server.slnx: Pos.Server.AppHost (Aspire) / Pos.Server.Core (Accessor) / Pos.Server.Host (API + Blazor) / tests / tools (Pos.Server.SampleData)
terminal/  Pos.Terminal.slnx: Pos.Terminal (MAUI, Android)
```

サーバと端末は別ソリューションとして Visual Studio で個別に開く。どちらも `shared/` のプロジェクトを含む。

| 項目 | 内容 |
| --- | --- |
| サーバ | .NET 10 / Minimal API + Blazor Server (MudBlazor) / SQLite + Smart.Data.Accessor (2-way SQL) / OpenAPI (Swagger, ReDoc) / 帳票 PDF (OysterReport) / Aspire |
| 端末 | .NET 10 MAUI (Android) / Smart.Navigation + Smart.Mvvm / SQLite ローカル DB + Outbox (オフライン対応) / カメラスキャン / レシート画像 (SkiaSharp) + 電子レシート QR |
| 共有 | `Pos.Domain` (税・値引按分・ポイント・返品の計算、業務ルール) / `Pos.Shared` (`XxxRequest` / `XxxResponse`) |

## 起動

### 1. サーバ

```bash
dotnet run --project server/src/Pos.Server.Host
```

- 管理画面: http://localhost:8080/ 、API 仕様 (開発時): http://localhost:8080/swagger 、http://localhost:8080/redoc
- データベース (SQLite `pos.db`、実行ディレクトリ) は起動時に自動作成され、初期データ (店舗 2 / 端末 3 / スタッフ / 税率 / 支払方法 / 部門・商品 33 / 値引 / 会員 5 / 在庫) が投入される ([architecture.md §8](docs/architecture.md#8-初期データ))
- Aspire で起動する場合は `dotnet run --project server/src/Pos.Server.AppHost` (ダッシュボードは http://localhost:15000)
- Visual Studio では `server/Pos.Server.slnx` を開いて `Pos.Server.Host` を実行

### 2. 端末

`terminal/Pos.Terminal.slnx` を開いて Android エミュレータまたは実機で実行する。コマンドラインなら:

```bash
dotnet build terminal/Pos.Terminal/Pos.Terminal.csproj -f net10.0-android -t:Run -p:AdbTarget="-s emulator-5554"
```

初回起動の初期設定 (T-00) で接続先を決める。

| 方法 | 手順 |
| --- | --- |
| 設定 QR | 管理画面の「店舗 › レジ端末」(S-71) で端末を選んで設定 QR を表示し、端末の「QR 読取」で読み取る (実機向け) |
| 手入力 | サーバ URL・店舗 ID・端末 ID を入力する。エミュレータからホストのサーバへは `http://10.0.2.2:8080/`。初期データの本店 / 本店 レジ 1 は店舗 ID `00000000-0000-0000-0001-000000000001`、端末 ID `00000000-0000-0000-0002-000000000001` (管理画面の店舗 / 端末の画面でも確認できる) |

「開始」でサーバに店舗・端末を確認してマスタを全件同期し、スタッフ選択 (T-01) → ホーム (T-02) へ進む。「レジ開設」で釣銭準備金を入れてシフトを開くと販売できる。取引は端末のローカル DB に保存してから Outbox 経由でサーバへ送るので、オフラインでも販売・返品・精算を続けられる (復帰後に自動送信。サーバに拒否された場合は「設定・同期」(T-90) で理由を確認して再送 / 破棄する)。

### 3. サンプル取引の生成 (任意)

レポートやダッシュボードの確認用に、起動中のサーバへ直近数日分のシフト・販売・返品・取消・入出金・精算を API で登録する。

```bash
dotnet run --project server/tools/Pos.Server.SampleData -- --days 7
```

オプションは `--base <url>` (既定 `http://localhost:8080/`)、`--days <n>` (既定 7)、`--per-day <n>` (端末 1 台 1 日あたりの販売件数の目安、既定 6)、`--seed <n>` (乱数、既定 1)。開設中のシフトがある端末は省略する。詳細は [architecture.md §8](docs/architecture.md#8-初期データ) と [D-41](docs/decisions.md#d-41-サンプル取引の生成-api-経由のコンソールツール)。

## テスト・静的解析

テストはテストプロジェクトごとに `dotnet run --project` で実行する (`dotnet test` は使わない。CI の Jenkins も同じコマンド)。

```bash
dotnet run --project shared/Pos.Domain.Tests
```

```bash
dotnet run --project server/tests/Pos.Server.UnitTests
```

```bash
dotnet run --project server/tests/Pos.Server.IntegrationTests
```

ビルドは警告ゼロ、ReSharper の InspectCode (`jb inspectcode server/Pos.Server.slnx` / `terminal/Pos.Terminal.slnx`) は指摘ゼロを保つ。コーディング規約は [AGENTS.md](AGENTS.md)。

## ドキュメント

| 文書 | 内容 |
| --- | --- |
| [docs/README.md](docs/README.md) | 設計ドキュメントの索引と前提の要約 |
| [docs/decisions.md](docs/decisions.md) | 設計判断の記録 (D-00 〜 D-42) |
| [docs/architecture.md](docs/architecture.md) | ソリューション構成、プロジェクト・層・パッケージ、実行・開発、初期データ |
| [docs/implementation-plan.md](docs/implementation-plan.md) | 実装プラン (Phase 0〜7 は完了。Phase 8〜14 は後回し項目の計画) |
| [docs/api-design.md](docs/api-design.md) / [docs/db-design.md](docs/db-design.md) / [docs/screen-design.md](docs/screen-design.md) | API / DB / 画面の設計 |
