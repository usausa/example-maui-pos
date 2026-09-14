namespace Pos.Server.Services;

// 書き込みの結果。Invalid は各サービスの業務ルール違反 (文言は呼び出し側が持つ)
public enum DataWriteStatus
{
    Success,
    NotFound,
    Duplicate,
    VersionMismatch,
    InUse,
    Invalid
}
