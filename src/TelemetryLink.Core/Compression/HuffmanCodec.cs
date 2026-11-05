using TelemetryLink.Core.Format;

namespace TelemetryLink.Core.Compression;

/// <summary>Канонічне кодування Гаффмана за FORMAT_SPEC §3.2–3.3. КРОК S3.</summary>
public sealed class HuffmanCodec : ISymbolCodec
{
    public string Name => "huffman";
    public PacketFlags Flags => PacketFlags.None;

    public CodeTable BuildTable(ReadOnlySpan<byte> sample)
    {
        // TODO(S3): частоти → довжини → CodeTable.FromLengths.
        throw new NotImplementedException("HuffmanCodec.BuildTable");
    }

    public byte[] Encode(ReadOnlySpan<byte> data, CodeTable table)
    {
        // TODO(S3): для кожного байта записати table.CodeOf(байт)
        // довжиною table.LengthOf(байт), СТАРШИМИ БІТАМИ ВПЕРЕД.
        //
        // Символ, відсутній у таблиці, — це помилка ПРОГРАМІСТА (таблицю
        // побудовано не за тими даними), тому тут доречний виняток,
        // а не код відмови.
        throw new NotImplementedException("HuffmanCodec.Encode");
    }

    public RejectionCode Decode(ReadOnlyMemory<byte> bitstream, CodeTable table, int symbolCount, out byte[] data)
        => CanonicalDecoder.TryDecode(bitstream, table, symbolCount, out data);
}
