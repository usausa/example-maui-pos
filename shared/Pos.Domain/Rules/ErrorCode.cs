namespace Pos.Domain.Rules;

using System.Text;

// api-design §5。JSON の errorCode は ToCode() (UPPER_SNAKE_CASE)
public enum ErrorCode
{
    ValidationError,
    NotFound,
    DuplicateIdMismatch,
    VersionMismatch,
    DuplicateCode,
    TerminalHasOpenShift,
    ShiftNotFound,
    ShiftClosed,
    ShiftTerminalMismatch,
    DuplicateReceiptNo,
    ProductNotFound,
    PriceOverrideNotAllowed,
    CalculationMismatch,
    PaymentMismatch,
    CustomerRequired,
    OriginalNotFound,
    OriginalNotReturnable,
    ReturnQuantityExceeded,
    HasReturns,
    InUse
}

// 受理するが応答の warnings[] に含める
public enum WarningCode
{
    PointBalanceNegative,
    ProductInactive,
    InventoryNegative
}

public static class ErrorCodeExtensions
{
    public static string ToCode(this ErrorCode code) => ToUpperSnakeCase(code.ToString());

    public static string ToCode(this WarningCode code) => ToUpperSnakeCase(code.ToString());

    private static string ToUpperSnakeCase(string name)
    {
        var builder = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if ((i > 0) && char.IsUpper(c))
            {
                builder.Append('_');
            }

            builder.Append(char.ToUpperInvariant(c));
        }

        return builder.ToString();
    }
}
