---
name: emulator
description: レジ端末アプリをエミュレータで動かして確認する (インストール、撮影、タップ、F キー、電卓、登録とログイン)。端末の UI や通信を変更したときに使う
---

# エミュレータでの確認

操作はリポジトリのルートで `python .claude/skills/emulator/scripts/emu.py <コマンド>` を使う (Windows では `python`、他では `python3`)。

## デバイスの選択

- スクリプトはエミュレータ (`emulator-` で始まるデバイス) だけを選び、実機が接続されていても使わない
- 使うデバイスは、環境変数 `ANDROID_SERIAL` でも選べる (`emulator-` で始まるものだけ)
- `adb` や `dotnet build -t:Run` を直接使うときも、必ずエミュレータを指定する
  - `adb`: `-s emulator-xxxx`
  - `dotnet build -t:Run`: `"-p:AdbTarget=-s emulator-xxxx"`

## 準備

- `adb` と `emulator` が PATH にないときは、環境変数 `ADB` と `EMULATOR` で場所を渡す (emu.py は Android SDK の既定の場所も探す)
- エミュレータを起動する: `emu.py boot <AVD の名前>`
  - 名前を省くと、AVD の一覧を出す
  - 起動済みなら、起動が終わるのを待つだけ
- F キーと電卓の座標は、1080x2400 (Pixel 6a 相当) の画面のもの
  - `emu.py devices` が違う大きさを出したら、撮った画像で座標を確認する
- サーバをバックグラウンドで起動する
  - `<サーバの URL>/health` がレスポンスを返すまで待つ
- エミュレータからホストへは、`http://10.0.2.2:<ポート>/` で接続できる
- 確認の前に、元に戻すための値を控える (`emu.py pref get ApiEndPoint`)
- インストールする: `emu.py install` (Debug)
  - ビルドから起動まで数分かかるので、バックグラウンドで実行する
- 端末のビルドを同時に 2 つ実行しない (`verify.py terminal` と `emu.py install` を重ねると、共有の `obj` が壊れて署名に失敗する)

## 登録とログイン

ペアリングコード (6 桁、10 分で失効) は管理画面の「レジ端末」で発行するので、手順を作って撮影のスクリプトで実行する。

1. 発行の手順を作る: `python .claude/skills/emulator/scripts/admin_steps.py pair --out <一時フォルダ> --base <サーバの URL>`
   - 端末は `--terminal <名称>` で選ぶ (既定は「本店 レジ 2」)
2. 手順を実行する: `node .claude/skills/readme-images/scripts/capture_server.mjs <一時フォルダ>/steps.json`
   - 出力の `"code: 123456"` がペアリングコード
3. アプリを起動すると、初期設定がカメラの許可を求めるので、`emu.py camera deny` で拒否する
   - `camera deny` は、ダイアログが出るのを待ってから戻るキーを送る
   - 未登録の初期設定で、ダイアログのないときに戻るキーを送ると、アプリがバックグラウンドへ回る (`emu.py launch` で戻す)
4. サーバ URL の欄をタップする
5. `emu.py text http://10.0.2.2:<ポート>/` で入力する
   - 前の値が残っているときは、先に `emu.py key MOVE_END` と `emu.py key DEL --repeat 40` で消す
6. `emu.py key ENTER` でキーボードを閉じる
7. `emu.py fkey 3` (コード) で電卓のシートを開く
8. 撮って、シートが開いたことを確認する
9. `emu.py keypad <6 桁> --ok` でペアリングコードを入力する
10. `emu.py fkey 4` (登録) を押す
    - 登録済みの端末では「登録し直しますか？」の確認が出る
11. 全件の同期が終わって担当の選択に進むまで、`emu.py screen` か撮った画像で待つ
12. 担当を選ぶ
13. `emu.py keypad <PIN> --ok` で PIN を入力する (初期データは A001 = 0000、M001 = 1111、C001 = 2222、C002 = 3333)

## 操作

- 画面: `emu.py shot <一時フォルダ>/xxx.png` で撮って、画像を読む
- 遷移: `emu.py screen` で logcat の `Navigated:` を出す (Debug だけ)
  - 最後の行が今の画面
- タップ: `emu.py tap <x> <y>`
  - 座標は撮った画像 (1080x2400) のピクセル
  - 縮小して表示された画像から読むときは、倍率を戻す
- F キー: `emu.py fkey <1-4>` (画面下端の 4 つ)
- 電卓のシート: `emu.py keypad <数字>` (`A` = AC、`C` = 1 字消す)
  - `--ok` で確定する (確定だけなら `emu.py keypad --ok`)
  - 数字の間を待たないので、シートが開いたことを撮って確認してから押す
  - 入力した値を確認するときは、`--ok` を付けずに押して撮り、確定だけを送る
- 処理中と遷移中のタップは捨てられるので、押した後は撮って結果を確認してから次へ進む
- 文字: `emu.py text <ASCII>` (空白と日本語は送れない)
- キー: `emu.py key BACK`、`ENTER`、`DEL` など (`--repeat N` で繰り返す)
- スワイプ: `emu.py swipe <x1> <y1> <x2> <y2> [ms]` (設定画面の末尾の [登録の解除] などへスクロールする)
- アプリの起動と停止: `emu.py launch`、`emu.py stop`
- オフライン: `emu.py airplane on` / `off`
- 例外の確認: `emu.py logcat --grep "Exception|FATAL"` (`--lines` で読む行数を変更する)
- カメラの許可: `emu.py camera status` / `grant` / `revoke` / `deny`
  - QR の読み取りを確認するときだけ許可する
  - 設定 QR の読み取りは、利用者に実機での確認を頼む (エミュレータの仮想カメラの初期の視野に画像を映せないため。`virtualscene-image` の壁と台は視野の外)

## Release での確認

- `emu.py install --release` は、インストール済みのアプリのデータを消して再インストールする
- データを残すときは、Release でビルドしてから上書きでインストールする
  - `dotnet build terminal/Pos.Terminal/Pos.Terminal.csproj -f net11.0-android -c Release`
  - `adb -s emulator-xxxx install -r terminal/Pos.Terminal/bin/Release/net11.0-android/pos.terminal-Signed.apk`
- エミュレータの空きが足りずにインストールできないときは、`adb -s emulator-xxxx uninstall pos.terminal` でアンインストールしてからインストールする
  - インストールの後、再登録する
- Release では `emu.py screen` (Navigated のログ) と `emu.py pref` (run-as) が使えないので、遷移は撮った画像で確認する
- 時間を測るときは、一時的に `Android.Util.Log.Info("POSDBG", ...)` を追加する (測り終えたら削除する)
  - 操作の前に `adb -s emulator-xxxx shell log -t POSDBG <印>` で印を付ける
  - logcat で印とログの時刻を比べる

## クリーンアップ

- 確認のために開いたシフトは精算する (未精算のシフトが残ると、その日を締められず、サンプルデータもその端末を使わない)
- 確認のために登録した端末は、端末の [登録の解除] (手元のトークンを消すだけ) に加えて、サーバ側の登録も解除する
  - 手順を作る: `python .claude/skills/emulator/scripts/admin_steps.py revoke --out <一時フォルダ> --base <サーバの URL>`
  - 手順を実行する: `node .claude/skills/readme-images/scripts/capture_server.mjs <一時フォルダ>/steps.json`
  - 出力が `"revoked: …"` か `"not registered: …"` なら完了
- `emu.py stop` でアプリを止める
- 変更した設定を、控えた値に戻す: `emu.py pref set ApiEndPoint <元の値>`
  - `pref set` は、動いているアプリを止めてから書き換える
  - キーがなければ足す
- 機内モードにしたときは、`emu.py airplane off` で戻す
- カメラを許可したときは、`emu.py camera revoke` で次にまた尋ねる状態に戻す
- 確認のためにエミュレータを起動したときは、`emu.py shutdown` で終了する
