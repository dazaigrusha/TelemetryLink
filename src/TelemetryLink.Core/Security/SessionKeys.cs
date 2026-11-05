using System.Security.Cryptography;

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
        ArgumentNullException.ThrowIfNull(password);
        if (salt.Length != SaltSize) throw new ArgumentException("Сіль має містити 16 байтів.", nameof(salt));
        byte[] master = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations,
            HashAlgorithmName.SHA256, DerivedLength);
        try { return new SessionKeys(master[..32], master[32..]); }
        finally { CryptographicOperations.ZeroMemory(master); }
    }

    /// <summary>Випадкова сіль сеансу.</summary>
    public static byte[] GenerateSalt()
        => System.Security.Cryptography.RandomNumberGenerator.GetBytes(SaltSize);
}
