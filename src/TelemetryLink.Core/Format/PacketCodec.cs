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
        ArgumentNullException.ThrowIfNull(keys);
        if (salt is null || salt.Length != SessionKeys.SaltSize)
            throw new ArgumentException("Сіль має містити 16 байтів.", nameof(salt));
        byte[] plaintext = PayloadWriter.Write(content);
        byte[] ciphertext = PacketCipher.Transform(plaintext, keys.EncryptionKey, nonce);
        var header = new PacketHeader(deviceId, packetSequence, salt, nonce, (uint)ciphertext.Length);
        var packet = new byte[checked(MinimumPacketSize + ciphertext.Length)];
        header.WriteTo(packet);
        ciphertext.CopyTo(packet, PacketHeader.SizeInBytes);
        int tagOffset = PacketHeader.SizeInBytes + ciphertext.Length;
        PacketAuthenticator.ComputeTag(packet.AsSpan(0, tagOffset), keys.MacKey).CopyTo(packet, tagOffset);
        return packet;
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
        if (packet.Length < PacketWriter.MinimumPacketSize) return ReceiveResult.Rejected(RejectionCode.E_SHORT);
        if (!PacketHeader.HasValidMagic(packet)) return ReceiveResult.Rejected(RejectionCode.E_MAGIC);
        var header = PacketHeader.ReadFrom(packet);
        if ((long)PacketWriter.MinimumPacketSize + header.CiphertextLength != packet.Length)
            return ReceiveResult.Rejected(RejectionCode.E_LENGTH, header);
        int tagOffset = packet.Length - PacketAuthenticator.TagSize;
        // Автентичність перевіряємо до дешифрування та розбору навантаження.
        if (!PacketAuthenticator.Verify(packet[..tagOffset], packet[tagOffset..], keys.MacKey))
            return ReceiveResult.Rejected(RejectionCode.E_HMAC, header);
        byte[] plaintext = PacketCipher.Transform(packet.Slice(PacketHeader.SizeInBytes,
            (int)header.CiphertextLength), keys.EncryptionKey, header.Nonce);
        var code = PayloadReader.TryRead(plaintext, out var payload);
        return code == RejectionCode.None
            ? ReceiveResult.Accepted(header, payload!)
            : ReceiveResult.Rejected(code, header);
    }
}
