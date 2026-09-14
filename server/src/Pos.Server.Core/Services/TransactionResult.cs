namespace Pos.Server.Services;

using Pos.Domain.Logic;
using Pos.Server.Models.Views;

public enum TransactionResultStatus
{
    // 登録・取消した
    Success,
    // 同じ id の再送 (登録済みを返す)
    Existing,
    NotFound,
    // 同じ id で内容が異なる
    DuplicateMismatch,
    // 業務ルール違反 (Validation にエラーと期待値)
    Invalid,
    // 書き込み中に見つかった違反 (Violation)
    Violation
}

// 取引の登録・取消の結果
public sealed class TransactionResult
{
    public TransactionResultStatus Status { get; }

    public TransactionDetail? Detail { get; }

    public IReadOnlyList<RuleWarning> Warnings { get; }

    public TransactionValidation? Validation { get; }

    public RuleError? Violation { get; }

    private TransactionResult(TransactionResultStatus status, TransactionDetail? detail = null, IReadOnlyList<RuleWarning>? warnings = null, TransactionValidation? validation = null, RuleError? violation = null)
    {
        Status = status;
        Detail = detail;
        Warnings = warnings ?? [];
        Validation = validation;
        Violation = violation;
    }

    public static TransactionResult Success(TransactionDetail detail, IReadOnlyList<RuleWarning>? warnings = null) => new(TransactionResultStatus.Success, detail, warnings);

    public static TransactionResult Existing(TransactionDetail detail) => new(TransactionResultStatus.Existing, detail);

    public static TransactionResult NotFound() => new(TransactionResultStatus.NotFound);

    public static TransactionResult DuplicateMismatch() => new(TransactionResultStatus.DuplicateMismatch);

    public static TransactionResult Invalid(TransactionValidation validation) => new(TransactionResultStatus.Invalid, validation: validation);

    public static TransactionResult Violated(RuleError violation) => new(TransactionResultStatus.Violation, violation: violation);
}

// 計算の結果 (登録しない)。Error は先頭の 1 件
public sealed record TransactionCalculation(SalesResult? Result, RuleError? Error);
