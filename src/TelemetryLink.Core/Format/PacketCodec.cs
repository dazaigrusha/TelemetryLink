using TelemetryLink.Core.Security;

namespace TelemetryLink.Core.Format;

/// <summary>Складання пакета: заголовок + шифротекст + HMAC (FORMAT_SPEC §2).</summary>
public static class PacketWriter
{
    /// <summary>Мінімальна довжина пакета: 43 + 0 + 32.</summary>
    public const int MinimumPacketSize = PacketHeader.SizeInBytes + PacketAuthenticator.TagSize;

    public static byte[] Build(
        PayloadContent content,
        ushort deviceId,
        uint packetSequence,
        byte[] salt,
        byte[] nonce,
        SessionKeys keys)
    {
        // TODO(S2). Порядок операцій: стиснути → зашифрувати → додати код
        // автентичності (Encrypt-then-MAC). Тег рахується над ЗАГОЛОВКОМ
        // І ШИФРОТЕКСТОМ РАЗОМ, а не над відкритим текстом.
        throw new NotImplementedException("PacketWriter.Build");
    }
}

/// <summary>
/// Розбір пакета в порядку, заданому FORMAT_SPEC §5.
/// Порядок ОБОВ'ЯЗКОВИЙ: приймач, який розшифровує до перевірки HMAC,
/// не проходить критерій C5.
/// </summary>
public static class PacketReader
{
    public static ReceiveResult Read(ReadOnlySpan<byte> packet, SessionKeys keys)
    {
        // TODO(S2). Рівно ця послідовність:
        //
        //   1. довжина ≥ 75 байтів                          інакше E_SHORT
        //   2. magic == "TLM1" і version == 1                інакше E_MAGIC
        //   3. довжина == 43 + ciphertext_len + 32           інакше E_LENGTH
        //   4. HMAC над packet[0 .. 43+ct_len-1] У ПОСТІЙНИЙ ЧАС
        //   5. не збігся → ВІДХИЛИТИ (E_HMAC). НЕ розшифровувати, НЕ логувати вміст
        //   6. розшифрувати AES-256-CTR
        //   7. розібрати навантаження                        інакше E_PAYLOAD
        //   8. декодувати кадри                              інакше E_CODE
        //
        // max_packet_bytes ТУТ НЕ ПЕРЕВІРЯЄТЬСЯ: це обмеження передавача,
        // а не умова валідності формату (§3.5). Еталонний ref_04_session.bin
        // має 771 байт при ліміті 512 і мусить прийматися.
        throw new NotImplementedException("PacketReader.Read");
    }
}
