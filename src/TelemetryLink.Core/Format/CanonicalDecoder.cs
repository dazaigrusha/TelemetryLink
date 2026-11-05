namespace TelemetryLink.Core.Format;

/// <summary>
/// Декодування бітового потоку за канонічною таблицею.
///
/// Винесено в окремий клас, бо потрібне у двох місцях: у кодеку Гаффмана
/// і в розборі навантаження. Дублювати цикл декодування не можна — розбіжність
/// між ними дала б пакети, які читає власний приймач і не читає чужий.
///
/// КРОК S3.
/// </summary>
public static class CanonicalDecoder
{
    /// <summary>
    /// Читає рівно symbolCount символів. Декодер зупиняється, прочитавши
    /// потрібну кількість; решта бітів до кінця байта ігнорується (§3.3).
    /// </summary>
    /// <returns>E_CODE, якщо потік обірвався або код довший за 15 біт.</returns>
    public static RejectionCode TryDecode(
        ReadOnlyMemory<byte> bitstream,
        CodeTable table,
        int symbolCount,
        out byte[] symbols)
    {
        symbols = [];
        if (symbolCount < 0 || (long)symbolCount > (long)bitstream.Length * 8) return RejectionCode.E_CODE;
        var reader = new BitReader(bitstream);
        var result = new byte[symbolCount];
        for (int i = 0; i < symbolCount; i++)
            if (!table.TryDecodeNext(reader, out result[i])) return RejectionCode.E_CODE;
        if (reader.BitLength - reader.BitPosition > 7 || !reader.RemainingBitsAreZero())
            return RejectionCode.E_CODE;
        symbols = result;
        return RejectionCode.None;
    }
}
