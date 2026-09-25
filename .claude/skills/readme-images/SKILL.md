---
name: readme-images
description: README の画面と帳票の画像 (docs/images/ の server-*.png、terminal-*.png、report-*.png) を撮り直す。管理画面と帳票はヘッドレス Chrome、端末はエミュレータで撮り、README の大きさ (管理画面 1200 幅、端末 360x800、帳票 600x849) と 256 色にそろえる。画面や帳票の見た目を変えたときや、画面・帳票を README に足すときに使う。
---

# README の画像

画像は `docs/images/` にあり、README の表から参照する。
管理画面は `server-*.png` (1400x900 で撮って幅 1200)、端末は `terminal-*.png` (1080x2400 で撮って 360x800)、帳票は `report-*.png` (PDF を 1240x1754 で表示して撮り、用紙を切り出して 600x849) で、どれも 256 色の PNG。
スクリプトはリポジトリのルートで実行する (Windows では `python`、他では `python3`)。撮った元の画像は一時フォルダに置き、リポジトリに入れない。

## 準備

- サーバを起動する (`dotnet run --project server/src/Pos.Server.Host`。ポートを変えたときは `--base` に合わせる)
- 画面に中身が出るようにサンプルの取引を作る。ダッシュボードは当日の取引が要るので、撮る直前に当日分を足す
  - `dotnet run --project server/tools/Pos.Server.SampleData -- --base http://localhost:8080 --days 7`
  - `dotnet run --project server/tools/Pos.Server.SampleData -- --base http://localhost:8080 --days 1 --seed 3`

## 管理画面

1. 手順を作る: `python .claude/skills/readme-images/scripts/server_steps.py --out <一時フォルダ> --base http://localhost:8080` (一部だけなら `--only server-orders,server-products`)
2. 撮る: `node .claude/skills/readme-images/scripts/capture_server.mjs <一時フォルダ>/steps.json` (Node.js 22 以降、Chrome が要る。場所は `CHROME_PATH`)
3. 撮った画像を見て、中身 (件数、ダイアログ、エラーの有無) を確かめる
4. そろえる: `python .claude/skills/readme-images/scripts/shrink.py server <一時フォルダ>/server-*.png`

- レジ端末 (`server-terminals`) は、未登録の「本店 レジ 2」にペアリングコードを発行してダイアログを出した状態で撮る (コードは 10 分で切れる)
- 新しい画面を README に足すときは、`server_steps.py` の `PAGES` と README の表の両方に足す

## 端末

撮るのはエミュレータだけにする (emulator skill の手順で入れて操作する)。

1. 画面を README の表の状態にして、`emulator` skill の `emu.py shot <一時フォルダ>/terminal-xxx.png` で撮る
2. そろえる: `python .claude/skills/readme-images/scripts/shrink.py terminal <一時フォルダ>/terminal-*.png`

| 画像 | 画面の状態 |
| --- | --- |
| `terminal-home` | ホーム (担当とシフトが開いた状態) |
| `terminal-sales` | 販売 (明細が数行ある状態) |
| `terminal-payment` | 会計 (預かりを入れた状態) |
| `terminal-receipt` | レシート |
| `terminal-setup` | 初期設定 (サーバ URL を入れた状態) |
| `terminal-product-search` | 商品検索 (検索結果がある状態) |
| `terminal-shift-close` | 精算 |
| `terminal-sales-report` | 売上照会 |
| `terminal-transactions` | 取引履歴 |
| `terminal-product-inquiry` | 商品・在庫照会 |
| `terminal-orders` | 受注一覧 |
| `terminal-order-detail` | 受注詳細 (前受金を受け取った入荷待ちの受注) |
| `terminal-receiving` | 入荷・移動の検品 |
| `terminal-line-edit` | 明細の編集 (販売の明細をタップして開いたシート) |
| `terminal-sales-menu` | 販売の操作 (販売の [⋯] で開いたシート) |
| `terminal-pin` | PIN の入力 (スタッフ選択で担当を選び、2 桁入れた電卓のシート) |

## 帳票

管理画面にログインした画面から帳票の PDF を保存し、Chrome の PDF 表示 (操作バーなし、1 ページを画面に合わせる) で撮る。

1. 手順を作る: `python .claude/skills/readme-images/scripts/report_steps.py --out <一時フォルダ> --base http://localhost:8080` (一部だけなら `--only report-order`)
2. 撮る: `node .claude/skills/readme-images/scripts/capture_server.mjs <一時フォルダ>/steps.json 1240 1754` (A4 の比)
3. 撮った画像を見て、対象と中身 (確認のために入れた備考などが写っていないか) を確かめる
4. そろえる: `python .claude/skills/readme-images/scripts/shrink.py report <一時フォルダ>/report-*.png` (用紙を切り出し、600x849 の枠の中央に置く)

| 画像 | 帳票の対象 (開発用の DB から選ぶ) |
| --- | --- |
| `report-order` | 受注票 (前受金のある未完了の受注) |
| `report-purchase-order` | 発注書 (備考のない発注済みの発注) |
| `report-shift` | 精算レポート (精算済みのシフト) |
| `report-daily-sales` | 売上日報 (締め済みの店舗 × 営業日) |
| `report-receipt` | レシートの控え (直近の販売のうち明細の多いもの) |

- 新しい帳票を README に足すときは、`report_steps.py` の `REPORTS` と README の表の両方に足す

## 仕上げ

- 置き換えた画像を開いて、縮小で文字が潰れていないかを確かめる
- README の説明 (表の下の行) が画面と合っているかを確かめる
- 撮るために変えたデータ (発行したペアリングコード、足した取引) は開発用の DB に残る。元に戻す必要があれば DB を作り直す
