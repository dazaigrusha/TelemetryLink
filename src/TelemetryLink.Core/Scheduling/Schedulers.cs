using TelemetryLink.Core.Domain;

namespace TelemetryLink.Core.Scheduling;

/// <summary>
/// Відправляє пакети в порядку формування. Базова лінія: саме з нею
/// порівнюється жадібний планувальник. КРОК S5.
/// </summary>
public sealed class SequentialScheduler : ISendScheduler
{
    public string Name => "sequential";

    public IReadOnlyList<Packet> Order(IReadOnlyList<Packet> packets)
    {
        ArgumentNullException.ThrowIfNull(packets);
        return packets;
    }
}

/// <summary>
/// Жадібний планувальник: спершу пакети з найбільшою цінністю на байт.
///
/// Це евристика, а не доказово оптимальний алгоритм — її якість оцінюється
/// експериментально. Сенс: бюджет часу вичерпається раніше за чергу, тому
/// за однакової ціни каналу вигідніше віддати те, що несе більше цінності.
///
/// КРОК S5.
/// </summary>
public sealed class ValueDensityScheduler : ISendScheduler
{
    public string Name => "greedy";

    public IReadOnlyList<Packet> Order(IReadOnlyList<Packet> packets)
    {
        ArgumentNullException.ThrowIfNull(packets);
        if (packets.Any(p => p is null || p.Bytes is null))
            throw new ArgumentException("Пакет та його байти не можуть бути null.", nameof(packets));
        var entries = packets.Select((packet, index) => (Packet: packet, Index: index)).ToArray();
        Array.Sort(entries, (left, right) =>
        {
            // Перехресне множення точно порівнює щільність без double.
            // Для нульової довжини зберігаємо правило Packet.ValueDensity = 0.
            long leftValue = left.Packet.SizeInBytes == 0 ? 0 : left.Packet.Value;
            long rightValue = right.Packet.SizeInBytes == 0 ? 0 : right.Packet.Value;
            int leftSize = Math.Max(left.Packet.SizeInBytes, 1);
            int rightSize = Math.Max(right.Packet.SizeInBytes, 1);
            int density = (rightValue * leftSize).CompareTo(leftValue * rightSize);
            if (density != 0) return density;
            int sequence = left.Packet.Sequence.CompareTo(right.Packet.Sequence);
            return sequence != 0 ? sequence : left.Index.CompareTo(right.Index);
        });
        // Повертаємо ті самі пакети: байти, sequence, nonce та HMAC не змінюються.
        return entries.Select(entry => entry.Packet).ToArray();
    }
}
