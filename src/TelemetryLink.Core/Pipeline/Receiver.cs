using TelemetryLink.Core.Format;
using TelemetryLink.Core.Security;

namespace TelemetryLink.Core.Pipeline;

/// <summary>
/// Приймач: пакет → кадри або код відмови.
///
/// Клас навмисно тонкий. Уся послідовність перевірок живе в PacketReader,
/// бо вона є частиною контракту формату, а не політикою приймача.
/// Приймач додає лише те, що специфікації не належить: облік причин відмови.
///
/// КРОК S2.
/// </summary>
public sealed class Receiver
{
    private readonly SessionKeys _keys;
    private readonly Dictionary<RejectionCode, int> _rejections = new();

    public Receiver(SessionKeys keys) => _keys = keys;

    public int AcceptedCount { get; private set; }

    public IReadOnlyDictionary<RejectionCode, int> Rejections => _rejections;

    public ReceiveResult Accept(ReadOnlySpan<byte> packet)
    {
        // TODO(S2): викликати PacketReader.Read і полічити результат.
        throw new NotImplementedException("Receiver.Accept");
    }
}
