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
        // TODO(S1): взяти байт _data.Span[_bitPosition / 8], витягти біт
        // зі зсувом (7 - _bitPosition % 8). За межами потоку — повернути false.
        throw new NotImplementedException("BitReader.TryReadBit");
    }

    /// <summary>
    /// Чи всі біти, що лишилися до кінця останнього байта, нульові.
    /// Специфікація вимагає саме нулів у доповненні (§3.3).
    /// </summary>
    public bool RemainingBitsAreZero()
    {
        // TODO(S1)
        throw new NotImplementedException("BitReader.RemainingBitsAreZero");
    }
}
