namespace Pos.Domain.Logic;

using System.Security.Cryptography;

// スタッフの PIN のハッシュ (PBKDF2 + ソルト)。サーバが作り、端末は同期した値でオフラインでも照合する
public static class PinHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;

    // 端末で照合するので、ログインが待たされない回数にする
    private const int Iterations = 100_000;

    public static bool IsValidFormat(string? pin) =>
        (pin is not null) && (pin.Length >= Length.PinMinDigits) && (pin.Length <= Length.PinDigits) && pin.All(Char.IsAsciiDigit);

    // ソルト + ハッシュ
    public static byte[] Hash(string pin)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var bytes = Rfc2898DeriveBytes.Pbkdf2(pin, salt, Iterations, HashAlgorithmName.SHA256, HashSize);

        var hash = new byte[SaltSize + HashSize];
        salt.AsSpan().CopyTo(hash);
        bytes.AsSpan().CopyTo(hash.AsSpan(SaltSize));
        return hash;
    }

    public static bool Verify(string pin, byte[]? hash)
    {
        if ((hash is null) || (hash.Length != SaltSize + HashSize))
        {
            return false;
        }

        var bytes = Rfc2898DeriveBytes.Pbkdf2(pin, hash.AsSpan(0, SaltSize), Iterations, HashAlgorithmName.SHA256, HashSize);
        return CryptographicOperations.FixedTimeEquals(bytes, hash.AsSpan(SaltSize));
    }
}
