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
        // TODO(S2): HMACSHA256.HashData(macKey, headerAndCiphertext)
        throw new NotImplementedException("PacketAuthenticator.ComputeTag");
    }

    /// <summary>
    /// Порівняння в ПОСТІЙНИЙ ЧАС. Звичайне порівняння масивів виходить на
    /// першій розбіжності і дозволяє підбирати тег побайтово за часом відповіді.
    /// </summary>
    public static bool Verify(ReadOnlySpan<byte> headerAndCiphertext, ReadOnlySpan<byte> tag, byte[] macKey)
    {
        // TODO(S2): CryptographicOperations.FixedTimeEquals, а не SequenceEqual.
        throw new NotImplementedException("PacketAuthenticator.Verify");
    }
}
