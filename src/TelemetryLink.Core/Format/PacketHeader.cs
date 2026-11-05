using System.Buffers.Binary;
using System.Text;

namespace TelemetryLink.Core.Format;

/// <summary>
/// Відкритий заголовок пакета — 43 байти (FORMAT_SPEC §2.1).
/// Не шифрується: приймач має розібрати його до отримання ключа.
/// </summary>
public readonly record struct PacketHeader(
    ushort DeviceId,
    uint PacketSequence,
    byte[] Salt,
    byte[] Nonce,
    uint CiphertextLength)
{
    public const int SizeInBytes = 43;
    public const byte Version = 1;
    public static ReadOnlySpan<byte> Magic => "TLM1"u8;

    public void WriteTo(Span<byte> destination)
    {
        Magic.CopyTo(destination[..4]);
        destination[4] = Version;
        BinaryPrimitives.WriteUInt16BigEndian(destination.Slice(5, 2), DeviceId);
        BinaryPrimitives.WriteUInt32BigEndian(destination.Slice(7, 4), PacketSequence);
        Salt.CopyTo(destination.Slice(11, 16));
        Nonce.CopyTo(destination.Slice(27, 12));
        BinaryPrimitives.WriteUInt32BigEndian(destination.Slice(39, 4), CiphertextLength);
    }

    /// <summary>Розбирає заголовок. Перевірка magic і version — на боці викликача.</summary>
    public static PacketHeader ReadFrom(ReadOnlySpan<byte> source) => new(
        BinaryPrimitives.ReadUInt16BigEndian(source.Slice(5, 2)),
        BinaryPrimitives.ReadUInt32BigEndian(source.Slice(7, 4)),
        source.Slice(11, 16).ToArray(),
        source.Slice(27, 12).ToArray(),
        BinaryPrimitives.ReadUInt32BigEndian(source.Slice(39, 4)));

    public static bool HasValidMagic(ReadOnlySpan<byte> packet)
        => packet.Length >= 5 && packet[..4].SequenceEqual(Magic) && packet[4] == Version;

    public string SaltHex => Convert.ToHexStringLower(Salt);
    public string NonceHex => Convert.ToHexStringLower(Nonce);

    public override string ToString() =>
        $"{Encoding.ASCII.GetString(Magic)} v{Version} device={DeviceId} seq={PacketSequence} ct_len={CiphertextLength}";
}
