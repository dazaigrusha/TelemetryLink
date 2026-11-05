using System.Buffers.Binary;
using System.Security.Cryptography;

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
        if (nonce.Length != NonceSize) throw new ArgumentException("Nonce має містити 12 байтів.", nameof(nonce));
        if (destination.Length < BlockSize) throw new ArgumentException("Потрібно 16 байтів.", nameof(destination));
        if (blockIndex == 0) throw new ArgumentOutOfRangeException(nameof(blockIndex));
        nonce.CopyTo(destination);
        BinaryPrimitives.WriteUInt32BigEndian(destination.Slice(12, 4), blockIndex);
    }

    /// <summary>
    /// Перетворює дані XOR-ом із гамою.
    /// Nonce має бути УНІКАЛЬНИМ для кожного пакета в межах одного ключа:
    /// повторення пари (ключ, nonce) розкриває відкритий текст.
    /// </summary>
    public static byte[] Transform(ReadOnlySpan<byte> data, byte[] encryptionKey, ReadOnlySpan<byte> nonce)
    {
        ArgumentNullException.ThrowIfNull(encryptionKey);
        if (encryptionKey.Length != 32) throw new ArgumentException("Ключ AES-256 має містити 32 байти.", nameof(encryptionKey));
        if (nonce.Length != NonceSize) throw new ArgumentException("Nonce має містити 12 байтів.", nameof(nonce));
        using var aes = Aes.Create();
        aes.Key = encryptionKey;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        using var encryptor = aes.CreateEncryptor();
        var result = new byte[data.Length];
        var counter = new byte[BlockSize];
        var gamma = new byte[BlockSize];
        try
        {
            for (int offset = 0; offset < data.Length;)
            {
                uint index = (uint)(offset / BlockSize) + 1;
                BuildCounterBlock(nonce, index, counter);
                encryptor.TransformBlock(counter, 0, BlockSize, gamma, 0);
                int count = Math.Min(BlockSize, data.Length - offset);
                for (int i = 0; i < count; i++) result[offset + i] = (byte)(data[offset + i] ^ gamma[i]);
                offset += count;
            }
            return result;
        }
        finally { CryptographicOperations.ZeroMemory(gamma); }
    }

    /// <summary>
    /// Рекомендований спосіб: 8 випадкових байтів сеансу || uint32_BE(packet_seq).
    /// Гарантує унікальність nonce у межах сеансу без зберігання стану.
    /// </summary>
    public static byte[] BuildNonce(ReadOnlySpan<byte> sessionPrefix8, uint packetSequence)
    {
        if (sessionPrefix8.Length != 8) throw new ArgumentException("Префікс сеансу має містити 8 байтів.", nameof(sessionPrefix8));
        var nonce = new byte[NonceSize];
        sessionPrefix8.CopyTo(nonce);
        BinaryPrimitives.WriteUInt32BigEndian(nonce.AsSpan(8), packetSequence);
        return nonce;
    }
}
