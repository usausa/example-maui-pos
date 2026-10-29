---
paths:
  - "terminal/**"
---
# 端末 (.NET MAUI)

Accessor の書き方は accessor.md、SQL は sql.md、画面は terminal-ui.md に置く。

## 層

- ViewModel は `Services/` の `XxxService` (単機能の部品) と `Usecases/` の `XxxUsecase` (通信 → DB → 完了までの複合的な手順) を呼ぶ。両者はフォルダと名前空間を分ける
- 例外として、`SyncService` は同期の複合処理を持つ Service で、ViewModel からは同期 (`SyncAllAsync`)、再送 (`RetryAsync`)、破棄 (`DiscardAsync`)、送信を促す `Trigger` を呼んでよい (全件の同期と付け替えは `SetupUsecase` が呼ぶ)
- ViewModel は `IDbProvider` と `DataAccessor` の書き込みを使わない。マスタや一覧の読み取り (`QueryXxx`) は直接呼んでよく、書き込み・トランザクション・通信を組み合わせる手順は Usecase に渡す
- オンライン限定の 1 回の呼び出しは、ViewModel から `NetworkService` で呼んでよい
- シフトの要る操作は、ViewModel がシフトが開いているか (`Session.CanTransact`、`IsShiftOpen`) を確かめてから Usecase を呼ぶ (Usecase は開設中を前提にする。精算の後もしばらく `CurrentShift` に閉じたシフトが残る)
- ViewModel のコンストラクタの引数とフィールドは `ILogger<T>` → Component (`IDialog`、`IPopupNavigator`、Essentials) → State (`Settings`、`Session`、`DeviceState`) → Service (`DataAccessor`、`XxxService`、`XxxUsecase`) の順にし (区分の中の順は決めない)、DI 以外のフィールドはその後に置く
- Essentials は DI で受けるのを基本にし (`IAppInfo`、`IDeviceInfo`、`IFileSystem`)、その場で 1 回使う OS の機能 (共有の `Share.Default`、ファイルの場所と同梱ファイルの `FileSystem`、カメラの権限の `Permissions`) は静的に使ってよい (DI の外で動く `CrashReport` も静的に使う)
- Service と Usecase のコンストラクタは、基盤 (`ILogger<T>`、`IDbProvider`) を先にし、その後を Component → State → Service の順にする (既存のクラスは触ったときに揃える)
- View と ViewModel は自動で登録される (Modules の外の `MainPage` と `MainPageViewModel` は手で登録する)
- Service、Usecase、State は `MauiProgram.ConfigureComponents` に Singleton で、画面間の状態 (`XxxContext`、`CustomerDraft`) は transient で手で登録する
- 特定の機能の画面間だけで持ち回る状態 (`XxxContext`、`CustomerDraft`) は Smart.Navigation の Scope プラグインで注入する。各 ViewModel に同じ名前の `[Scope]` プロパティを宣言し、遷移パラメータで渡さない
- 複数の機能から使う途中の画面 (スキャン) は、呼び出し元の機能の状態を保持するために、各機能のコンテキストの `[Scope]` プロパティを持つ
- 使用者に常に紐付く情報 (店舗、端末、担当、シフト) は `Session` に集約する
- 背景のループから `Session` を変えるときは `MainThread.InvokeOnMainThreadAsync` を使う
- `ConfigureAwait(false)` は `HttpService` の中だけで使う (Usecase は await の後に UI スレッドで `Session` を書く)
- UI スレッドを塞ぐ処理 (SkiaSharp の描画、QR の生成、PIN のハッシュの照合) は Service の中で `Task.Run` に回す
- アプリで作る画像 (レシート、QR) は SkiaSharp の `SKImageImageSource` で出し、`ImageSource.FromStream` を使わない (MAUI の画像の読み込み (Glide) は初回の初期化で UI スレッドを止める)
- 取得した商品画像は、保存したファイルを `ImageSource.FromFile` で出す (Glide の縮小・向きの補正・キャッシュを使う。初回の初期化で止まるのは Release では目立たない)
- コードで結ぶバインディング (`EntryBind`、`BarcodeBind` の `SetBinding`) はラムダで書き、文字列のパスの `Binding` を使わない (リフレクションを使わず、トリミングで消えず、名前の誤りがビルドで出る)
- 回線の状態は `DeviceState` から読み、Essentials の `IConnectivity` は使わない (`DeviceInformation` が Android の通知から受け取る)
- 電池の残量と無線 LAN の信号強度は取らない
- 承認の要否は、取消は `StaffLogic.RequiresVoidApproval`、値引は値引の定義の `RequiresApproval` で判定する
- 承認者は `PinService.ChooseApproverAsync` で選び、`ApprovedByStaffId` に入れる (画面で PIN を照合しない)
- Service が `IDialog` と `IPopupNavigator` を使うのは、`PinService`、`NetworkService` のような画面をまたぐ対話だけにする
- 日時は、保存と送信は `DateTime.UtcNow`、営業日は `Session.BusinessDate`、表示は `ViewHelper` で扱う (未処理の例外の記録 (`CrashReport`) は端末で読むだけなので `DateTimeOffset.Now` でよい)
- ログは `Log.cs` の `[LoggerMessage]` に集約し、メソッド名は Info、Warn、Error、Debug で始める

## 通信と同期

- サーバの API は `HttpService` が例外を投げずに `ApiResult<T>` を返す
- オンライン限定の操作は `NetworkService.ExecuteAsync(h => h.Xxx(...))` で呼んで `IsSuccess` を見る。知らせとインジケータは NetworkService が出し、画面で別に知らせるときと、取れなければローカルで進める Usecase は `notify: false` にする
- 続けて取る複数の読み取りは 1 回の `ExecuteAsync` にまとめ、どれかが失敗したら全体を失敗にする (ViewModel と Usecase のどちらも)
- 404 は `NetworkService` が知らせないので、呼び出し側が `IsNotFound` で扱い、知らせるときは対象を入れた固定の文言 (「会員が見つかりません」。探した番号は「: 番号」で添えてよい) にする
- 401 は `ApiContext` がイベントで 1 回だけ知らせ、`MainPageViewModel` が知らせて初期設定へ戻すので、画面と `NetworkService` は知らせない (同期のように `NetworkService` を通さない失敗を画面で知らせるときも、`ApiResult.IsUnauthorized` を除く)
- ほかの操作の失敗を知らせるときは `ApiResult.Message` (Problem Details の title、なければ状態の文言) をそのまま出す (前に「同期できませんでした」のような 1 行を添えてよい。取得できない一覧と詳細は、結果に重ねるときは固定の「取得できません」、知らせて一覧へ戻るときは対象を入れた「取引を取得できません」にする)
- 取引・シフト・入出金・在庫の変動は、Usecase がローカル DB と Outbox (`SyncService.CreateEntry`) を 1 つのトランザクションで書き、`UpdateCountsAsync()` と `Trigger()` で送信を促す
- 端末で記録した返品は、元の取引 (端末の記録) の返品済みの数量に同じトランザクションで足し、返品の取消で戻す。サーバから取った元の取引には、まだ送っていない取消と返品を反映する
- 送った取引をサーバの応答で置き換えるときは、端末だけが知る記録を残す (販売にはこの端末で記録した取り消していない返品の数量を足し、取消を送る前の取引は取消の状態と情報を残す。送信は発生順なので、後の記録はまだ応答に入っていない)
- ローカル DB の 1 文だけの書き込みにはトランザクションを使わない (読んでから書き換えるときは、書き込みが 1 文でも 1 つのトランザクションにする)
- Outbox の種類を足すときは `OutboxKind`、`SyncService.SendAsync`、`SyncService.Reassign`、`ViewHelper.Name(OutboxKind)`、`DisplayNameConverter` を揃える
- 端末の設定は `State/Settings` (IPreferences、キーは `nameof`) に置く
- トークンは `CredentialService` 経由で `ISecureStorage` に置き、`ApiContext` に写す
- ViewModel は `IPreferences` と `ISecureStorage` を直接使わず、`Settings` と `CredentialService` を使う
- `ISecureStorage` は鍵が壊れると読み書きと削除が Java の例外になるので、`CredentialService` で捕まえて保存領域を消し、未登録として扱う

## 置き場所と名前

| 種類 | 置き場所 |
| --- | --- |
| アプリの起動と画面の枠 (`App`、`MainPage`、`MainPageViewModel`、`MauiProgram`、DI のファクトリの生成の指定 `GeneratedFactory`)、ログ (`Log.cs`)、拡張メソッド (`Extensions.cs`)、権限 (`Permissions.cs`)、プロジェクト全体の指定 (`GlobalUsing.cs`、`GlobalSuppressions.cs`、`AssemblyInfo.cs`) | 直下 |
| 使用者と端末の状態 (`Settings`、`Session`、`DeviceState`、`StartupState`) | `State/` |
| 画面共通の基盤 (`AppViewModelBase`、`AppDialogViewModelBase`、`ViewId`、`DialogId`、`Parameters`、`PopupNavigatorExtensions`、`ValidationHelper`) | `Modules/` 直下 |
| 画面と、機能の画面間の状態 (`XxxContext`、`CustomerDraft`) | `Modules/{機能}/` |
| 共通のダイアログ | `Modules/Dialogs/` |
| 表示用の書式と文言、業務ルールの文言 (`ViewHelper`) | `Modules/Helpers/` |
| アプリに依存しない処理だけ (日付の書式の `DateTimeHelper`、LIKE のパターンの `Data/SqlHelper` (サーバの `SqlHelper` とは別物)、DB と JSON の変換器) | `Helpers/`、`Helpers/Data/`、`Helpers/Json/` |
| XAML の Converter (画面固有のものも) | `Converters/` |
| 画面をまたぐモデル (カートは `Models/Cart/`) | `Models/{機能}/` |
| DB の行 | `Models/Entity/` |
| 単機能の部品 (`XxxService`)、サーバとの通信の型 (`ApiContext`、`ApiResult`、`ProblemResponse`、`ApiNames`)、ローカル DB (`DataAccessor`、`DataProfile`、`Sql/`)、テキストや画像の組み立て (`XxxBuilder`) | `Services/` |
| 複合的な手順 (`XxxUsecase`) と、その変換 (`XxxMapper`) と計算 (`XxxCalculator`) | `Usecases/` |
| 見た目の部品 | `Controls/` |
| 添付プロパティ (`XxxOption`、`EntryBind`、`BarcodeBind`、`Border`、`Scroll`) | `Behaviors/` |
| マークアップ拡張とアイコン (`AppIcons`) | `Markup/` |
| F キーと表題の仕組み (`ShellProperty`、`ShellUpdateBehavior`、`IShellControl`、`ShellEvent`) | `Shell/` |
| ViewModel から View を動かす仕組み (`EntryController`、`BarcodeController`) | `Messaging/` |
| Smart.Navigation と MauiComponents のプラグイン | `Extender/` |
| プラットフォームのサービス | `Components/` |
| 未処理の例外の記録 (`CrashReport`) | `Diagnostics/` |
| プラットフォーム固有の実装 | 元のファイルの隣の `Xxx.android.cs` / `Xxx.ios.cs` (partial。`Pos.Terminal.csproj` が振り分ける) |
| Android の入口と共通の処理 (`MainActivity`、`MainApplication`、`AndroidHelper`) | `Platforms/Android/` 直下 |
| 元のない Android だけのクラス (`DirectFontImageSourceService`、Java の型の拡張 `JavaObjectExtensions`) | `Platforms/Android/` の下の用途のフォルダ (`Graphics/`、`Helpers/`。直下には置かない) |

- 同じ名前のプロパティを写すだけの変換は代入を並べず、Smart.Mapper の `[Mapper] private static partial` で使うクラスに書く
- 複数の Usecase が使う変換と、Usecase から切り出す長い手書きの変換は `XxxMapper` にする
- 一覧のプロパティは要素の `[Mapper]` を `[MapCollection]` に渡して写し、入れ子の 1 件は `[MapNested]` で写す
- 一覧そのものを返す変換は `[Mapper]` にできない (SMP0007) ので、別の元から一覧を作るときは `[MapUsing]` に渡す `Select(ToXxx).ToList()` で写す
- 同じ名前でも写してはいけない列 (開設中の途中の集計など) は `[MapIgnore]` にし、理由をコメントに書く
- 複数の入力 (カート、計算結果、文脈) を組み合わせる変換と、行番号や条件で値が変わる変換は手で書く
