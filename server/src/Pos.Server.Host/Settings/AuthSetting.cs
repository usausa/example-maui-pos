namespace Pos.Server.Host.Settings;

public sealed class AuthSetting
{
    // false で認可を素通しにする (開発・デモ)。ログインと端末のペアリング・トークンの照合は行う。
    // 設定が抜けても認証が外れないように既定は true
    public bool Enabled { get; set; } = true;

    [Range(1, 43200)]
    public int ExpireMinutes { get; set; }

    // ログインとペアリングの試行回数の上限 (接続元ごと、1 分あたり)
    [Range(1, 100_000)]
    public int AttemptsPerMinute { get; set; }

    // アカウントが 1 件もないときに作る管理者
    [Required]
    public string InitialName { get; set; } = default!;

    [Required]
    public string InitialPassword { get; set; } = default!;
}
