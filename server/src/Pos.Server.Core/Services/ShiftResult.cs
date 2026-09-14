namespace Pos.Server.Services;

using Pos.Server.Models.Entity;
using Pos.Server.Models.Views;

public enum ShiftResultStatus
{
    Success,
    // 同じ id の再送 (登録済みを返す)
    Existing,
    NotFound,
    // 同じ id で内容が異なる
    DuplicateMismatch,
    // 端末に開設中のシフトがある
    TerminalHasOpenShift,
    // 精算済み
    Closed
}

// 開設・精算の結果
public sealed record ShiftResult(ShiftResultStatus Status, ShiftDetail? Detail = null);

public enum CashEventResultStatus
{
    Success,
    Existing,
    DuplicateMismatch,
    ShiftNotFound,
    ShiftClosed
}

// 入出金の結果
public sealed record CashEventResult(CashEventResultStatus Status, CashEventEntity? Entity = null);
