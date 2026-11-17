---
name: verify
description: 作業の単位の検証 (ビルドの警告 0、テスト、InspectCode の指摘 0、改行コード、文書の改行) を 1 回で行う。作業を閉じる前とコミットの前に使う
---

# 検証

AGENTS.md の「検証」を 1 回で確認する。
スクリプトはリポジトリのルートで実行する (Windows では `python`、他では `python3`)。

```bash
python .claude/skills/verify/scripts/verify.py
```

## 対象

- 対象を絞るときは `server`、`terminal`、`files` を並べる (例: `verify.py server files`)
- 対象を省くと、すべてを行う
- `server`
  - `server/Pos.Server.slnx` の Release のリビルド
  - `shared/Pos.Domain.Tests`、`server/tests/Pos.Server.UnitTests`、`server/tests/Pos.Server.IntegrationTests` の `dotnet run --project`
  - InspectCode
  - サーバのビルドが失敗したときは、テストを実行せずに NG にする
- `terminal`
  - `terminal/Pos.Terminal.slnx` の Release と Debug のリビルド
  - InspectCode
- `files`
  - 変更したファイル (git の未コミット分) の改行コード (CRLF)
  - 変更した `docs/*.md` と README の改行 (1 行 1 文、「。」の後に 2 スペース)
- InspectCode は実行ごとに新しいキャッシュで解析し、終わったらキャッシュを消す (古い解析結果を返さないように)

## オプション

- `--fix` で改行コードと文書の改行を直す
- `--all-docs` で変更のない文書も確認する
- `--no-inspect` は途中の確認用
  - 作業の単位の検証では、InspectCode を省かない
- ログは `--out` のフォルダ (既定は一時フォルダの `pos-verify`) に残る

## 時間の目安

- サーバのビルドとテストは数分、端末のビルド (Release と Debug) は数分、InspectCode は 1 ソリューションで数分かかる
- 時間がかかるので、Claude はバックグラウンドで実行し、終了の通知を待つ
- 途中で直したときは、変更した側 (`server` / `terminal`) だけを再実行する
  - 最後に全体を 1 回通す

## 前提

- .NET SDK 11 と Android のワークロード (端末)
- InspectCode は `jb` (JetBrains.ReSharper.GlobalTools を `dotnet tool install -g` でインストールする)
- サーバの Release の出力 (`bin/Release`) から動かしているサーバは、止めてから実行する (ビルドが上書きできない)
- 端末のビルドと `emu.py install` を同時に実行しない (共有の `obj` が壊れる)

## 結果の見方

- 最後の行が「すべて OK」なら完了
- NG の項目は、名前と件数、先頭の指摘を出す
- NG のときは、ログを読んで直す
- 警告と InspectCode の指摘は、抑止する前に利用者に確認する (AGENTS.md)
- 改行コードの NG は、新しく作ったファイルが LF のまま (Claude の Write は LF で書く) か、Git Bash の `sed -i` で LF になったことが多い
  - `--fix` で直してから、もう一度 `files` を確認する
