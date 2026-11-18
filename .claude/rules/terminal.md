---
paths:
  - "terminal/**"
---
# 端末 (.NET MAUI)

## 層

- ViewModel は、`Services/` の `XxxService` (単機能の部品) と `Usecases/` の `XxxUsecase` (通信 → DB → 完了までの複合的な手順) を呼ぶ
- Service と Usecase は、フォルダと名前空間を分ける
- 例外として、`SyncService` は同期の複合処理を持つ Service にする
- ViewModel から `SyncService` の同期 (`SyncAllAsync`)、再送 (`RetryAsync`)、破棄 (`DiscardAsync`)、送信を促す `Trigger` を呼んでよい
- `SyncService` の全件の同期と付け替えは、`SetupUsecase` が呼ぶ
- ViewModel は `IDbProvider` を使わず、`DataAccessor` の書き込みも使わない
- マスタや一覧の読み取り (`QueryXxx`) は、ViewModel から直接呼んでよい
- 書き込み・トランザクション・通信を組み合わせる手順は、Usecase に任せる
- オンライン限定の 1 回の呼び出しは、ViewModel から `NetworkService` で呼んでよい
- シフトの要る操作は、ViewModel がシフトが開いているか (`Session.CanTransact`、`IsShiftOpen`) を確認してから Usecase を呼ぶ (精算の後もしばらく `CurrentShift` に閉じたシフトが残る)
- Usecase は、シフトの開設中を前提にする
- Service が `IDialog` と `IPopupNavigator` を使うのは、`PinService`、`NetworkService` のような画面をまたぐ対話だけにする

## DI

- ViewModel のコンストラクタの引数とフィールドは、`ILogger<T>` → Component (`IDialog`、`IPopupNavigator`、Essentials) → State (`Settings`、`Session`、`DeviceState`) → Service (`DataAccessor`、`XxxService`、`XxxUsecase`) の順にする (区分の中の順は決めない)
- ViewModel の DI 以外のフィールドは、DI のフィールドの後に置く
- Service と Usecase のコンストラクタは、基盤 (`ILogger<T>`、`IDbProvider`) を先にし、その後を Component → State → Service の順にする
- 既存の Service と Usecase は、触ったときにこの順にそろえる
- Essentials は DI で受けるのを基本にする (`IAppInfo`、`IDeviceInfo`、`IFileSystem`)
- その場で 1 回使う OS の機能は、静的に使ってよい (共有の `Share.Default`、ファイルの場所と同梱ファイルの `FileSystem`、カメラの権限の `Permissions`)
- DI の外で動く `CrashReport` も、Essentials を静的に使う
- View と ViewModel は自動で登録される
- `Modules/` の外の `MainPage` と `MainPageViewModel` は、手で登録する
- Service、Usecase、State は、`MauiProgram.ConfigureComponents` に Singleton で手で登録する
- 画面間の状態 (`XxxContext`、`CustomerDraft`) は、`MauiProgram.ConfigureComponents` に transient で手で登録する

## 状態

- 特定の機能の画面の間だけで持ち回る状態 (`XxxContext`、`CustomerDraft`) は、Smart.Navigation の Scope プラグインで注入する
- その状態を使う各 ViewModel に、同じ名前の `[Scope]` プロパティを宣言する
- この状態は、遷移パラメータで渡さない
- 複数の機能から使う途中の画面 (スキャン) は、各機能のコンテキストの `[Scope]` プロパティを持つ (呼び出し元の機能の状態を保持するため)
- 使用者に常に紐付く情報 (店舗、端末、担当、シフト) は `Session` に集約する
- 回線・バッテリー・無線 LAN の状態は `DeviceState` から読み、Essentials の `IConnectivity` と `IBattery` は使わない (`DeviceInformation` が Android の通知から受け取るため)

## スレッドと画像

- バックグラウンドのループから `Session` を変えるときは、`MainThread.InvokeOnMainThreadAsync` を使う
- `ConfigureAwait(false)` は `HttpService` の中だけで使う (Usecase は await の後に UI スレッドで `Session` を書くため)
- UI スレッドをブロックする処理 (SkiaSharp の描画、QR の生成、PIN のハッシュの照合) は、Service の中で `Task.Run` に回す
- アプリで作る画像 (レシート、QR) は SkiaSharp の `SKImageImageSource` で出し、`ImageSource.FromStream` を使わない (MAUI の画像の読み込みに使う Glide は、初回の初期化で UI スレッドを止めるため)
- 取得した商品画像は、保存したファイルを `ImageSource.FromFile` で出す (Glide の縮小・向きの補正・キャッシュを使うため。初回の初期化で止まるのは Release では目立たない)

## バインディング

- コードで設定するバインディング (`EntryBind`、`BarcodeBind` の `SetBinding`) はラムダで書き、文字列のパスの `Binding` を使わない (リフレクションを使わず、トリミングで消えず、名前の誤りがビルドで出るため)

## 承認

- 承認の要否は、取消は `StaffLogic.RequiresVoidApproval`、値引は値引の定義の `RequiresApproval` で判定する
- 承認者は `PinService.ChooseApproverAsync` で選び、`ApprovedByStaffId` に入れる
- 画面で PIN を照合しない

## 日時とログ

- 日時は、保存と送信は `DateTime.UtcNow`、営業日は `Session.BusinessDate`、表示は `ViewHelper` で扱う
- クラッシュレポート (`CrashReport`) の日時は、端末で読むだけなので `DateTimeOffset.Now` でよい
- ログは `Log.cs` の `[LoggerMessage]` に集約する
- ログのメソッド名は Info、Warn、Error、Debug で始める

## 通信

- サーバの API は、`HttpService` が例外を投げずに `ApiResult<T>` を返す
- オンライン限定の操作は `NetworkService.ExecuteAsync(h => h.Xxx(...))` で呼び、`IsSuccess` を見る
- 失敗のメッセージとインジケータは `NetworkService` が出す
- 画面で別にメッセージを出すときと、取得できなければローカルで進める Usecase は、`notify: false` にする
- 続けて行う複数の読み取りは 1 回の `ExecuteAsync` にまとめ、どれかが失敗したら全体を失敗にする (ViewModel と Usecase のどちらも)
- 404 は `NetworkService` がメッセージを出さないので、呼び出し側が `IsNotFound` で扱う
- 404 のメッセージを出すときは、対象を入れた固定の文言 (「会員が見つかりません」) にする
- 404 のメッセージには、探した番号を「: 番号」で添えてよい
- 401 は、`ApiContext` がイベントで 1 回だけ通知し、`MainPageViewModel` がメッセージを出して初期設定へ戻す
- 画面と `NetworkService` は 401 のメッセージを出さない
- 同期のように `NetworkService` を通さない失敗のメッセージを画面で出すときも、`ApiResult.IsUnauthorized` を除く
- ほかの操作の失敗のメッセージには、`ApiResult.Message` (Problem Details の title、なければステータスの文言) をそのまま使う
- `ApiResult.Message` の前に、「同期できませんでした」のような 1 行を添えてよい
- 取得できない一覧と詳細を結果に重ねるときは、固定の「取得できません」にする
- 取得できずにメッセージを出して一覧へ戻るときは、対象を入れた「取引を取得できません」にする

## 同期とローカル DB

- 取引・シフト・入出金・在庫の変動は、Usecase がローカル DB と Outbox (`SyncService.CreateEntry`) を 1 つのトランザクションで書く
- 書いた後は、`UpdateCountsAsync()` と `Trigger()` で送信を促す
- 端末で記録した返品は、元の取引 (端末の記録) の返品済みの数量に、同じトランザクションで足す
- 返品の取消では、元の取引の返品済みの数量を戻す
- サーバから取得した元の取引には、未送信の取消と返品を反映する
- 送った取引をサーバのレスポンスで置き換えるときは、端末だけが知る記録を残す (送信は発生順なので、後の記録はまだレスポンスに入っていない)
  - 販売には、この端末で記録した取り消していない返品の数量を足す
  - 取消を送る前の取引は、取消の状態と情報を残す
- ローカル DB の 1 文だけの書き込みには、トランザクションを使わない
- 読んでから書き換えるときは、書き込みが 1 文でも 1 つのトランザクションにする
- Outbox の種類を足すときは、`OutboxKind`、`SyncService.SendAsync`、`SyncService.Reassign`、`ViewHelper.Name(OutboxKind)`、`DisplayNameConverter` をそろえる

## 設定とトークン

- 端末の設定は `State/Settings` に置く (`IPreferences`、キーは `nameof`)
- トークンは `CredentialService` を通して `ISecureStorage` に保存し、`ApiContext` にコピーする
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
| アプリに依存しない処理だけ (日付の書式の `DateTimeHelper`、要素の探索とフォーカスの移動の `ElementHelper`、LIKE のパターンの `Data/SqlHelper`、DB と JSON のコンバータ) | `Helpers/`、`Helpers/Data/`、`Helpers/Json/` |
| XAML の Converter (画面固有のものも) | `Converters/` |
| 画面をまたぐモデル (カートは `Models/Cart/`) | `Models/{機能}/` |
| ローカル DB のエンティティ | `Models/Entity/` |
| 単機能の部品 (`XxxService`)、サーバとの通信の型 (`ApiContext`、`ApiResult`、`ProblemResponse`、`ApiNames`)、ローカル DB (`DataAccessor`、`DataProfile`、`Sql/`)、テキストや画像の組み立て (`XxxBuilder`) | `Services/` |
| 複合的な手順 (`XxxUsecase`) と、その変換 (`XxxMapper`) と計算 (`XxxCalculator`) | `Usecases/` |
| 表示の部品 | `Controls/` |
| 添付プロパティ (`XxxOption`、`EntryBind`、`BarcodeBind`、`Border`、`Scroll`、`Focus`) | `Behaviors/` |
| マークアップ拡張とアイコン (`AppIcons`) | `Markup/` |
| F キー、タイトル、ステータスバーの仕組み (`ShellProperty`、`ShellUpdateBehavior`、`IShellControl` と `FunctionState`、`ShellEvent`) | `Shell/` |
| ViewModel から View を動かす仕組み (`EntryController` と `EntryCompleteCommand`、`BarcodeController`) | `Messaging/` |
| Smart.Navigation と MauiComponents のプラグイン | `Extender/` |
| プラットフォームのサービス | `Components/` |
| クラッシュレポート (`CrashReport`) | `Diagnostics/` |
| プラットフォーム固有の実装 | 元のファイルの隣の `Xxx.android.cs` / `Xxx.ios.cs` (partial。`Pos.Terminal.csproj` が振り分ける) |
| Android のエントリポイントと共通の処理 (`MainActivity`、`MainApplication`、`AndroidHelper`) | `Platforms/Android/` 直下 |
| 元のない Android だけのクラス (`DirectFontImageSourceService`、Java の型の拡張 `JavaObjectExtensions`) | `Platforms/Android/` の下の用途のフォルダ (`Graphics/`、`Helpers/`。直下には置かない) |

- 端末の `Helpers/Data/SqlHelper` は、サーバの `SqlHelper` とは別物

## 共通の部品

- 共通の部品は、テンプレート (template-maui の `Template.MobileApp`) にもある次のファイルを指す
  - `Behaviors/`、`Extender/`、`Shell/`、`Components/`、`Diagnostics/`、`Messaging/`、`Helpers/`、`Converters/`、`Platforms/Android/` の C# と XAML のファイル
  - `Markup/ViewIdExtension`、`App`、`AppViewModelBase`、`AppDialogViewModelBase`、`ValidationHelper`、`DeviceState`、`StartupState`、`NumberInputParameter`
- 共通の部品はテンプレートと同じにし、変えるのは名前空間と using の並びだけにする
- `AndroidManifest.xml` は、アプリごとに違うので共通の部品から除く
- 共通の部品のコメントは、区切り (`//----` で挟んだ見出し)、ReSharper の指示、空の catch の `// Ignore` だけにする
- 区切りの線の形 (`//----` / `// ----`) もテンプレートのままにする
- ほかのコメントは、テンプレートからも同じに消す
- 共通の部品には、日本語のコメントの規則と、コンストラクタの引数の順の規則を当てない
- 例外として、テンプレートにしかない機能やパッケージが要る行は入れない
  - `AppHostBuilderExtensions` の PDF・Stepper・Carousel
  - `MainActivity` の通知
  - `MainApplication` の地図のキー
  - `App.xaml` の Palettes
  - `App.xaml.cs` のフォアグラウンドの状態 (`Session.IsForeground`)
- 例外として、`MainActivity` の名前と、`App.xaml.cs` の初期化の内容と同期の開始は POS のものにする
- POS だけの動きと表示は、共通の部品を変えずに、別のファイル (`Extender/NavigationUnfocusPlugin`、`Messaging/EntryCompleteCommand`、`Models/Input/DigitsInputParameter`、`Controls/LabelChip`) か、アプリ固有のファイル (`MainPage`、`MainPageViewModel`、`Extensions.cs`、`Log.cs`) に置く
- 共通の部品が使うアプリ固有のファイルのメンバー (`Extensions.cs` の `SetDefaultFocus` と `XxxChangedAsObservable`、`Log.cs` のメッセージ) は、テンプレートと同じにする (違うと、テンプレートの更新を取り込んだときに壊れるため)

## 変換

- 同じ名前のプロパティをコピーするだけの変換は、代入を並べず、使うクラスに Smart.Mapper の `[Mapper] private static partial` で書く
- 複数の Usecase が使う変換と、Usecase から切り出す長い手書きの変換は `XxxMapper` にする
- 一覧のプロパティは、要素の `[Mapper]` を `[MapCollection]` に渡してコピーする
- 入れ子の 1 件は `[MapNested]` でコピーする
- 一覧そのものを返す変換は `[Mapper]` にできないので (SMP0007)、別の元から一覧を作るときは `[MapUsing]` に渡す `Select(ToXxx).ToList()` でコピーする
- 同じ名前でもコピーしてはいけない列 (開設中の途中の集計など) は `[MapIgnore]` にする
- `[MapIgnore]` にした理由はコメントに書く
- 複数の入力 (カート、計算結果、コンテキスト) を組み合わせる変換と、行番号や条件で値が変わる変換は手で書く
