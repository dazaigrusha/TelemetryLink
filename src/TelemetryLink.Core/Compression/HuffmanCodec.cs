using TelemetryLink.Core.Format;

namespace TelemetryLink.Core.Compression;

/// <summary>Канонічне кодування Гаффмана за FORMAT_SPEC §3.2–3.3. КРОК S3.</summary>
public sealed class HuffmanCodec : ISymbolCodec
{
    public string Name => "huffman";
    public PacketFlags Flags => PacketFlags.None;

    public CodeTable BuildTable(ReadOnlySpan<byte> sample)
    {
        return CodeTable.FromLengths(HuffmanTreeBuilder.BuildLengths(
            HuffmanTreeBuilder.CountFrequencies(sample)));
    }

    public byte[] Encode(ReadOnlySpan<byte> data, CodeTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        var writer = new BitWriter();
        foreach (byte symbol in data)
        {
            if (!table.Contains(symbol))
                throw new ArgumentException($"Символ 0x{symbol:X2} відсутній у таблиці.", nameof(data));
            writer.WriteBits(table.CodeOf(symbol), table.LengthOf(symbol));
        }
        return writer.ToArray();
    }

    public RejectionCode Decode(ReadOnlyMemory<byte> bitstream, CodeTable table, int symbolCount, out byte[] data)
        => CanonicalDecoder.TryDecode(bitstream, table, symbolCount, out data);
}
