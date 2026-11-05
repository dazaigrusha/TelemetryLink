namespace TelemetryLink.Core.Format;

/// <summary>
/// Читання бітів старшим бітом уперед — дзеркало <see cref="BitWriter"/>.
///
/// Метод називається TryReadBit, а не ReadBit: кінець потоку тут не виняткова
/// ситуація, а штатний результат розбору зіпсованого пакета (код E_CODE).
/// Не кидайте виняток — поверніть false.
///
/// КРОК S1.
/// </summary>
public sealed class BitReader
{
    private readonly ReadOnlyMemory<byte> _data;
    private int _bitPosition;

    public BitReader(ReadOnlyMemory<byte> data) => _data = data;

    public int BitPosition => _bitPosition;
    public int BitLength => _data.Length * 8;

    public bool TryReadBit(out int bit)
    {
        bit = 0;
        if (_bitPosition >= BitLength) return false;
        bit = (_data.Span[_bitPosition / 8] >> (7 - _bitPosition % 8)) & 1;
        _bitPosition++;
        return true;
    }

    /// <summary>
    /// Чи всі біти, що лишилися до кінця останнього байта, нульові.
    /// Специфікація вимагає саме нулів у доповненні (§3.3).
    /// </summary>
    public bool RemainingBitsAreZero()
    {
        for (int position = _bitPosition; position < BitLength; position++)
            if (((_data.Span[position / 8] >> (7 - position % 8)) & 1) != 0) return false;
        return true;
    }
}
