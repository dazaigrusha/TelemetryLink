using System.Buffers.Binary;
using TelemetryLink.Core.Domain;

namespace TelemetryLink.Core.Format;

/// <summary>Корисне навантаження, підготовлене до шифрування (FORMAT_SPEC §3).</summary>
public sealed record PayloadContent(
    int FrameCount,
    PacketFlags Flags,
    CodeTable Table,
    int SymbolCount,
    byte[] Bitstream,
    byte[]? ClassTable = null)
{
    /// <summary>Фіксовані поля навантаження: frame_count, flags, reserved,
    /// table_nsym, symbol_count, bitstream_len. Саме це «14» у формулі місткості W.</summary>
    public const int FixedFieldsSize = 14;

    public int SizeInBytes =>
        FixedFieldsSize + Table.SizeInBytes + Bitstream.Length + (ClassTable?.Length ?? 0);
}

/// <summary>Розібране навантаження. Використовується і приймачем, і командою inspect.</summary>
public sealed record PayloadInfo(
    int FrameCount,
    PacketFlags Flags,
    CodeTable Table,
    int SymbolCount,
    byte[] Bitstream,
    byte[]? ClassTable,
    IReadOnlyList<TelemetryFrame> Frames);

/// <summary>КРОК S1. Порядок секцій жорсткий, вирівнювань між ними немає.</summary>
public static class PayloadWriter
{
    public static byte[] Write(PayloadContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.FrameCount < 0 || content.FrameCount > ushort.MaxValue
            || content.SymbolCount != content.FrameCount * TelemetryFrame.SizeInBytes)
            throw new ArgumentException("Неузгоджена кількість кадрів і символів.", nameof(content));
        var bytes = new byte[content.SizeInBytes];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, (ushort)content.FrameCount);
        bytes[2] = (byte)content.Flags;
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(4), (ushort)content.Table.SymbolCount);
        int offset = 6;
        foreach (var entry in content.Table.Entries)
        {
            bytes[offset++] = entry.Symbol;
            bytes[offset++] = entry.Length;
        }
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(offset), (uint)content.SymbolCount);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(offset + 4), (uint)content.Bitstream.Length);
        offset += 8;
        content.Bitstream.CopyTo(bytes, offset);
        offset += content.Bitstream.Length;
        content.ClassTable?.CopyTo(bytes, offset);
        // Письменник не створює навантажень, які відхиляє власний читач.
        if (PayloadReader.TryRead(bytes, out _) != RejectionCode.None)
            throw new ArgumentException("Некоректне навантаження.", nameof(content));
        return bytes;
    }
}

/// <summary>КРОК S1. Суворий розбір: жодного винятку на поганих даних.</summary>
public static class PayloadReader
{
    /// <summary>
    /// Розбирає навантаження і відновлює кадри.
    /// Будь-яка невідповідність — це код відмови, а не виняток.
    /// </summary>
    public static RejectionCode TryRead(ReadOnlySpan<byte> payload, out PayloadInfo? info)
    {
        info = null;
        if (payload.Length < PayloadContent.FixedFieldsSize) return RejectionCode.E_PAYLOAD;
        int frameCount = BinaryPrimitives.ReadUInt16BigEndian(payload);
        var flags = (PacketFlags)payload[2];
        if (payload[3] != 0 || ((byte)flags & 0xF3) != 0) return RejectionCode.E_PAYLOAD;
        bool raw = (flags & PacketFlags.NoCompression) != 0;
        int count = BinaryPrimitives.ReadUInt16BigEndian(payload[4..]);
        if (count > 256 || (raw && count != 0)) return RejectionCode.E_PAYLOAD;
        int offset = 6;
        if (payload.Length < PayloadContent.FixedFieldsSize + 2 * count) return RejectionCode.E_PAYLOAD;
        var lengths = new byte[256];
        int previous = -1;
        for (int i = 0; i < count; i++)
        {
            byte symbol = payload[offset++];
            byte length = payload[offset++];
            if (symbol <= previous) return RejectionCode.E_PAYLOAD;
            if (length is 0 or > 15) return RejectionCode.E_CODE;
            lengths[symbol] = length;
            previous = symbol;
        }
        uint symbolCount = BinaryPrimitives.ReadUInt32BigEndian(payload[offset..]);
        uint streamLength = BinaryPrimitives.ReadUInt32BigEndian(payload[(offset + 4)..]);
        offset += 8;
        if (symbolCount != frameCount * TelemetryFrame.SizeInBytes) return RejectionCode.E_PAYLOAD;
        int classLength = (flags & PacketFlags.ClassTable) != 0 ? frameCount : 0;
        if ((long)offset + streamLength + classLength != payload.Length) return RejectionCode.E_PAYLOAD;
        CodeTable table;
        try { table = CodeTable.FromLengths(lengths); }
        catch (ArgumentException) { return RejectionCode.E_CODE; }
        byte[] stream = payload.Slice(offset, (int)streamLength).ToArray();
        offset += (int)streamLength;
        byte[]? classes = classLength > 0 || (flags & PacketFlags.ClassTable) != 0
            ? payload[offset..].ToArray() : null;
        if (classes is not null && classes.Any(value => value > 2)) return RejectionCode.E_PAYLOAD;
        byte[] decoded;
        if (raw)
        {
            if (streamLength != symbolCount) return RejectionCode.E_PAYLOAD;
            decoded = stream;
        }
        else
        {
            var code = CanonicalDecoder.TryDecode(stream, table, (int)symbolCount, out decoded);
            if (code != RejectionCode.None) return code;
        }
        var frames = TelemetryFrame.Deserialize(decoded);
        if (frames.Any(frame => !frame.IsValid)) return RejectionCode.E_PAYLOAD;
        info = new PayloadInfo(frameCount, flags, table, (int)symbolCount, stream, classes, frames);
        return RejectionCode.None;
    }
}
