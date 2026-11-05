using TelemetryLink.Core.Domain;

namespace TelemetryLink.Core.Scheduling;

/// <summary>
/// Відправляє пакети в порядку формування. Базова лінія: саме з нею
/// порівнюється жадібний планувальник. КРОК S5.
/// </summary>
public sealed class SequentialScheduler : ISendScheduler
{
    public string Name => "sequential";

    public IReadOnlyList<Packet> Order(IReadOnlyList<Packet> packets) => packets;
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
        // TODO(S5): відсортувати за Packet.ValueDensity спадно,
        // нічиї — за Sequence, щоб результат був відтворюваним.
        //
        // Результат має бути ПЕРЕСТАНОВКОЮ входу: жоден пакет не зникає
        // і не дублюється. packet_seq у заголовку при цьому НЕ змінюється.
        throw new NotImplementedException("ValueDensityScheduler.Order");
    }
}
