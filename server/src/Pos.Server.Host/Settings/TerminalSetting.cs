namespace Pos.Server.Host.Settings;

public sealed class TerminalSetting : IValidatableObject
{
    // 設定 QR の接続先 (端末から届くこのサーバの URL)。空なら管理画面を開いた URL にする
    // (管理画面を localhost で開いたときや、端末の引けない名前で開いたときに使う)
    public string ApiEndPoint { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if ((ApiEndPoint.Length > 0) &&
            (!Uri.TryCreate(ApiEndPoint, UriKind.Absolute, out var uri) || ((uri.Scheme != Uri.UriSchemeHttp) && (uri.Scheme != Uri.UriSchemeHttps))))
        {
            yield return new ValidationResult("ApiEndPoint は http か https の絶対 URL にしてください", [nameof(ApiEndPoint)]);
        }
    }
}
