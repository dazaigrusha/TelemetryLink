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
        Entries = entries;

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
        foreach (var entry in entries) lengths[entry.Symbol] = entry.Length;
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
        // TODO(S3)
        throw new NotImplementedException("CodeTable.MeasureBits");
    }

    /// <summary>
    /// Декодує один символ із бітового потоку за канонічними кодами.
    /// Повертає false, якщо потік обірвався або код довший за 15 біт.
    /// </summary>
    public bool TryDecodeNext(BitReader reader, out byte symbol)
    {
        // TODO(S3). Канонічні коди дозволяють декодувати без дерева:
        //
        //   code = 0
        //   для L = 1..15:
        //       code = (code << 1) | наступний_біт
        //       якщо існують символи довжини L і
        //          code - перший_код_довжини_L < кількість_символів_довжини_L:
        //             повернути символ за зміщенням
        //
        // Перший код довжини L і зміщення обчислюються так само, як у
        // CanonicalCodes.Assign. Символи всередині довжини йдуть за зростанням.
        throw new NotImplementedException("CodeTable.TryDecodeNext");
    }
}
