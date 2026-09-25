---
name: verify
description: 作業の単位の検証 (サーバ・端末の Release ビルドの警告 0、端末の Debug ビルド、3 つのテストプロジェクト、両ソリューションの InspectCode の指摘 0、変更したファイルの改行コード、文書の改行) を 1 回で行う。コードや文書を変えたあと、作業を閉じる前やコミットの前に使う。
---

# 検証

AGENTS.md の「検証」を 1 回で確かめる。
スクリプトはリポジトリのルートで実行する (Windows では `python`、他では `python3`)。

```bash
python .claude/skills/verify/scripts/verify.py
```

- 対象を絞るときは `server` / `terminal` / `files` を並べる (例: `verify.py server files`)。省くとすべて
- `server`: `server/Pos.Server.slnx` の Release ビルド (作り直し)、`shared/Pos.Domain.Tests`・`server/tests/Pos.Server.UnitTests`・`server/tests/Pos.Server.IntegrationTests` の `dotnet run --project`、InspectCode
- `terminal`: `terminal/Pos.Terminal.slnx` の Release と Debug のビルド (作り直し)、InspectCode
- `files`: 変更したファイル (git の未コミット分) の改行コード (CRLF) と、`docs/*.md`・README の改行 (1 行 1 文、「。」の後に 2 スペース)
- `--fix` で改行コードと文書の改行を直す。`--all-docs` で変更のない文書も確かめる
- `--no-inspect` は途中の確認用。作業の単位の検証では InspectCode を省かない
- ログは `--out` のフォルダ (既定は一時フォルダの `pos-verify`) に残る。NG のときはログを読んで直す

## 時間の目安

- サーバのビルドとテストは数分、端末のビルド (Release と Debug) は数分、InspectCode は 1 ソリューションで数分かかる
- 時間がかかるので、Claude はバックグラウンドで実行し、終わった通知を待つ
- 途中で直したときは、変えた側だけ (`server` / `terminal`) をやり直し、最後に全体を 1 回通す

## 前提

- .NET SDK 10 と Android のワークロード (端末)
- InspectCode は `jb` (JetBrains.ReSharper.GlobalTools を `dotnet tool install -g` で入れる)
- テストは `dotnet test` ではなく `dotnet run --project` で実行する (xunit v3 の実行形式)

## 結果の見方

- 最後の行が「すべて OK」なら完了。NG の項目は名前と件数、先頭の指摘を出す
- 警告は抑止する前に確認する (AGENTS.md)。InspectCode の指摘も同じ
- 改行コードの NG は、新しく作ったファイルが LF のまま (Claude の Write は LF で書く) のことが多い。`--fix` で直してから、もう一度 `files` を確かめる
