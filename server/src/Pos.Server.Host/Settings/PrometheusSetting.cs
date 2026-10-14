namespace Pos.Server.Host.Settings;

public sealed class PrometheusSetting : IValidatableObject
{
    // メトリクスを HTTP で公開する URI。使うのはホストとポートだけで、パスは /metrics になる。空なら公開しない
    public Uri? Uri { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if ((Uri is not null) && !Uri.IsAbsoluteUri)
        {
            yield return new ValidationResult("Uri は絶対 URL にしてください", [nameof(Uri)]);
        }
    }
}
