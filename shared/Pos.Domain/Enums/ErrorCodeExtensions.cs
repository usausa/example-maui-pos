namespace Pos.Domain.Enums;

using System.Text;

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
