# 起動と開発

サーバと端末の起動手順、サンプル取引の生成、テストと静的解析。

- プロジェクト構成: [architecture.md](architecture.md)
- 設計方針: [decisions.md](decisions.md)
- AI 向けの規則: [AGENTS.md](../AGENTS.md)、`.claude/rules/`
- 繰り返す手順とスクリプト: `.claude/skills/`

## 🧱 構成

```
shared/    Pos.Domain (ドメインロジック) / Pos.Domain.Tests / Pos.Contract (通信データ)
server/    Pos.Server.slnx: Pos.Server.AppHost (Aspire) / Pos.Server.Core (Accessor・Service) / Pos.Server.Host (API + Blazor) / tests / tools (Pos.Server.SampleData)
terminal/  Pos.Terminal.slnx: Pos.Terminal (MAUI, Android)
```

サーバと端末は別ソリューションとして Visual Studio で個別に開く。  
どちらも `shared/` のプロジェクトを含む。

| 項目 | 内容 |
| --- | --- |
| サーバ | .NET 11 / Minimal API + Blazor Server (MudBlazor) / SQLite + Smart.Data.Accessor (2-way SQL) / OpenAPI (Swagger, ReDoc) / 帳票 PDF (OysterReport) / Aspire |
| 端末 | .NET 11 MAUI (Android) / Smart.Navigation + Smart.Mvvm / SQLite ローカル DB + Outbox (オフライン対応) / カメラスキャン / レシート画像 (SkiaSharp) + 電子レシート QR |
| 共有 | `Pos.Domain` (税・値引按分・ポイント・返品の計算、業務ルール) / `Pos.Contract` (`XxxRequest` / `XxxResponse`) |

## 🧰 前提

- .NET 11 SDK
- 端末: MAUI の Android ワークロード (`dotnet workload install maui-android`)、Android 11 以上のエミュレータか実機
- InspectCode: `jb` (`dotnet tool install -g JetBrains.ReSharper.GlobalTools`)
- 検証のスクリプト: Python

## 💻 サーバ

```bash
dotnet run --project server/src/Pos.Server.Host
```

| 対象 | URL |
| --- | --- |
| 管理画面 | http://localhost:8080/ |
| API 仕様 (開発時) | http://localhost:8080/swagger 、http://localhost:8080/redoc 、`/openapi/v1.json` |
| ヘルスチェック | `/health`、`/alive` |
| メトリクス (Prometheus 形式) | http://localhost:9464/metrics |
| Aspire のダッシュボード | http://localhost:15000 |

- ポートは、`dotnet run` と Visual Studio では Host の `Properties/launchSettings.json` の `applicationUrl`、発行したものは `appsettings.json` の `http_ports`
- Aspire で起動するときは `dotnet run --project server/src/Pos.Server.AppHost`。  
  ログ・メトリクス・トレースは OTLP でダッシュボードに送られる
- Visual Studio では `server/Pos.Server.slnx` を開いて実行する。  
  既定のスタートアップは `Pos.Server.AppHost` (Aspire)。  
  Aspire なしで動かすときは、`Pos.Server.Host` をスタートアップにする
- データベースは SQLite の `pos.db` (Host の出力フォルダ) で、起動時に作られる。  
  空なら初期データ ([architecture.md §6](architecture.md#-6-初期データ)) が入る。  
  後から増えた列は、起動時に既存の DB へ足す (`SchemaHelper.EnsureColumnAsync`)
- 管理画面の回線と SignalR の値 (切れた回線を残す時間、タイムアウトなど) は、`Blazor:Circuit`、`Blazor:Hub` で変える。  
  既定は ASP.NET Core の既定と同じ値

### 管理画面のログイン

管理画面はログインしてから使う。

- 初回はアカウントが 1 件もないので、設定 (`Auth:InitialName` / `InitialPassword`) の管理者が作られる。  
  既定は `admin` / `admin`
- 運用では、ログイン後に「設定 › ユーザー」でパスワードを変え、オペレーターを追加する。  
  オペレーターは、参照と、取引・在庫・顧客・受注・日次締めの操作ができる
- 開発・デモでは、`Auth:Enabled` を `false` にすると認可を素通しにできる ([D-35](decisions.md#d-35-認証は管理画面のログイン端末のトークンスタッフの-pin-にする))

### ログとメトリクス

ログとメトリクスは次の設定で切り替える。

| 設定 | 内容 |
| --- | --- |
| `Log:HttpLog` | API のリクエストログ |
| `Log:HttpDump` | 本文のダンプ |
| `Log:W3CLog` | W3C 形式のアクセスログ |
| `Profiler` | SQL のログとトレース |
| `Prometheus:Uri` | メトリクス (空にすると止まる) |

Development では、HTTP ログと SQL が既定で有効。

## 📱 端末

`terminal/Pos.Terminal.slnx` を開いて、Android のエミュレータか実機で実行する。  
コマンドラインなら次のとおり。

```bash
dotnet build terminal/Pos.Terminal/Pos.Terminal.csproj -f net11.0-android -t:Run -p:AdbTarget="-s emulator-5554"
```

### 端末の登録

初回起動の初期設定で端末を登録する。  
管理画面の「店舗 › レジ端末」で端末の [ペアリングコードを発行] を押し、表示された 6 桁のコードを端末で使う。  
コードは 10 分間、一度だけ使える。

| 方法 | 手順 |
| --- | --- |
| 設定 QR (実機向け) | 「店舗 › レジ端末」の [設定 QR] で開くページを印刷しておき、端末の初期設定の画面のカメラで読み取る。サーバ URL が入って電卓が開くので、ペアリングコードを入れる |
| 手入力 | サーバ URL を入力し、「コード」でペアリングコードを電卓で入れて「登録」を押す。エミュレータからホストのサーバへは `http://10.0.2.2:8080/` |

コードを入れるか「登録」を押すと、端末を登録してマスタを全件同期する。  
その後、スタッフ選択へ進む。

実機では次のようにする。

- サーバは LAN で待ち受けて起動する (`dotnet run --project server/src/Pos.Server.Host -- --urls http://0.0.0.0:8080`)
- 管理画面を端末から届くアドレスで開いてから、設定 QR を印刷する。  
  設定 QR の `ApiEndPoint` は、管理画面を開いた URL になる。  
  localhost のままなら、設定 QR のページに警告を出す
- 管理画面をサーバの PC から localhost で開くときは、`Terminal:ApiEndPoint` に端末から届く URL を設定する (`appsettings.json`、または `-- --Terminal:ApiEndPoint=http://192.168.0.10:8080/`)

管理画面で登録を解除すると、端末は次の通信で初期設定に戻る。

- 未送信の取引は残り、同じ店舗・番号の端末に登録し直すと送る
- 新しいサーバでも、同じ番号の端末なら、その端末の取引として送る

### スタッフ選択と販売

- スタッフを選んで PIN を入れると、ホームへ進む。  
  PIN は管理画面の「店舗 › スタッフ」で設定する。  
  初期データの PIN は A001 = 0000、M001 = 1111、C001 = 2222、C002 = 3333
- 承認が必要な値引とレジ係の取消は、店長以上を承認者に選んで、その PIN を入れる
- 「レジ開設」で釣銭準備金を入れてシフトを開くと、販売できる
- 取引は端末のローカル DB に保存してから、Outbox 経由でサーバへ送る。  
  オフラインでも販売・返品・精算を続けられ、復帰後に自動で送信する
- サーバに拒否された取引は、「設定・同期」で理由を確認して、再送するか破棄する

## 🧾 サンプル取引の生成

レポートやダッシュボードの確認用に、起動中のサーバへ API で次のことを行う。

- 初日の入荷を登録する (発注から作った入荷予定を受領して、在庫を積む)
- 直近数日分のシフト・販売・返品・取消・出金・精算を登録する
- 前日までを日次締めする
- 店舗ごとに、入荷待ちの発注を 1 件残す

開設中のシフトがある端末は省略する。

```bash
dotnet run --project server/tools/Pos.Server.SampleData -- --days 7
```

| オプション | 内容 | 既定 |
| --- | --- | --- |
| `--base <url>` | サーバ | `http://localhost:8080/` |
| `--user <id>` / `--password <pw>` | 管理画面のログイン | `admin` / `admin` |
| `--days <n>` | 日数 | 7 |
| `--per-day <n>` | 端末 1 台 1 日あたりの販売件数の目安 | 6 |
| `--seed <n>` | 乱数のシード | 1 |
| `--help` | 使い方を出す | — |

詳細は [architecture.md §6](architecture.md#-6-初期データ) と [D-42](decisions.md#d-42-サンプルの取引は-api-を呼ぶツールで作る)。

商品マスタの CSV 取込は、[docs/samples/products.csv](samples/products.csv) を編集して、管理画面の「商品 › CSV 取込」で試せる。  
このファイルの商品は初期データと同じで、列は「CSV 出力」と同じ。

## 🧪 テスト・静的解析

テストは、テストプロジェクトごとに `dotnet run --project` で実行する。  
`dotnet test` は使わない (CI の Jenkins も同じコマンド)。

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
InspectCode は次のように実行する。

- ソリューション全体の解析はしない (`--no-swea`)。  
  全体の解析は、端末の共通の部品 (テンプレートと同じにする) のうち POS で使わないメンバーを、`.Global` の指摘で挙げる
- `--caches-home` には毎回新しいフォルダを渡す。  
  キャッシュが残っていると、前回の結果が混じることがある

```bash
jb inspectcode server/Pos.Server.slnx --no-swea --caches-home=<新しいフォルダ> -o=results-server.sarif
```

```bash
jb inspectcode terminal/Pos.Terminal.slnx --no-swea --caches-home=<新しいフォルダ> -o=results-terminal.sarif
```

次の項目は、下のスクリプトでまとめて確認できる (ログは一時フォルダの `pos-verify`)。

- ビルドの警告
- テスト
- InspectCode
- 変更したファイルの改行コードと、文書の改行

```bash
python .claude/skills/verify/scripts/verify.py
```
