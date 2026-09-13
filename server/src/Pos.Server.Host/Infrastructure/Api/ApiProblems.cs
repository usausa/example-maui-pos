namespace Pos.Server.Host.Infrastructure.Api;

using Pos.Domain.Rules;
using Pos.Shared.Transactions;

// api-design §2.4 の Problem Details (errorCode / errors / expected 付き)
public static class ApiProblems
{
    public static IResult Problem(int status, ErrorCode code, string title, string? detail = null, IReadOnlyDictionary<string, string[]>? errors = null, TransactionCalculationResponse? expected = null)
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

    // 業務ルール違反 (先頭のエラーを title / errorCode に、全件を errors に)
    public static IResult FromValidation(TransactionValidation validation, TransactionCalculationResponse? expected)
    {
        var first = validation.Errors[0];
        var errors = validation.Errors
            .GroupBy(static x => x.LineId?.ToString("D") ?? "transaction", StringComparer.Ordinal)
            .ToDictionary(static g => g.Key, static g => g.Select(static x => x.Message).ToArray(), StringComparer.Ordinal);
        return Problem(StatusCodes.Status422UnprocessableEntity, first.Code, first.Message, null, errors, expected);
    }
}
