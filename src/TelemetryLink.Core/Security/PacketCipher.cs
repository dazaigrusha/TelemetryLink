namespace TelemetryLink.Core.Security;

/// <summary>
/// AES-256 у режимі CTR (FORMAT_SPEC §4.2).
///
/// CTR — потоковий режим: шифрування і розшифрування — одна й та сама операція
/// XOR із гамою. Доповнення немає, тому ciphertext_len = довжині відкритого тексту.
///
/// КРОК S2.
/// </summary>
public static class PacketCipher
{
    public const int NonceSize = 12;
    private const int BlockSize = 16;

    /// <summary>
    /// Блок лічильника: nonce (12 байтів) || uint32_BE(i), де i — номер блоку
    /// в межах пакета, ПОЧИНАЮЧИ З 1. Значення i = 0 не використовується.
    ///
    /// Це найчастіша помилка в проєкті: з нумерацією від нуля збігається все,
    /// крім байтів шифротексту.
    /// </summary>
    public static void BuildCounterBlock(ReadOnlySpan<byte> nonce, uint blockIndex, Span<byte> destination)
    {
        // TODO(S2)
        throw new NotImplementedException("PacketCipher.BuildCounterBlock");
    }

    /// <summary>
    /// Перетворює дані XOR-ом із гамою.
    /// Nonce має бути УНІКАЛЬНИМ для кожного пакета в межах одного ключа:
    /// повторення пари (ключ, nonce) розкриває відкритий текст.
    /// </summary>
    public static byte[] Transform(ReadOnlySpan<byte> data, byte[] encryptionKey, ReadOnlySpan<byte> nonce)
    {
        // TODO(S2):
        //   keystream = AES-256-ECB(K_enc, counter_block(1)) || AES-256-ECB(K_enc, counter_block(2)) || ...
        //   ciphertext[j] = plaintext[j] XOR keystream[j]
        //
        // ECB тут — примітив для генерації гами, а не режим шифрування даних.
        // Aes.Create() з Mode = CipherMode.ECB, Padding = PaddingMode.None.
        throw new NotImplementedException("PacketCipher.Transform");
    }

    /// <summary>
    /// Рекомендований спосіб: 8 випадкових байтів сеансу || uint32_BE(packet_seq).
    /// Гарантує унікальність nonce у межах сеансу без зберігання стану.
    /// </summary>
    public static byte[] BuildNonce(ReadOnlySpan<byte> sessionPrefix8, uint packetSequence)
    {
        // TODO(S2)
        throw new NotImplementedException("PacketCipher.BuildNonce");
    }
}
