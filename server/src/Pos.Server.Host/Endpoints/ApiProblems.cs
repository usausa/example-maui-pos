namespace Pos.Server.Host.Endpoints;

using Pos.Contract.Transactions;
using Pos.Domain.Logic;
using Pos.Server.Services;

// Problem Details (errorCode / errors / expected 付き)
public static class ApiProblems
{
    public static IResult Problem(int status, ErrorCode code, string title, string? detail = null, IReadOnlyDictionary<string, string[]>? errors = null, TransactionCalculateResponse? expected = null)
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

    public static IResult BadRequest(string title) =>
        Problem(StatusCodes.Status400BadRequest, ErrorCode.ValidationError, title);

    public static IResult NotFound(string title = "対象が見つかりません") =>
        Problem(StatusCodes.Status404NotFound, ErrorCode.NotFound, title);

    public static IResult DuplicateCode(string title = "コードが重複しています") =>
        Problem(StatusCodes.Status409Conflict, ErrorCode.DuplicateCode, title);

    public static IResult DuplicateIdMismatch() =>
        Problem(StatusCodes.Status409Conflict, ErrorCode.DuplicateIdMismatch, "同じ ID で内容の異なるデータが登録済みです");

    public static IResult VersionMismatch() =>
        Problem(StatusCodes.Status409Conflict, ErrorCode.VersionMismatch, "他で更新されています。再読み込みしてください");

    public static IResult InUse(string title) =>
        Problem(StatusCodes.Status422UnprocessableEntity, ErrorCode.InUse, title);

    public static IResult Unprocessable(ErrorCode code, string title, string? detail = null) =>
        Problem(StatusCodes.Status422UnprocessableEntity, code, title, detail);

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
}
