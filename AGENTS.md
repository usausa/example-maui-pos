# プロジェクトの規則

仕事の進め方と全体に共通する規則。  
コードの書き方は領域ごとに `.claude/rules/` にある。

## コーディングスタイル

- `.editorconfig` の規則に従う
- メンバー変数に `_` 接頭辞を付けない
- ビルドの警告を出さない。  
  警告の抑止は理由のあるものだけにし、抑止する前に確認する
- 既存ファイルの改行コードは変えない。  
  新しく作るテキストファイルは CRLF
- ソースのコメントは日本語で、意図や制約を書く (コードの言い換えは書かない)
- ソースから設計文書 (節番号・`§`・画面 ID・決定番号) を参照しない (ソースが正)
- `ArgumentNullException.ThrowIfNull` は書かない (CA1062 は無効)
- UI の文言は日本語だけ (ローカライズ資源は持たない)
- 「DTO」という語はコード・名前空間・文書に使わない

## 構成と層

- モノレポ。  
  `server/Pos.Server.slnx` (ASP.NET Core) と `terminal/Pos.Terminal.slnx` (MAUI) は別々に開き、どちらも `shared/` (`Pos.Domain` / `Pos.Contract`) を含む。  
  構成は `docs/architecture.md`
- SQL は Accessor だけが持つ。  
  業務の手順はサーバは `Services/` の `XxxService`、端末は `Usecases/` の `XxxUsecase` に置き、Endpoints / Blazor ページ / ViewModel は入力の検証と表示だけを担う
- フォルダ直下には共通の部品だけを置く。  
  機能に属するもの (名称の辞書、絞り込みの状態、URL の生成、帳票) はサブフォルダか専用のフォルダに、あるフォルダの中でしか使わないものはそのフォルダに置く
- 1 つの Service だけが返す結果型 (`XxxResult`) は、その Service のファイルの先頭で定義する。  
  複数で使う型 (`DataWriteStatus` / `DataWriteResult<T>`) は独自のファイルにする
- `Builder` はテキストや画像の組み立てだけに使う。  
  データの変換は `XxxMapper`、計算は `XxxCalculator`
- 拡張メソッドは複数の項目を意味でまとめて判定するもの (`IsReturnable`) だけに使い、型と同じファイルに書く。  
  単一の値との比較 (`status == TransactionStatus.Voided`) は拡張メソッドにしない
- 文言が入り得るプロパティは `EmptyText` ではなく `Message`
- JSON は camelCase、`null` のプロパティは省略、UTC の日時は `yyyy-MM-ddTHH:mm:ss.fffZ`

## 検証

- 作業の単位ごとに、Release ビルド (サーバ・端末とも警告 0。端末は Debug も)、テスト、InspectCode (両ソリューション、ソリューション全体解析、新しい `--caches-home`、0 件) を通す
- テストは `dotnet run --project` で実行する (`shared/Pos.Domain.Tests` / `server/tests/Pos.Server.UnitTests` / `server/tests/Pos.Server.IntegrationTests`)。  
  `dotnet test` は使わない
- テストは実行順に依存させない (同じフィクスチャを使うテストが登録・削除した行を、件数の検証に含めない)
- 端末の UI の変更はエミュレータで動作を確認する

## 進め方

- コミットとプッシュは指示があったときだけ行う。  
  メッセージは日本語で、要約 1 行と変更点の箇条書き
- パッケージの追加・更新は事前に確認する
- レビューの指摘は「どうあるべきか」の形で規則 (本ファイルと `.claude/rules/`) に追記し、該当する設計文書も直す。  
  経緯は `docs/decisions.md` に書く
