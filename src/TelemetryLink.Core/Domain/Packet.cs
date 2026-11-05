namespace TelemetryLink.Core.Domain;

/// <summary>
/// Готовий до відправлення пакет.
/// </summary>
/// <param name="Sequence">packet_seq із заголовка. Присвоюється при формуванні
/// і не змінюється, навіть якщо планувальник переставить пакети.</param>
/// <param name="Bytes">Повні байти пакета: заголовок + шифротекст + HMAC.</param>
/// <param name="FrameIndexes">Індекси кадрів сеансу, що потрапили в пакет.</param>
/// <param name="Value">Сумарна цінність кадрів — вхід для планувальника.</param>
public sealed record Packet(
    uint Sequence,
    byte[] Bytes,
    IReadOnlyList<int> FrameIndexes,
    int Value)
{
    public int SizeInBytes => Bytes.Length;

    /// <summary>Цінність на байт — критерій жадібного планувальника.</summary>
    public double ValueDensity => Bytes.Length == 0 ? 0.0 : (double)Value / Bytes.Length;
}
