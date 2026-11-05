using TelemetryLink.Core.Format;

namespace TelemetryLink.Core.Compression;

/// <summary>
/// Свідома відмова від стиснення (FORMAT_SPEC §3.1, критерій C2 пункт 5).
///
/// Потрібен у двох ролях:
/// 1) базова лінія на початку роботи — тракт працює ще до Гаффмана (КРОК S1);
/// 2) штатний режим, коли стиснення не окупає таблицю.
///
/// Правило формату: table_nsym = 0, таблиці немає, bitstream містить рівно
/// symbol_count сирих байтів, bitstream_len = symbol_count.
/// </summary>
public sealed class NoCompressionCodec : ISymbolCodec
{
    public string Name => "none";
    public PacketFlags Flags => PacketFlags.NoCompression;

    public CodeTable BuildTable(ReadOnlySpan<byte> sample) => CodeTable.Empty;

    public byte[] Encode(ReadOnlySpan<byte> data, CodeTable table) => data.ToArray();

    public RejectionCode Decode(ReadOnlyMemory<byte> bitstream, CodeTable table, int symbolCount, out byte[] data)
    {
        data = [];
        if (bitstream.Length != symbolCount) return RejectionCode.E_PAYLOAD;
        data = bitstream.ToArray();
        return RejectionCode.None;
    }
}
