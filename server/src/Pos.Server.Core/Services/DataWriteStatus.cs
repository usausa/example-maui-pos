namespace Pos.Server.Services;

// 書き込みの結果。呼び出し側 (API / 管理画面) が応答や通知に写す
public enum DataWriteStatus
{
    Success,
    NotFound,
    Duplicate,
    VersionMismatch,
    InUse,
    Invalid
}
