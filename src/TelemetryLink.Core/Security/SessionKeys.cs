namespace TelemetryLink.Core.Security;

/// <summary>
/// Ключі сеансу, виведені з пароля (FORMAT_SPEC §4.1).
///
/// PBKDF2 виконується ОДИН раз на сеанс: сіль однакова для всіх пакетів, отже
/// ключі теж однакові. Виклик KDF на кожен пакет — помилка реалізації:
/// 100 000 ітерацій почнуть домінувати в часі t і зіпсують компоненту T метрики Q.
///
/// КРОК S2.
/// </summary>
public sealed class SessionKeys
{
    public const int Iterations = 100_000;
    public const int SaltSize = 16;
    private const int DerivedLength = 64;

    private SessionKeys(byte[] encryptionKey, byte[] macKey)
    {
        EncryptionKey = encryptionKey;
        MacKey = macKey;
    }

    /// <summary>K_enc — 32 байти, ключ AES-256.</summary>
    public byte[] EncryptionKey { get; }

    /// <summary>K_mac — 32 байти, ключ HMAC-SHA256.
    /// Використання ОДНОГО ключа для обох операцій є порушенням специфікації
    /// і фіксується на захисті.</summary>
    public byte[] MacKey { get; }

    public static SessionKeys Derive(string password, ReadOnlySpan<byte> salt)
    {
        // TODO(S2):
        //   master = PBKDF2-HMAC-SHA256(password, salt, 100000, dkLen = 64)
        //   K_enc  = master[0..31]
        //   K_mac  = master[32..63]
        //
        // Пароль — UTF-8 БЕЗ завершального нуля.
        // Бібліотека дозволена: Rfc2898DeriveBytes.Pbkdf2.
        throw new NotImplementedException("SessionKeys.Derive");
    }

    /// <summary>Випадкова сіль сеансу.</summary>
    public static byte[] GenerateSalt()
        => System.Security.Cryptography.RandomNumberGenerator.GetBytes(SaltSize);
}
