namespace Pos.Server.Services;

using System.Buffers.Text;
using System.Security.Cryptography;

using Pos.Domain;
using Pos.Server.Accessors;
using Pos.Server.Models.Entity;
using Pos.Server.Models.Views;

// 発行したペアリングコード
public sealed record TerminalPairingCode(string Code, DateTime ExpiresAt);

// ペアリングの結果。Token は端末に一度だけ渡す (サーバはハッシュだけを持つ)
public sealed record TerminalPairingResult(string? Token, TerminalEntity? Terminal)
{
    public bool IsSuccess => Token is not null;

    public static TerminalPairingResult Invalid() => new(null, null);
}

// 端末の登録: 管理画面でペアリングコードを発行し、端末がコードと引き換えにトークンを受け取る。
// トークンは要求ごとに DB で照合する (解除が次の要求から効く)。端末ごとに有効なトークンは 1 つ
public sealed class TerminalTokenService
{
    private static readonly TimeSpan PairingCodeLifetime = TimeSpan.FromMinutes(10);

    private const int TokenSize = 32;

    private readonly TimeProvider timeProvider;
    private readonly IDbProvider provider;
    private readonly TerminalTokenAccessor tokenAccessor;
    private readonly MasterAccessor masterAccessor;

    public TerminalTokenService(
        TimeProvider timeProvider,
        IDbProvider provider,
        TerminalTokenAccessor tokenAccessor,
        MasterAccessor masterAccessor)
    {
        this.timeProvider = timeProvider;
        this.provider = provider;
        this.tokenAccessor = tokenAccessor;
        this.masterAccessor = masterAccessor;
    }

    // 端末の未使用のコードを置き換える。既存のトークンはペアリングが済むまで有効
    public async ValueTask<TerminalPairingCode?> IssuePairingCodeAsync(Guid terminalId, CancellationToken cancellationToken)
    {
        if (await masterAccessor.QueryTerminalAsync(terminalId, cancellationToken) is not { IsDeleted: false })
        {
            return null;
        }

        var code = await CreatePairingCodeAsync(cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var entity = new TerminalTokenEntity
        {
            Id = Guid.CreateVersion7(),
            TerminalId = terminalId,
            PairingCode = code,
            PairingExpiresAt = now + PairingCodeLifetime,
            CreatedAt = now
        };
        await provider.UsingTxAsync(async (_, tx) =>
        {
            await tokenAccessor.DeletePendingAsync(tx, terminalId, cancellationToken);
            await tokenAccessor.InsertAsync(tx, entity, cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }, cancellationToken);

        return new TerminalPairingCode(code, entity.PairingExpiresAt.Value);
    }

    // コードを消費してトークンを発行し、端末の古いトークンを失効させる。不一致・期限切れ・使用済み・無効な端末は Invalid
    public async ValueTask<TerminalPairingResult> PairAsync(string pairingCode, string deviceName, string? appVersion, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var pending = await tokenAccessor.QueryByPairingCodeAsync(pairingCode, cancellationToken);
        if ((pending is null) || (pending.PairingExpiresAt <= now))
        {
            return TerminalPairingResult.Invalid();
        }

        var terminal = await masterAccessor.QueryTerminalAsync(pending.TerminalId, cancellationToken);
        if (terminal is not { IsActive: true, IsDeleted: false })
        {
            return TerminalPairingResult.Invalid();
        }

        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenSize));
        var paired = await provider.UsingTxAsync(async (_, tx) =>
        {
            await tokenAccessor.UpdateRevokedAsync(tx, terminal.Id, now, cancellationToken);
            if (await tokenAccessor.UpdatePairedAsync(tx, pending.Id, HashToken(token), deviceName, now, cancellationToken) == 0)
            {
                return false;
            }

            await tx.CommitAsync(cancellationToken);
            return true;
        }, cancellationToken);
        if (!paired)
        {
            return TerminalPairingResult.Invalid();
        }

        await masterAccessor.UpdateTerminalSeenAsync(terminal.Id, now, appVersion, cancellationToken);
        terminal.LastSeenAt = now;
        terminal.AppVersion = appVersion ?? terminal.AppVersion;
        return new TerminalPairingResult(token, terminal);
    }

    // 登録の解除 (有効なトークンの失効と、未使用のコードの削除)
    public ValueTask RevokeAsync(Guid terminalId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        return provider.UsingTxAsync(async (_, tx) =>
        {
            await tokenAccessor.UpdateRevokedAsync(tx, terminalId, now, cancellationToken);
            await tokenAccessor.DeletePendingAsync(tx, terminalId, cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }, cancellationToken);
    }

    // 有効なトークンなら端末と店舗 (null = 不明・解除済み・端末が無効)
    public ValueTask<TerminalTokenView?> AuthenticateAsync(string token, CancellationToken cancellationToken) =>
        tokenAccessor.QueryTokenAsync(HashToken(token), cancellationToken);

    public ValueTask<List<TerminalRegistrationView>> QueryRegistrationListAsync(CancellationToken cancellationToken) =>
        tokenAccessor.QueryRegistrationListAsync(cancellationToken);

    private static byte[] HashToken(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));

    // 未使用のコードと重ならない 6 桁
    private async ValueTask<string> CreatePairingCodeAsync(CancellationToken cancellationToken)
    {
        var max = (int)Math.Pow(10, Length.PairingCodeDigits);
        while (true)
        {
            var code = RandomNumberGenerator.GetInt32(max).ToString($"D{Length.PairingCodeDigits}", CultureInfo.InvariantCulture);
            if (await tokenAccessor.QueryByPairingCodeAsync(code, cancellationToken) is null)
            {
                return code;
            }
        }
    }
}
