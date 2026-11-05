namespace TelemetryLink.Core.Format;

/// <summary>
/// Запис бітів старшим бітом уперед (FORMAT_SPEC §0.3):
/// перший записаний біт стає бітом 0x80 першого байта.
/// Останній байт доповнюється нулями (§0.4).
///
/// КРОК S1. Перевірка: контрольний приклад FORMAT_SPEC §7 має дати 0C E6 DF 90.
/// </summary>
public sealed class BitWriter
{
    private readonly List<byte> _bytes = new();
    private byte _current;
    private int _filledBits;

    /// <summary>Скільки бітів уже записано.</summary>
    public int BitCount { get; private set; }

    public void WriteBit(int bit)
    {
        if (bit is not (0 or 1)) throw new ArgumentOutOfRangeException(nameof(bit));
        _current = (byte)((_current << 1) | bit);
        _filledBits++;
        BitCount++;
        if (_filledBits == 8)
        {
            _bytes.Add(_current);
            _current = 0;
            _filledBits = 0;
        }
    }

    /// <summary>Записує count молодших бітів значення, СТАРШИМИ ВПЕРЕД.</summary>
    public void WriteBits(uint value, int count)
    {
        if (count < 0 || count > 32) throw new ArgumentOutOfRangeException(nameof(count));
        for (int i = count - 1; i >= 0; i--) WriteBit((int)((value >> i) & 1));
    }

    /// <summary>Завершує потік, доповнивши останній байт НУЛЯМИ.</summary>
    public byte[] ToArray()
    {
        // Знімок не змінює стан: повторний виклик і подальший запис безпечні.
        var result = new byte[_bytes.Count + (_filledBits > 0 ? 1 : 0)];
        _bytes.CopyTo(result);
        if (_filledBits > 0) result[^1] = (byte)(_current << (8 - _filledBits));
        return result;
    }
}
