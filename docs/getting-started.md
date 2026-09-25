# 起動と開発

サーバと端末の起動手順、サンプル取引の生成、テストと静的解析。  
プロジェクト構成は [architecture.md](architecture.md)、設計判断は [decisions.md](decisions.md)、AI 向けの規則は [AGENTS.md](../AGENTS.md) と `.claude/rules/`、繰り返す手順とスクリプトは `.claude/skills/`。

## 構成

```
shared/    Pos.Domain (ドメインロジック) / Pos.Domain.Tests / Pos.Contract (通信データ)
server/    Pos.Server.slnx: Pos.Server.AppHost (Aspire) / Pos.Server.Core (Accessor) / Pos.Server.Host (API + Blazor) / tests / tools (Pos.Server.SampleData)
terminal/  Pos.Terminal.slnx: Pos.Terminal (MAUI, Android)
```

サーバと端末は別ソリューションとして Visual Studio で個別に開く。  
どちらも `shared/` のプロジェクトを含む。

| 項目 | 内容 |
| --- | --- |
| サーバ | .NET 10 / Minimal API + Blazor Server (MudBlazor) / SQLite + Smart.Data.Accessor (2-way SQL) / OpenAPI (Swagger, ReDoc) / 帳票 PDF (OysterReport) / Aspire |
| 端末 | .NET 10 MAUI (Android) / Smart.Navigation + Smart.Mvvm / SQLite ローカル DB + Outbox (オフライン対応) / カメラスキャン / レシート画像 (SkiaSharp) + 電子レシート QR |
| 共有 | `Pos.Domain` (税・値引按分・ポイント・返品の計算、業務ルール) / `Pos.Contract` (`XxxRequest` / `XxxResponse`) |

## サーバ

```bash
dotnet run --project server/src/Pos.Server.Host
```

- 管理画面: http://localhost:8080/ 。  
  ポートは `appsettings.json` の `http_ports`
- 管理画面はログインしてから使う。  
  初回はアカウントが 1 件もないので、設定 (`Auth:InitialName` / `InitialPassword`、既定 `admin` / `admin`) の管理者が作られる。  
  運用ではログイン後に「設定 › ユーザー」でパスワードを変え、オペレーター (参照と、取引・在庫・顧客の操作だけ) を追加する。  
  開発・デモでは `Auth:Enabled` を `false` にすると認可を素通しにできる ([D-73](decisions.md#d-73-認証と端末登録-管理画面はログイン端末はペアリングのトークンスタッフは-pin))
- API 仕様 (開発時): http://localhost:8080/swagger 、http://localhost:8080/redoc 、`/openapi/v1.json`
- データベース (SQLite `pos.db`、実行ディレクトリ) は起動時に自動作成され、初期データ (店舗 2 / 端末 3 / スタッフ / 税率 / 支払方法 / 部門・商品 33 / 値引 / 会員 5 / 在庫) が投入される ([architecture.md §6](architecture.md#6-初期データ))。  
  後から増えた列は起動時に既存の DB へ足す (`SchemaHelper.EnsureColumnAsync`)
- Aspire で起動する場合は `dotnet run --project server/src/Pos.Server.AppHost` (ダッシュボードは http://localhost:15000 。ログ・メトリクス・トレースが OTLP で送られる)
- メトリクスは http://localhost:9464/metrics (Prometheus 形式。`Prometheus:Uri` を空にすると止まる)。  
  API の要求ログは `Log:HttpLog`、本文のダンプは `Log:HttpDump`、W3C 形式のアクセスログは `Log:W3CLog`、SQL のログとトレースは `Profiler` で切り替える (Development は HTTP ログと SQL が既定で有効)
- Visual Studio では `server/Pos.Server.slnx` を開いて `Pos.Server.Host` を実行

## 端末

`terminal/Pos.Terminal.slnx` を開いて Android エミュレータまたは実機で実行する。  
コマンドラインなら次のとおり。

```bash
dotnet build terminal/Pos.Terminal/Pos.Terminal.csproj -f net10.0-android -t:Run -p:AdbTarget="-s emulator-5554"
```

初回起動の初期設定で端末を登録する。  
管理画面の「店舗 › レジ端末」で端末の [ペアリングコードを発行] を押し、表示された 6 桁のコード (10 分間、一度だけ使える) を端末で使う。

| 方法 | 手順 |
| --- | --- |
| 設定 QR | 発行のダイアログの設定 QR (`ApiEndPoint` と `PairingCode`) を、端末の「QR 読取」で読み取る (実機向け) |
| 手入力 | サーバ URL を入力し、「コード」でペアリングコードを電卓で入れる。エミュレータからホストのサーバへは `http://10.0.2.2:8080/` |

「登録」で端末を登録してマスタを全件同期し、スタッフ選択へ進む。  
スタッフを選んで PIN を入れるとホームへ進む (初期データの PIN は A001 = 0000、M001 = 1111、C001 = 2222、C002 = 3333。管理画面の「店舗 › スタッフ」で設定する)。  
承認が必要な値引とレジ係の取消は、店長以上を承認者に選んでその PIN を入れる。  
管理画面で登録を解除すると、端末は次の通信で初期設定に戻る (未送信の取引は残り、登録し直すと送る)。  
「レジ開設」で釣銭準備金を入れてシフトを開くと販売できる。  
取引は端末のローカル DB に保存してから Outbox 経由でサーバへ送るので、オフラインでも販売・返品・精算を続けられる (復帰後に自動送信)。  
サーバに拒否された取引は「設定・同期」で理由を確認して再送 / 破棄する。

## サンプル取引の生成

レポートやダッシュボードの確認用に、起動中のサーバへ初日の入荷 (在庫を積む) と直近数日分のシフト・販売・返品・取消・入出金・精算を API で登録し、前日までを日次締めする。

```bash
dotnet run --project server/tools/Pos.Server.SampleData -- --days 7
```

オプションは `--base <url>` (既定 `http://localhost:8080/`)、`--user <id>` / `--password <pw>` (管理画面のログイン、既定 `admin` / `admin`)、`--days <n>` (既定 7)、`--per-day <n>` (端末 1 台 1 日あたりの販売件数の目安、既定 6)、`--seed <n>` (乱数、既定 1)。  
開設中のシフトがある端末は省略する。  
詳細は [architecture.md §6](architecture.md#6-初期データ) と [D-41](decisions.md#d-41-サンプル取引の生成-api-経由のコンソールツール)。

商品マスタの CSV 取込は、初期データと同じ 33 商品の [docs/samples/products.csv](samples/products.csv) を編集して管理画面の「商品 › CSV 取込」で試せる (列は「CSV 出力」と同じ)。

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

ビルドは警告ゼロ、ReSharper の InspectCode は指摘ゼロを保つ。  
InspectCode は Jenkins と同じくソリューション全体の解析 (SWEA) を有効にして実行する (`--no-swea` を付けると `.Global` の指摘が出ないため、Jenkins との差になる)。

```bash
jb inspectcode server/Pos.Server.slnx -f=xml -o=results-server.xml --no-build --properties:Configuration=Release
```

```bash
jb inspectcode terminal/Pos.Terminal.slnx -f=xml -o=results-terminal.xml --no-build --properties:Configuration=Release
```

手元で繰り返すと前回の解析のキャッシュが結果に残ることがあるので、`--caches-home` に毎回新しいフォルダを渡す。  
ビルド (警告)、テスト、InspectCode、変更したファイルの改行コードと文書の改行は、次のスクリプトでまとめて確かめられる (ログは一時フォルダの `pos-verify`)。

```bash
python .claude/skills/verify/scripts/verify.py
```
