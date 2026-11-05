namespace TelemetryLink.Core.Format;

/// <summary>
/// Призначення канонічних кодів за довжинами (FORMAT_SPEC §3.3).
///
/// Алгоритм переписується зі специфікації ДОСЛІВНО. Будь-яка «своя» версія дає
/// ті самі довжини, але інші коди — і пакет перестає відповідати формату.
/// Саме канонічність дозволяє не передавати структуру дерева: у потік пишуться
/// лише довжини, а коди обидві сторони відновлюють однаково.
///
/// КРОК S3. Перевірка: довжини 1,3,4,4,4,4,3 з контрольного прикладу §7 мають
/// дати коди 0, 100, 1100, 1101, 1110, 1111, 101.
/// </summary>
public static class CanonicalCodes
{
    /// <summary>Максимальна довжина коду за специфікацією.</summary>
    public const int MaxCodeLength = 15;

    /// <summary>
    /// Повертає масив на 256 позицій: код кожного символу.
    /// Для символів із довжиною 0 значення не визначене.
    /// </summary>
    /// <param name="lengths">Довжини кодів, індекс = символ, 0 = символ відсутній.</param>
    public static uint[] Assign(ReadOnlySpan<byte> lengths)
    {
        if (lengths.Length != 256) throw new ArgumentException("Очікується 256 довжин.", nameof(lengths));
        var counts = new uint[MaxCodeLength + 1];
        foreach (byte length in lengths)
        {
            if (length > MaxCodeLength) throw new ArgumentException("Довжина коду перевищує 15.", nameof(lengths));
            if (length > 0) counts[length]++;
        }
        var next = new uint[MaxCodeLength + 1];
        uint code = 0;
        for (int length = 1; length <= MaxCodeLength; length++)
        {
            code = (code + counts[length - 1]) << 1;
            if (code + counts[length] > (1u << length))
                throw new ArgumentException("Таблиця містить забагато кодів заданої довжини.", nameof(lengths));
            next[length] = code;
        }
        var result = new uint[256];
        for (int symbol = 0; symbol < 256; symbol++)
            if (lengths[symbol] > 0) result[symbol] = next[lengths[symbol]]++;
        return result;
    }
}
