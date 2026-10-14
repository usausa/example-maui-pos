---
name: emulator
description: 端末アプリ (MAUI Android) をエミュレータに入れて動作を確かめる。エミュレータの起動と終了、ビルドと配置、画面の撮影・タップ・F キー・電卓の入力、管理画面でのペアリングコードの発行と登録の解除、PIN のログイン、機内モード、カメラの許可、アプリの設定の読み書きと確認後の戻しを行う。端末の UI や通信を変えたときに使う。実機には入れない。
---

# エミュレータでの確認

操作はリポジトリのルートで `python .claude/skills/emulator/scripts/emu.py <コマンド>` を使う (Windows では `python`、他では `python3`)。
スクリプトはエミュレータ (`emulator-` で始まる機器) だけを選び、実機が接続されていても使わない。
`adb` や `dotnet build -t:Run` を直接使うときも、必ず `-s emulator-xxxx` / `-p:AdbTarget=-s emulator-xxxx` でエミュレータを指定する。
`adb` と `emulator` が PATH にないときは、環境変数 `ADB` と `EMULATOR` で場所を渡す (emu.py は Android SDK の既定の場所も探す)。
使う機器は環境変数 `ANDROID_SERIAL` (`emulator-` で始まるものだけ) でも選べる。

## 準備

- エミュレータを起動する: `emu.py boot <AVD の名前>` (名前を省くと AVD の一覧を出す。起動済みなら起動が終わるのを待つだけ)
- 座標 (F キー、電卓) は 1080x2400 (Pixel 6a 相当) の配置。`emu.py devices` が違う大きさを出したら、撮った画像で座標を確かめる
- サーバをバックグラウンドで起動し、`<サーバの URL>/health` が応答するまで待つ。エミュレータからホストへは `http://10.0.2.2:<ポート>/` でつながる
- 確かめる前に、元に戻すための値を控える (`emu.py pref get ApiEndPoint`)
- 入れる: `emu.py install` (Debug。ビルドから起動まで数分かかるので、バックグラウンドで実行する)
- 端末のビルドは同時に 2 つ走らせない (`verify.py terminal` と `emu.py install` を重ねると、共有の `obj` が壊れて署名に失敗する)

## 登録とログイン

1. ペアリングコード (6 桁、10 分) を発行する。管理画面の「レジ端末」の操作なので、手順を作って撮影のスクリプトで実行する
   - `python .claude/skills/emulator/scripts/admin_steps.py pair --out <一時フォルダ> --base <サーバの URL>` (端末は `--terminal <名称>`。既定は「本店 レジ 2」)
   - `node .claude/skills/readme-images/scripts/capture_server.mjs <一時フォルダ>/steps.json` の出力の `"code: 123456"` がコード
2. アプリを起動すると、初期設定がカメラの許可を求める。ダイアログが出ている間に `emu.py key BACK` で断る (座標で押すと許可に当たることがある)
3. サーバ URL の欄をタップし、`emu.py text http://10.0.2.2:<ポート>/` で入れて `emu.py key ENTER` でキーボードを閉じる
   - 前の値が残っているときは、先に `emu.py key MOVE_END` と `emu.py key DEL --repeat 40` で消す
   - 未登録の初期設定で戻るキーを押すと、アプリが背面へ回る。そのときは `emu.py launch` で戻す
4. `emu.py fkey 3` (コード) で電卓のシートを開き、撮って開いたことを確かめてから `emu.py keypad <6 桁> --ok` で入れる
5. `emu.py fkey 4` (登録) を押す。登録済みの端末では「登録し直しますか？」の確認が出る
6. 全件の同期が終わると担当の選択に進むので、`emu.py screen` か撮った画像で待つ
7. 担当を選び、PIN を `emu.py keypad <PIN> --ok` で入れる (初期データは A001 = 0000、M001 = 1111、C001 = 2222、C002 = 3333)

## 操作

- 画面を見る: `emu.py shot <一時フォルダ>/xxx.png` で撮って画像を読む
- 遷移を見る: `emu.py screen` で logcat の `Navigated:` を出す (最後の行が今の画面。Debug だけ)
- 押す: `emu.py tap <x> <y>`。座標は撮った画像 (1080x2400) の画素で、縮小して表示された画像から読むときは倍率を戻す
- F キー: `emu.py fkey <1-4>` (画面下端の 4 つ)
- 電卓のシート: `emu.py keypad <数字>` (`A` = AC、`C` = 1 字消す)、`--ok` で確定
- 電卓は数字の間を待たないので、シートが開いてから押し、`--ok` の前に撮って確かめる
- 処理中と遷移中のタップは捨てられるので、押した後は撮って結果を確かめてから次へ進む
- 文字: `emu.py text <ASCII>` (空白と日本語は送れない)
- キー: `emu.py key BACK`、`ENTER`、`DEL` など (`--repeat N` で繰り返す)
- 送る: `emu.py swipe <x1> <y1> <x2> <y2> [ms]` (設定画面の末尾の [登録の解除] などへ送る)
- 起動と停止: `emu.py launch`、`emu.py stop`
- オフライン: `emu.py airplane on` / `off`
- 例外の確認: `emu.py logcat --grep "Exception|FATAL"` (`--lines` で読む行数を変える)
- カメラの許可: `emu.py camera status` / `grant` / `revoke` (QR の読み取りを確かめるときだけ許可する)
- エミュレータの仮想のカメラの初期の視野には画像を映せない (`virtualscene-image` の壁と台は視野の外) ので、設定 QR の読み取りは利用者に実機での確認を頼む

## Release で確かめる

- `emu.py install --release` は、入っているアプリのデータを消して入れ直す
- データを残すときは、`dotnet build terminal/Pos.Terminal/Pos.Terminal.csproj -f net10.0-android -c Release` の後、`adb -s emulator-xxxx install -r terminal/Pos.Terminal/bin/Release/net10.0-android/pos.terminal-Signed.apk` で上書きする
- エミュレータの空きが足りずに入らないときは、`adb -s emulator-xxxx uninstall pos.terminal` してから入れ、登録し直す
- Release では `emu.py screen` (Navigated のログ) と `emu.py pref` (run-as) が使えないので、遷移は撮った画像で確かめる
- 時間を測るときは、一時的に `Android.Util.Log.Info("POSDBG", ...)` を入れ、操作の前に `adb -s emulator-xxxx shell log -t POSDBG <印>` で印を付けて、logcat の時刻を比べる (測り終えたら外す)

## 後片付け

- `emu.py stop` でアプリを止める
- 変えた設定を控えた値に戻す (`emu.py pref set ApiEndPoint <元の値>`。アプリを止めてから書き換える。キーがなければ足す)
- 確かめるために登録した端末は、端末の [登録の解除] (手元のトークンを消すだけ) に加えて、サーバ側の登録も解除する
  - `python .claude/skills/emulator/scripts/admin_steps.py revoke --out <一時フォルダ> --base <サーバの URL>`
  - `node .claude/skills/readme-images/scripts/capture_server.mjs <一時フォルダ>/steps.json` の出力が `"revoked: …"` か `"not registered: …"` なら済み
- 確かめるために開いたシフトは精算する (未精算のシフトが残ると、その日を締められず、サンプルデータもその端末を使わない)
- 機内モードにしたときは `emu.py airplane off` で戻す
- カメラを許可したときは `emu.py camera revoke` で、次にまた尋ねる状態に戻す
- 確かめるためにエミュレータを起動したときは、`emu.py shutdown` で終了する
