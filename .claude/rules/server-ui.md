---
paths:
  - "server/src/Pos.Server.Host/Components/**"
  - "server/src/Pos.Server.Host/wwwroot/**"
  - "server/src/Pos.Server.Host/Models/Forms/**"
  - "server/src/Pos.Server.Host/Application/View*.cs"
---
# 管理画面 (Blazor)

## ページ

- 描画モードは対話型 (プリレンダリングなし) にする。エラーと 404 は再実行で描画され、ログインは Cookie を書くフォームを送るので、`[ExcludeFromInteractiveRouting]` で静的 SSR にし、`[AllowAnonymous]` と `EmptyLayout` を付ける
- ページは既定でログインが要る (`Pages/_Imports.razor` の `[Authorize]`)。管理者だけの画面は `[Authorize(Policy = Policies.Administrator)]` を付ける
- 管理者だけの操作のボタンは `<AuthorizeView Policy="@Policies.Administrator">` で囲み (行の中は `Context="auth"`)、同じ操作の API のポリシーと揃える
- ログイン中のアカウントは `[CascadingParameter] Task<AuthenticationState>` から `AuthClaims.AccountOf` で読む
- ページは `PageComponentBase` を継承する。読み込みは `LoadAsync`、書き込みは `RunAsync(操作, 再読み込み)`、結果の知らせは `NotifyResult` を使い、Service には `CancellationToken` を渡す
- 変更の通知 (`ChangeNotificationService`) を購読する画面は、イベントの中で読み込まず、タイマーを掛け直して `InvokeAsync` で読み直し、Dispose で購読を外す
- ページは `TimeProvider` を注入しない。期間の既定は `ReportService.Today` / `ResolvePeriod`、通信中の表示は `TerminalService.IsOnline`、登録時刻は Service に任せる
- razor の表示用の加工は `Application/ViewHelper` と `ViewExtensions` だけに置き、ページで書式 (`:N0`、`ToString("MM/dd")`) を組まない
- 一覧の日時は年なしの `ToShortDateTimeText()`、詳細は `ToDateTimeText()` にする
- CSV / PDF などのダウンロード URL はページで組み立てず、`Application/Urls/ExportUrls` で作る

## 見た目

- スタイルは `wwwroot/css/app.css` のクラスに集約する。`.razor.css` を作らず、要素に `Style=` / `style=` を書かない (テーブルの列幅も `w-120` のような幅クラスを app.css に定義して使う)。MudBlazor のユーティリティクラス (`pa-3`、`mud-width-full`、`font-weight-bold`) と併記する
- 状態のチップは `ViewHelper` で (文言, 色, アイコン) の組にし、`StatusChip` (`MudChip` の `Icon`) で出す
- 横スクロールする `MudDataGrid` (`grid-nowrap`) では操作列を `StickyRight="true"` にし、名称のように折り返してよい列は `CellClass="cell-wrap"` にする (`grid-nowrap` が表の `width: max-content` を `100%` に戻すので、幅が足りないときだけ折り返す)
- 絞り込みの入力欄は行の残りいっぱいに伸びるので、期間 (`MudDateRangePicker`) は `filter-range`、文字の検索欄は `search-field` で上限を付け、他は `min-w-*` にする
- `MudTable` の `FooterContent` は `<tr>` の中に描画されるので `MudTFootRow` を書かず `MudTd` を直接置く (太字は `FooterClass`)
- 見出しに付ける件数の `MudBadge` は見出しの右に縦中央で並べる (`Origin="Origin.CenterRight"`、`BadgeClass="ml-1"`)。`Overlap` で右上に重ねない (見出しの余白の分だけ行から浮く)
- `MudChart` の描画領域は 650×400 の比率で `Height` に合わせて拡大される (幅は高さで決める)。金額の軸は `YAxisFormat`、ラベルは短く (日別は月日だけ)、13 本以上は `XAxisLabelRotation` で斜めにする
