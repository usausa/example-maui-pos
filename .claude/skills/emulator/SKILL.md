---
name: emulator
description: 端末アプリ (MAUI Android) をエミュレータに入れて動作を確かめる。ビルドと配置、画面の撮影・タップ・F キー・電卓の入力、ペアリングでの登録、PIN のログイン、機内モード、アプリの設定の読み書きと確認後の戻しを行う。端末の UI や通信を変えたときに使う。実機には入れない。
---

# エミュレータでの確認

操作はリポジトリのルートで `python .claude/skills/emulator/scripts/emu.py <コマンド>` を使う (Windows では `python`、他では `python3`)。
スクリプトはエミュレータ (`emulator-` で始まる機器) だけを選び、実機が接続されていても使わない。
`adb` や `dotnet build -t:Run` を直接使うときも、必ず `-s emulator-xxxx` / `-p:AdbTarget=-s emulator-xxxx` でエミュレータを指定する。

## 準備

- エミュレータを起動する (`emulator -list-avds` で名前を見て `emulator -avd <名前>`)。`emu.py devices` で使う機器を確かめる
- サーバを起動する。エミュレータからホストへは `http://10.0.2.2:<ポート>/` でつながる
- 確かめる前に、元に戻すための値を控える (`emu.py pref get ApiEndPoint`)
- 入れる: `emu.py install` (Debug。ビルドから起動まで数分かかる)

## 登録とログイン

1. 管理画面の「レジ端末」で対象の端末にペアリングコードを発行する (6 桁、10 分)
2. 端末の初期設定でサーバ URL の欄をタップし、`emu.py text http://10.0.2.2:<ポート>/` で入れる
3. `emu.py fkey 3` (コード) → `emu.py keypad <6 桁> --ok` → `emu.py fkey 4` (登録)。全件の同期が終わると担当の選択に進む
4. 担当を選び、PIN を `emu.py keypad <PIN> --ok` で入れる (初期データは A001 = 0000、M001 = 1111、C001 = 2222、C002 = 3333)

## 操作

- 画面を見る: `emu.py shot <一時フォルダ>/xxx.png` で撮って画像を読む。今の画面の名前は `emu.py screen` (logcat の `Navigated:`)
- 押す: `emu.py tap <x> <y>`。座標は撮った画像 (1080x2400) の画素で、縮小して表示された画像から読むときは倍率を戻す
- F キー: `emu.py fkey <1-4>` (画面下端の 4 つ)
- 電卓のシート: `emu.py keypad <数字>` (`A` = AC、`C` = 1 字消す)、`--ok` で確定
- 文字: `emu.py text <ASCII>` (日本語は送れない)。キーは `emu.py key BACK` / `ENTER` / `DEL`
- オフライン: `emu.py airplane on` / `off`
- 例外の確認: `emu.py logcat --grep "Exception|FATAL"`
- カメラの許可のダイアログは、QR を読む確認でなければ「許可しない」を選ぶ
- 座標は Pixel 6a 相当 (1080x2400) の配置。画面の作りが変わったら撮った画像で確かめる

## 後片付け

- `emu.py stop` でアプリを止める
- 変えた設定を控えた値に戻す (`emu.py pref set ApiEndPoint <元の値>`。アプリを止めてから書き換わる)
- 確かめるために登録した端末は、元が未登録なら設定画面の [登録の解除] で戻す
- 機内モードにしたときは `emu.py airplane off` で戻す
