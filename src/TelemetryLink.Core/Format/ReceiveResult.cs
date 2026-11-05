using TelemetryLink.Core.Domain;

namespace TelemetryLink.Core.Format;

/// <summary>
/// Результат приймання пакета.
///
/// Відмова — не виняток, а штатний результат: канал псує і втрачає пакети,
/// це нормальний режим роботи системи. Винятки лишаються для помилок
/// програміста, а не для поганих вхідних даних.
/// </summary>
public readonly record struct ReceiveResult(
    RejectionCode Code,
    IReadOnlyList<TelemetryFrame> Frames,
    PacketHeader Header,
    PayloadInfo? Payload)
{
    public bool IsAccepted => Code == RejectionCode.None;

    public static ReceiveResult Rejected(RejectionCode code, PacketHeader header = default)
        => new(code, [], header, null);

    public static ReceiveResult Accepted(PacketHeader header, PayloadInfo payload)
        => new(RejectionCode.None, payload.Frames, header, payload);
}
