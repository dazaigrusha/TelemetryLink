namespace TelemetryLink.Core.Compression;

/// <summary>
/// Обмеження довжини коду згладжуванням частот. Алфавіт зберігається повністю.
/// Після кожного підняття межі дерево перебудовується і перевіряється знову.
/// Це детермінований базовий метод, а не оптимальний package-merge.
/// </summary>
public static class CodeLengthLimiter
{
    public static void RaiseFloor(IReadOnlyList<int> symbols, long[] frequencies, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(symbols);
        ArgumentNullException.ThrowIfNull(frequencies);
        if (maxLength < 1 || maxLength > 15) throw new ArgumentOutOfRangeException(nameof(maxLength));
        if (frequencies.Length != 256) throw new ArgumentException("Очікується 256 частот.", nameof(frequencies));
        if (symbols.Count > (1 << maxLength))
            throw new ArgumentException("Алфавіт не вміщується у задану довжину.", nameof(symbols));
        if (symbols.Count == 0) return;
        long maximum = 0, minimum = long.MaxValue;
        var seen = new bool[256];
        foreach (int symbol in symbols)
        {
            if (symbol < 0 || symbol > 255 || seen[symbol] || frequencies[symbol] <= 0)
                throw new ArgumentException("Очікуються різні символи 0..255 з додатними частотами.", nameof(symbols));
            seen[symbol] = true;
            maximum = Math.Max(maximum, frequencies[symbol]);
            minimum = Math.Min(minimum, frequencies[symbol]);
        }
        // Саме повторення max >> (L-1) може не змінити вже згладжену таблицю.
        // Подвоєння мінімуму гарантує прогрес; насичення захищає від переповнення.
        long doubled = minimum > maximum / 2 ? maximum : minimum * 2;
        long floor = Math.Max(1, Math.Max(maximum >> (maxLength - 1), doubled));
        foreach (int symbol in symbols) frequencies[symbol] = Math.Max(frequencies[symbol], floor);
    }
}
