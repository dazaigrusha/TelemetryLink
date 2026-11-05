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
        // TODO(S1). Розкладка (FORMAT_SPEC §3), усе big-endian:
        //
        //   0   uint16  frame_count
        //   2   uint8   flags
        //   3   uint8   reserved = 0
        //   4   uint16  table_nsym
        //   6   2×nsym  таблиця: пари (symbol, length), СОРТОВАНО ЗА СИМВОЛОМ
        //   …   uint32  symbol_count   (завжди 12 × frame_count)
        //   …   uint32  bitstream_len
        //   …   bitstream
        //   …   class_table            (лише якщо flags.bit2)
        throw new NotImplementedException("PayloadWriter.Write");
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
        // TODO(S1 + S3). Обов'язкові перевірки:
        //
        //   reserved != 0                         → E_PAYLOAD
        //   встановлені біти 4..7 прапорців       → E_PAYLOAD
        //   flags & 0x03 != 0 (чуже розширення)   → E_PAYLOAD  (§3.1.1)
        //   NO_COMPRESSION і table_nsym != 0      → E_PAYLOAD
        //   символи таблиці не зростають          → E_PAYLOAD
        //   довжина коду 0 або > 15               → E_CODE
        //   symbol_count != 12 × frame_count      → E_PAYLOAD  (інваріант §3)
        //   секції не вкладаються в довжину       → E_PAYLOAD
        //   зайві байти в кінці                   → E_PAYLOAD
        //
        // Далі: у режимі NO_COMPRESSION bitstream — це сирі байти кадрів
        // (і bitstream_len має дорівнювати symbol_count), інакше —
        // CanonicalDecoder.TryDecode.
        throw new NotImplementedException("PayloadReader.TryRead");
    }
}
