---
paths:
  - "server/src/Pos.Server.Host/Components/**"
  - "server/src/Pos.Server.Host/wwwroot/**"
  - "server/src/Pos.Server.Host/Models/Forms/**"
  - "server/src/Pos.Server.Host/Application/View*.cs"
  - "server/src/Pos.Server.Host/Application/State/**"
  - "server/src/Pos.Server.Host/Application/Urls/**"
---
# 管理画面 (Blazor)

## ページ

- 描画モードは対話型 (プリレンダリングなし) にする
- エラーと 404 は再実行で描画され、ログインは Cookie を書くフォームを送るので、`[ExcludeFromInteractiveRouting]` で静的 SSR にし、`[AllowAnonymous]` と `EmptyLayout` を付ける
- ページは既定でログインが要る (`Pages/_Imports.razor` の `[Authorize]`)。管理者だけの画面は `[Authorize(Policy = Policies.Administrator)]` を付ける
- 管理者だけの操作のボタンは `<AuthorizeView Policy="@Policies.Administrator">` で囲み (行の中は `Context="auth"`)、同じ操作の API のポリシーと揃える
- ページはログイン中のアカウントを `[CascadingParameter] Task<AuthenticationState>` から読む
- 対話型のページは `PageComponentBase` を継承する (読み書きのない `AccessDenied` は除く)
- 読み込みは `LoadAsync`、表 (`MudDataGrid` の `ServerData`) は `LoadGridAsync`、書き込みは `RunAsync(操作, 再読み込み)` で行い、Service には `CancellationToken` (表は `ServerData` が渡すもの) を渡す
- 書き込みの結果は、マスタ (`DataWriteStatus`) は `NotifyResult` で、Service 固有の結果 (`XxxResult`) はページの `Notify(XxxResult, 成功の文言)` で知らせる
- `LoadAsync`、`LoadGridAsync`、`RunAsync` は受けた例外をバナーに出してログに記録するので、ページで重ねて記録しない
- 変更の通知 (`ChangeNotificationService`) を購読する画面は、イベントの中で読み込まず、タイマーを掛け直して `InvokeAsync` で読み直し、Dispose で購読を外す
- razor の表示用の加工は `Application/ViewHelper` と `ViewExtensions` だけに置き、ページで書式 (`:N0`、`ToString("MM/dd")`) を組まない (グラフの軸の書式とラベル、入力部品の `DateFormat` と表のページャーの `InfoFormat`、値がないときと差し引く額に付ける「-」、チップと色を条件で選ぶ式は除く)
- 列挙型の文言は、チップで出すものは `ViewHelper` のチップ、文字で出すもの (表のセル、見出し) は `ViewExtensions` の `ToDisplayName` から引き、ページとダイアログに書かない (絞り込みと入力の選択肢は、絵文字や注記を添えた文言をその場に書いてよい)
- 金額は `ToYen()` (3 桁区切り、単位なし) にし、表の列は見出しで金額とわかるようにする。ダッシュボードと売上集計の金額のカードだけ「¥」を前に付ける (金種と、% と並ぶ値引の額は「円」を後ろに付ける)
- 件数・人数・枚数は `ToCountText()` (3 桁区切り) にする (タブとバッジの数は除く)
- 一覧の日時と近い日時 (ペアリングの有効期限) は年なしの `ToShortDateTimeText()`、詳細は `ToDateTimeText()` にする
- 例外として、会員の一覧と現在庫 (会員一覧の登録日、会員詳細のタブの表、現在庫の更新日時) は、何年も前の日時が並ぶので年付きの `ToDateTimeText()` にする
- 詳細のダイアログの中の表 (受注の前受金、締めのシフト) の日時は、一覧と同じにする
- CSV や PDF のダウンロード URL はページで組み立てず、`Application/Urls/ExportUrls` で作る
- 店舗の絞り込みは `StoreSelect` にし、選んだ店舗は `StoreFilterState` でページの間で共有する
- ページの見出し (`h5`) の先頭には、画面設計のナビのグループと同じ色付きの絵文字を付ける (受注、日次締め、発注、設定 QR は画面ごとの絵文字)

## ダイアログとフォーム

- フォームを編集して返すダイアログ (編集の `XxxEditDialog` と、`PointAdjustDialog` のような操作のダイアログ) は `EditDialogBase<TForm>` を継承し、`Models/Forms/` の `XxxForm` を編集する (`OrderEditDialog`、`PurchaseOrderEditDialog`、`InventoryReceiptEditDialog`、`InventoryTransferEditDialog` は、明細をまたぐ検証の誤りを出すので継承しない)
- フォームの検証は `XxxFormValidator` (FluentValidation) に書く
- フォームと Entity の変換は `ToForm` と `ToEntity` にし、明細を持つフォームは詳細から `FromDetail` (出荷・受領の確認は `FromReceipt`、`FromTransfer`) で作り、`ToParameter`、`ToLines`、`ToDetail`、`ToQuantities` で渡す
- 詳細のダイアログから操作 (編集、取消など) を始めるときは、ダイアログが `XxxDialogResult` (`XxxDialogAction` と対象) を返して閉じ、操作はページが行う
- ダイアログは書き込まずに結果を返し、書き込みはページが `RunAsync` で行う (例外として、商品の取込のダイアログはプレビューと反映を中で行う)
- 詳細を読むダイアログは `OnInitializedAsync` で `CancellationToken.None` を渡して読み、読み込みの間は `MudProgressLinear` を出す (空の表示を先に出さない)
- 詳細のダイアログの中の別の画面へのリンク (`MudLink`、`MudButton` の `Href`) は、`MudDialog.Close()` で閉じてから移る (新しいタブで開く PDF は除く)
- `MudDialogProvider` は `MainLayout` の `ErrorBoundary` の中にだけ置く (ダイアログの読み込みの例外も `ErrorBoundary` が受け、回線の未処理例外にしない)
- エラー表示の間は `MudDialogProvider` がないので、`ErrorBoundary` の外の部品 (AppBar、NavMenu) からダイアログを開かない

## 見た目

- スタイルは `wwwroot/css/app.css` のクラスに集約し、`.razor.css` を作らず、要素に `Style=`、`style=` を書かない
- テーブルの列幅も `w-120` のような幅クラスを app.css に定義して使う
- MudBlazor のユーティリティクラス (`pa-3`、`mud-width-full`、`font-weight-bold`) は、app.css のクラスと併記してよい
- 状態のチップは `ViewHelper` で (文言, 色, アイコン) の組にし、`StatusChip` (`MudChip` の `Icon`) で出す
- 横スクロールする `MudDataGrid` は `grid-nowrap` と `HorizontalScrollbar="true"` にし、操作列を `StickyRight="true"` にする
- 幅が足りないときに折り返してよい列 (長くなる名称や、商品をまとめた列) は `CellClass="cell-wrap"` にし、`min-w-*` で最小の幅を付ける (`grid-nowrap` が表の `width: max-content` を `100%` に戻すので、幅が足りないときだけ折り返す)
- 絞り込みと帳票の条件の入力欄は行の残りいっぱいに伸びるので、期間 (`MudDateRangePicker`) は `filter-range`、単一の日付 (`MudDatePicker`) は `filter-date` で上限を付ける
- 文字の検索欄 (`MudTextField`) は `min-w-*` と `search-field` を併記する (`min-w-*` が 240px より広いと、幅はその値で止まる)
- 選択 (`MudSelect`、`StoreSelect`、商品を選ぶ `MudAutocomplete`) は `min-w-*` だけにし、行に合わせて伸ばす (`MudAutocomplete` の `Class` は外側の枠に付かず、上限を付けると隙間ができる)
- `MudTable` の `FooterContent` は `<tr>` の中に描画されるので `MudTFootRow` を書かず `MudTd` を直接置く (太字は `FooterClass`)
- 見出しに付ける件数の `MudBadge` は見出しの右に縦中央で並べる (`Origin="Origin.CenterRight"`、`BadgeClass="ml-1"`)。`Overlap` で右上に重ねない (見出しの余白の分だけ行から浮く)
- `MudChart` の大きさは `Height` で決める (描画領域は 650×400 の比率で高さに合わせて拡大される)
- グラフの金額の軸は `YAxisFormat` にし、ラベルは短く (日別は月日だけ)、13 本以上は `XAxisLabelRotation` で斜めにする
