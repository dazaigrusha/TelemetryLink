using TelemetryLink.Core.Format;
using TelemetryLink.Core.Security;

namespace TelemetryLink.Core.Pipeline;

/// <summary>Точка підміни декодера для перевірки обробки збоїв приймача.</summary>
public delegate ReceiveResult PacketDecoder(ReadOnlySpan<byte> packet, SessionKeys keys);

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
    private readonly PacketDecoder _decoder;
    private readonly Dictionary<RejectionCode, int> _rejections = new();

    public Receiver(SessionKeys keys, PacketDecoder? decoder = null)
    {
        _keys = keys;
        _decoder = decoder ?? PacketReader.Read;
    }

    public int AcceptedCount { get; private set; }

    public IReadOnlyDictionary<RejectionCode, int> Rejections => _rejections;

    public ReceiveResult Accept(ReadOnlySpan<byte> packet)
    {
        var result = _decoder(packet, _keys);
        if (result.IsAccepted) AcceptedCount++;
        else _rejections[result.Code] = _rejections.GetValueOrDefault(result.Code) + 1;
        return result;
    }
}
