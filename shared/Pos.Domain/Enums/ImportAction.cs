namespace Pos.Domain.Enums;

// 取込の行の結果。変更のない行は書き込まない (端末の差分同期を増やさない)
public enum ImportAction
{
    Insert,
    Update,
    Unchanged,
    Error
}
