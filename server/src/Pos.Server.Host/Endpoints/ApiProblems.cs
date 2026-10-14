namespace Pos.Server.Host.Endpoints;

using Pos.Contract.Transactions;
using Pos.Domain.Logic;
using Pos.Server.Services;

// Problem Details (errorCode / errors / expected 付き)
public static class ApiProblems
{
    public static IResult BadRequest(string title) =>
        Problem(StatusCodes.Status400BadRequest, ErrorCode.ValidationError, title);

    public static IResult NotFound(string title = "対象が見つかりません") =>
        Problem(StatusCodes.Status404NotFound, ErrorCode.NotFound, title);

    public static IResult PayloadTooLarge(string title) =>
        Problem(StatusCodes.Status413PayloadTooLarge, ErrorCode.ValidationError, title);

    public static IResult UnsupportedMediaType(string title) =>
        Problem(StatusCodes.Status415UnsupportedMediaType, ErrorCode.ValidationError, title);

    public static IResult DuplicateCode(string title = "コードが重複しています") =>
        Problem(StatusCodes.Status409Conflict, ErrorCode.DuplicateCode, title);

    public static IResult DuplicateIdMismatch() =>
        Problem(StatusCodes.Status409Conflict, ErrorCode.DuplicateIdMismatch, "同じ ID で内容の異なるデータが登録済みです");

    public static IResult VersionMismatch() =>
        Problem(StatusCodes.Status409Conflict, ErrorCode.VersionMismatch, "他で更新されています");

    public static IResult InUse(string title) =>
        Problem(StatusCodes.Status422UnprocessableEntity, ErrorCode.InUse, title);

    // 端末のトークンと、本文・クエリの店舗・端末が一致しない
    public static IResult TerminalMismatch() =>
        Problem(StatusCodes.Status403Forbidden, ErrorCode.TerminalMismatch, "この端末からは操作できません");

    public static IResult PairingCodeInvalid() =>
        Problem(StatusCodes.Status422UnprocessableEntity, ErrorCode.PairingCodeInvalid, "ペアリングコードが正しくないか、期限が切れています");

    public static IResult TerminalHasOpenShift() =>
        Problem(StatusCodes.Status409Conflict, ErrorCode.TerminalHasOpenShift, "この端末には開設中のシフトがあります");

    public static IResult ShiftNotFound(string title = "シフトが見つかりません") =>
        Unprocessable(ErrorCode.ShiftNotFound, title);

    public static IResult ShiftClosed(string title) =>
        Unprocessable(ErrorCode.ShiftClosed, title);

    // 締める営業日に未精算のシフトがある
    public static IResult ShiftStillOpen(int openShiftCount) =>
        Unprocessable(ErrorCode.ShiftStillOpen, "未精算のシフトがあります", $"未精算 {openShiftCount} 件");

    public static IResult AlreadyClosed() =>
        Problem(StatusCodes.Status409Conflict, ErrorCode.AlreadyClosed, "既に締め済みです");

    // 取込の行の誤り (errors のキーは行番号)
    public static IResult ImportRowsInvalid(IReadOnlyDictionary<string, string[]> errors) =>
        Problem(StatusCodes.Status422UnprocessableEntity, ErrorCode.ValidationError, "取り込めない行があります", $"{errors.Count} 行に誤りがあります", errors);

    // 書き込みの失敗 (Success 以外) を応答にする
    public static IResult FromStatus(DataWriteStatus status, string? duplicateTitle = null, string? inUseTitle = null, string? invalidTitle = null) =>
        status switch
        {
            DataWriteStatus.NotFound => NotFound(),
            DataWriteStatus.Duplicate => DuplicateCode(duplicateTitle ?? "コードが重複しています"),
            DataWriteStatus.VersionMismatch => VersionMismatch(),
            DataWriteStatus.InUse => InUse(inUseTitle ?? "使用中のため削除できません"),
            DataWriteStatus.Invalid => Unprocessable(ErrorCode.ValidationError, invalidTitle ?? "指定が不正です"),
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };

    // 業務ルール違反 (先頭のエラーを title / errorCode に、全件を errors に)
    public static IResult FromValidation(TransactionValidation validation, TransactionCalculateResponse? expected)
    {
        var first = validation.Errors[0];
        var errors = validation.Errors
            .GroupBy(static x => x.LineId?.ToString("D") ?? "transaction", StringComparer.Ordinal)
            .ToDictionary(static g => g.Key, static g => g.Select(static x => ApiRuleText.Of(x.Reason)).ToArray(), StringComparer.Ordinal);
        return Problem(StatusCodes.Status422UnprocessableEntity, first.Code, ApiRuleText.Of(first.Reason), null, errors, expected);
    }

    public static IResult FromViolation(RuleError violation)
    {
        return Unprocessable(violation.Code, ApiRuleText.Of(violation.Reason));
    }

    // 状態コードと ErrorCode の組は公開のメソッドで決める (ハンドラが直接組めないように private にする)
    private static ProblemHttpResult Problem(int status, ErrorCode code, string title, string? detail = null, IReadOnlyDictionary<string, string[]>? errors = null, TransactionCalculateResponse? expected = null)
    {
        var extensions = new Dictionary<string, object?>(StringComparer.Ordinal) { ["errorCode"] = code.ToCode() };
        if (errors is not null)
        {
            extensions["errors"] = errors;
        }

        if (expected is not null)
        {
            extensions["expected"] = expected;
        }

        return TypedResults.Problem(statusCode: status, title: title, detail: detail, extensions: extensions);
    }

    private static ProblemHttpResult Unprocessable(ErrorCode code, string title, string? detail = null) =>
        Problem(StatusCodes.Status422UnprocessableEntity, code, title, detail);
}
