namespace Pos.Terminal.State;

// 端末設定 (IPreferences)。サーバ URL・店舗・端末はペアリングで決まる
#pragma warning disable CA1724
public sealed class Settings
{
    private readonly IPreferences preferences;

    public Settings(IPreferences preferences)
    {
        this.preferences = preferences;
    }

    public string ApiEndPoint
    {
        get => preferences.Get(nameof(ApiEndPoint), string.Empty);
        set => preferences.Set(nameof(ApiEndPoint), value);
    }

    public Guid? StoreId
    {
        get => Guid.TryParse(preferences.Get(nameof(StoreId), string.Empty), out var id) ? id : null;
        set => preferences.Set(nameof(StoreId), value?.ToString() ?? string.Empty);
    }

    public Guid? TerminalId
    {
        get => Guid.TryParse(preferences.Get(nameof(TerminalId), string.Empty), out var id) ? id : null;
        set => preferences.Set(nameof(TerminalId), value?.ToString() ?? string.Empty);
    }

    // 端末を登録 (ペアリング) した日時。トークンは SecureStorage (CredentialService)
    public DateTime? PairedAt
    {
        get => DateTime.TryParse(preferences.Get(nameof(PairedAt), string.Empty), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value) ? value : null;
        set => preferences.Set(nameof(PairedAt), value?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty);
    }

    // スタッフ選択後にシフト開設済みなら販売画面を直接開く
    public bool OpenSalesAfterLogin
    {
        get => preferences.Get(nameof(OpenSalesAfterLogin), false);
        set => preferences.Set(nameof(OpenSalesAfterLogin), value);
    }

    public bool IsConfigured => !String.IsNullOrEmpty(ApiEndPoint) && (StoreId is not null) && (TerminalId is not null);
}
#pragma warning restore CA1724
