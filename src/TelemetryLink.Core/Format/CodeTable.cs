namespace TelemetryLink.Core.Format;

/// <summary>Один запис таблиці: символ і довжина його коду.</summary>
public readonly record struct CodeTableEntry(byte Symbol, byte Length);

/// <summary>
/// Таблиця кодів Гаффмана у вигляді, в якому вона передається в пакеті
/// (FORMAT_SPEC §3.2): зберігаються ЛИШЕ довжини, коди обчислюються канонічно.
///
/// Клас має бути НЕЗМІННИМ. Це важливо: таблиця будується один раз для вікна
/// кандидатів і далі не змінюється — інакше ваги кадрів «попливуть» під час
/// вибору і задача про рюкзак стане невизначеною.
///
/// КРОК S1 (структура) і S3 (декодування).
/// </summary>
public sealed class CodeTable
{
    private readonly byte[] _lengths;   // 256 позицій, 0 = символ відсутній
    private readonly uint[] _codes;     // 256 позицій, канонічні коди

    public static readonly CodeTable Empty = FromLengths(new byte[256]);

    private CodeTable(byte[] lengths)
    {
        _lengths = lengths;
        _codes = CanonicalCodes.Assign(lengths);

        var entries = new List<CodeTableEntry>();
        for (int symbol = 0; symbol < 256; symbol++)
            if (lengths[symbol] > 0)
                entries.Add(new CodeTableEntry((byte)symbol, lengths[symbol]));
        Entries = entries.AsReadOnly();

        // TODO(S3): підготувати допоміжні структури для TryDecodeNext.
    }

    /// <summary>Записи, відсортовані за зростанням символу — саме так вони пишуться в пакет.</summary>
    public IReadOnlyList<CodeTableEntry> Entries { get; }

    /// <summary>table_nsym: кількість символів із ненульовою довжиною.</summary>
    public int SymbolCount => Entries.Count;

    /// <summary>Розмір секції таблиці в пакеті: по 2 байти на запис.</summary>
    public int SizeInBytes => 2 * SymbolCount;

    public bool Contains(byte symbol) => _lengths[symbol] > 0;
    public byte LengthOf(byte symbol) => _lengths[symbol];
    public uint CodeOf(byte symbol) => _codes[symbol];

    public static CodeTable FromLengths(ReadOnlySpan<byte> lengths)
    {
        if (lengths.Length != 256)
            throw new ArgumentException("Очікується рівно 256 довжин.", nameof(lengths));
        return new CodeTable(lengths.ToArray());
    }

    public static CodeTable FromEntries(IEnumerable<CodeTableEntry> entries)
    {
        var lengths = new byte[256];
        var seen = new bool[256];
        foreach (var entry in entries)
        {
            if (seen[entry.Symbol] || entry.Length is 0 or > 15)
                throw new ArgumentException("Повтор символу або некоректна довжина.", nameof(entries));
            seen[entry.Symbol] = true;
            lengths[entry.Symbol] = entry.Length;
        }
        return FromLengths(lengths);
    }

    /// <summary>
    /// Сумарна довжина в бітах послідовності байтів за цією таблицею.
    /// Повертає -1, якщо трапився символ, відсутній у таблиці.
    ///
    /// Саме так рахується ВАГА КАДРУ w_i для задачі про рюкзак (S4).
    /// </summary>
    public int MeasureBits(ReadOnlySpan<byte> data)
    {
        int total = 0;
        foreach (byte symbol in data)
        {
            if (_lengths[symbol] == 0) return -1;
            total = checked(total + _lengths[symbol]);
        }
        return total;
    }

    /// <summary>
    /// Декодує один символ із бітового потоку за канонічними кодами.
    /// Повертає false, якщо потік обірвався або код довший за 15 біт.
    /// </summary>
    public bool TryDecodeNext(BitReader reader, out byte symbol)
    {
        uint code = 0;
        for (int length = 1; length <= CanonicalCodes.MaxCodeLength; length++)
        {
            if (!reader.TryReadBit(out int bit)) break;
            code = (code << 1) | (uint)bit;
            foreach (var entry in Entries)
                if (entry.Length == length && _codes[entry.Symbol] == code)
                {
                    symbol = entry.Symbol;
                    return true;
                }
        }
        symbol = 0;
        return false;
    }
}
