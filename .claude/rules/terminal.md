---
paths:
  - "terminal/**"
---
# 端末 (.NET MAUI)

Accessor の書き方は accessor.md、SQL は sql.md、画面は terminal-ui.md に置く。

## 層

- ViewModel は `Services/` の `XxxService` (単機能の部品) と `Usecases/` の `XxxUsecase` (通信 → DB → 完了までの複合的な手順) を呼ぶ。両者はフォルダと名前空間を分ける
- ViewModel は `IDbProvider` と `DataAccessor` の書き込みを使わない。マスタや一覧の読み取り (`QueryXxx`) は直接呼んでよく、書き込み・トランザクション・通信を組み合わせる手順は Usecase に渡す
- オンライン限定の 1 回の呼び出しは、ViewModel から `NetworkService` で呼んでよい
- コンストラクタの引数とフィールドは `ILogger<T>` → Component (`IDialog`、`IPopupNavigator`、Essentials) → State (`Settings`、`Session`、`DeviceState`) → Service (`IDbProvider`、`DataAccessor`、`XxxService`、`XxxUsecase`) の順にし、DI 以外のフィールドはその後に置く
- View と ViewModel は自動で登録される。Service、Usecase、State は `MauiProgram.ConfigureComponents` に Singleton で、画面間の状態 (`XxxContext`) は transient で手で登録する
- 特定の機能の画面間だけで持ち回る状態 (`XxxContext`、`CustomerDraft`) は Smart.Navigation の Scope プラグインで注入する。各 ViewModel に同じ名前の `[Scope]` プロパティを宣言し、遷移パラメータで渡さない
- スキャンのような途中の画面は、呼び出し元の機能の状態を保持するために各コンテキストのプロパティを持つ。使用者に常に紐付く情報 (店舗、端末、担当、シフト) は `Session` に集約する
- 背景のループから `Session` を変えるときは `MainThread.InvokeOnMainThreadAsync` を使う
- UI スレッドを塞ぐ処理 (SkiaSharp の描画、PIN のハッシュの照合) は Service の中で `Task.Run` に回す
- 承認が要る操作は `StaffLogic` で要否を判定し、`PinService.ChooseApproverAsync` で選んだ承認者を `ApprovedByStaffId` に入れる (画面で PIN を照合しない)
- Service が `IDialog` / `IPopupNavigator` を使うのは、`PinService`、`NetworkService` のような画面をまたぐ対話だけにする
- 日時は、保存と送信は `DateTime.UtcNow`、営業日は `Session.BusinessDate`、表示は `ViewHelper` で扱う
- ログは `Log.cs` の `[LoggerMessage]` に集約する

## 通信と同期

- サーバの API は `HttpService` が例外を投げずに `ApiResult<T>` を返す。オンライン限定の操作は `NetworkService.ExecuteAsync(h => h.Xxx(...))` で呼んで `IsSuccess` を見る (知らせとインジケータは NetworkService が出す。画面で重ねて出すときは `notify: false`)
- 401 は `ApiContext` が一度だけ知らせて初期設定へ戻すので、画面で 401 を扱わない。409 / 422 は `ApiResult.Message` (Problem Details の title) をそのまま出す
- 取引・シフト・入出金・在庫の変動は、Usecase がローカル DB と Outbox (`SyncService.CreateEntry`) を 1 つのトランザクションで書き、`UpdateCountsAsync()` と `Trigger()` で送信を促す
- Outbox の種類を足すときは `OutboxKind`、`SyncService.SendAsync`、`ViewHelper.Name(OutboxKind)` を揃える
- 端末の設定は `State/Settings` (IPreferences、キーは `nameof`) に置き、トークンは `CredentialService` 経由で `ISecureStorage` に置いて `ApiContext` に写す。ViewModel はどちらも直接使わない

## 置き場所と名前

- 画面共通の基盤 (`AppViewModelBase`、`ViewId`、`DialogId`、`Parameters`、`PopupNavigatorExtensions`) は `Modules/` 直下に置く
- 表示用の書式と文言、業務ルールの文言 (`ViewHelper`) は `Modules/Helpers/` に置く
- `Helpers/` にはアプリに依存しない処理だけを置く。日付の書式は `Helpers/DateTimeHelper`、LIKE のパターンの組み立ては `Helpers/Data/SqlHelper` (サーバの `SqlHelper` とは別物)
- Converter は `Converters/` にまとめ、画面固有の Converter でも Modules の下には置かない
- 共通のダイアログは `Modules/Dialogs/`、画面をまたぐモデルは `Models/{機能}/` (カートは `Models/Cart/`) に置く
- Usecase だけが使う変換 (`XxxMapper`) と計算 (`XxxCalculator`) は `Usecases/`、`XxxBuilder` は `Services/`、見た目の部品は `Controls/`、プラットフォームのサービスは `Components/` に置く
