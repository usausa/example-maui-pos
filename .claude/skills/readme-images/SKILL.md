---
name: readme-images
description: README の画像 (管理画面、端末、帳票) を撮り直す。画面や帳票の見た目を変更したときと、README に画像を足すときに使う
---

# README の画像

画像は `docs/images/` にあり、README の表から参照する。
どれも 256 色の PNG。

| 対象 | ファイル | 撮る大きさ | README の大きさ |
| --- | --- | --- | --- |
| 管理画面 | `server-*.png` | 1400x900 | 幅 1200 |
| 端末 | `terminal-*.png` | 1080x2400 | 360x800 |
| 帳票 | `report-*.png` | PDF を 1240x1754 で表示 | 用紙を切り出して 600x849 |

スクリプトはリポジトリのルートで実行する (Windows では `python`、他では `python3`)。
撮った元の画像は一時フォルダに置き、リポジトリに入れない。
スクリプトとサンプルデータのツールの `--base` の既定は `http://localhost:8080/`。
サーバをほかの URL で動かすときだけ、`--base <サーバの URL>` を渡す。

## 準備

- `shrink.py` には Python の Pillow が要る
  - Pillow がなければ、インストールする前に利用者に確認する
- サーバをバックグラウンドで起動する: `dotnet run --project server/src/Pos.Server.Host`
  - `<サーバの URL>/health` がレスポンスを返すまで待つ
  - 撮り終えたら止める
- 画面に中身が出るように、サンプルの取引を作る
  - `dotnet run --project server/tools/Pos.Server.SampleData -- --days 7`
- ダッシュボードには当日の取引が要るので、撮る直前に当日分を足す
  - `dotnet run --project server/tools/Pos.Server.SampleData -- --days 1 --seed 3`
- サンプルデータは受注を作らない
- 受注の画像 (`server-orders`、`terminal-orders`、`terminal-order-detail`、`report-order`) には、端末で受注を作って前受金を受け取ったデータが要る (emulator skill の手順で操作する)

## 管理画面

1. 手順を作る: `python .claude/skills/readme-images/scripts/server_steps.py --out <一時フォルダ>`
   - 一部だけなら `--only server-orders,server-products`
2. 撮る: `node .claude/skills/readme-images/scripts/capture_server.mjs <一時フォルダ>/steps.json`
   - Node.js 22 以降と Chrome が要る
   - Chrome の場所は `CHROME_PATH`、DevTools のポートは `CDP_PORT` (既定 9334) で変更する
3. 撮った画像を見て、中身 (件数、ダイアログ、エラーの有無) を確認する
4. そろえる: `python .claude/skills/readme-images/scripts/shrink.py server <一時フォルダ>/server-*.png` (`*` はスクリプトが展開する)

- レジ端末 (`server-terminals`) は、登録していない「本店 レジ 2」にペアリングコードを発行し、ダイアログを出した状態で撮る (コードは 10 分で失効する)
- 「本店 レジ 2」が登録済みなら、emulator skill の `admin_steps.py revoke` で登録を解除してから撮る
- 新しい画面を README に足すときは、`server_steps.py` の `PAGES` と README の表に足す

## 端末

撮るのはエミュレータだけにする (emulator skill の手順でインストールして操作する)。

1. 画面を下の表の状態にする
2. emulator skill の `emu.py shot <一時フォルダ>/terminal-xxx.png` で撮る
3. そろえる: `python .claude/skills/readme-images/scripts/shrink.py terminal <一時フォルダ>/terminal-*.png`

| 画像 | 画面の状態 |
| --- | --- |
| `terminal-home` | ホーム (担当とシフトが開いた状態) |
| `terminal-sales` | 販売 (明細が数行ある状態) |
| `terminal-payment` | 会計 (預かりを入力した状態) |
| `terminal-receipt` | レシート |
| `terminal-setup` | 初期設定 (カメラを許可し、サーバ URL とペアリングコードを入力した状態。登録は押さない) |
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
| `terminal-pin` | PIN の入力 (スタッフ選択で担当を選び、2 桁入力した電卓のシート) |

- 新しい画面を README に足すときは、README の表とこの SKILL の表に足す

## 帳票

管理画面にログインした画面から帳票の PDF を保存し、Chrome の PDF 表示 (ツールバーなし、1 ページを画面に合わせる) で撮る。

1. 手順を作る: `python .claude/skills/readme-images/scripts/report_steps.py --out <一時フォルダ>`
   - 一部だけなら `--only report-order`
2. 撮る: `node .claude/skills/readme-images/scripts/capture_server.mjs <一時フォルダ>/steps.json 1240 1754` (A4 の比)
3. 撮った画像を見て、対象と中身を確認する (確認のために入力した備考などが写っていないか)
4. そろえる: `python .claude/skills/readme-images/scripts/shrink.py report <一時フォルダ>/report-*.png` (用紙を切り出し、600x849 の枠の中央に置く)

| 画像 | 帳票の対象 (開発用の DB から選ぶ) |
| --- | --- |
| `report-order` | 受注票 (前受金のある未完了の受注) |
| `report-purchase-order` | 発注書 (備考のない発注済みの発注) |
| `report-shift` | 精算レポート (販売のある精算済みのシフト) |
| `report-daily-sales` | 売上日報 (締め済みの店舗 × 営業日) |
| `report-receipt` | レシートの控え (直近の販売のうち明細の多いもの) |

- 新しい帳票を README に足すときは、`report_steps.py` の `REPORTS`、README の表、この SKILL の表に足す

## 仕上げ

- 置き換えた画像を開いて、縮小で文字が潰れていないかを確認する
- README の説明 (表の下の行) が画面と合っているかを確認する
- 撮るために変更したデータ (発行したペアリングコード、足した取引、端末の登録、開いたシフト) は、開発用の DB に残る
  - 端末の登録は解除する
  - シフトは精算する
- DB を作り直すときは、サーバを止めて `pos.db` (Host の出力フォルダ) を消し、起動し直す (初期データが入る)
