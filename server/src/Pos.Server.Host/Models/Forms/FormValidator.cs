namespace Pos.Server.Host.Models.Forms;

using FluentValidation;

// MudForm の Validation に渡す項目単位の検証
public abstract class FormValidator<T> : AbstractValidator<T>
{
    public Func<object, string, Task<IEnumerable<string>>> ValidateValue => async (model, propertyName) =>
    {
        var result = await ValidateAsync(ValidationContext<T>.CreateWithOptions((T)model, x => x.IncludeProperties(propertyName)));
        return result.IsValid ? [] : result.Errors.Select(static e => e.ErrorMessage);
    };
}
