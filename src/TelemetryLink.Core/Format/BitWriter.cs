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
        // TODO(S1): додати біт до поточного байта; коли назбиралося 8 —
        // відправити байт у _bytes і почати новий.
        throw new NotImplementedException("BitWriter.WriteBit");
    }

    /// <summary>Записує count молодших бітів значення, СТАРШИМИ ВПЕРЕД.</summary>
    public void WriteBits(uint value, int count)
    {
        // TODO(S1): перебрати біти від (count-1) до 0 і викликати WriteBit.
        // Пастка: порядок бітів. Спочатку найстарший біт коду.
        throw new NotImplementedException("BitWriter.WriteBits");
    }

    /// <summary>Завершує потік, доповнивши останній байт НУЛЯМИ.</summary>
    public byte[] ToArray()
    {
        // TODO(S1): якщо поточний байт неповний, зсунути його вліво
        // на (8 - _filledBits) і дописати.
        throw new NotImplementedException("BitWriter.ToArray");
    }
}
