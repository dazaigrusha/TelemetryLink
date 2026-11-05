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
        // TODO(S3): symbolCount разів викликати table.TryDecodeNext.
        // Жодного винятку: невдача — це RejectionCode.E_CODE.
        throw new NotImplementedException("CanonicalDecoder.TryDecode");
    }
}
