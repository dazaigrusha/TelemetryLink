using System.Security.Cryptography;

namespace TelemetryLink.Core.Security;

/// <summary>
/// HMAC-SHA256 над заголовком і шифротекстом разом (FORMAT_SPEC §4.3).
/// Схема Encrypt-then-MAC: тег рахується від ШИФРОТЕКСТУ, і приймач перевіряє
/// його ДО будь-якої обробки вмісту.
///
/// КРОК S2.
/// </summary>
public static class PacketAuthenticator
{
    public const int TagSize = 32;

    public static byte[] ComputeTag(ReadOnlySpan<byte> headerAndCiphertext, byte[] macKey)
    {
        ArgumentNullException.ThrowIfNull(macKey);
        return HMACSHA256.HashData(macKey, headerAndCiphertext);
    }

    /// <summary>
    /// Порівняння в ПОСТІЙНИЙ ЧАС. Звичайне порівняння масивів виходить на
    /// першій розбіжності і дозволяє підбирати тег побайтово за часом відповіді.
    /// </summary>
    public static bool Verify(ReadOnlySpan<byte> headerAndCiphertext, ReadOnlySpan<byte> tag, byte[] macKey)
    {
        if (tag.Length != TagSize) return false;
        return CryptographicOperations.FixedTimeEquals(ComputeTag(headerAndCiphertext, macKey), tag);
    }
}
