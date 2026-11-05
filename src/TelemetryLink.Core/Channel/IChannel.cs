namespace TelemetryLink.Core.Channel;

/// <summary>Результат спроби передавання одного пакета.</summary>
/// <param name="Delivered">Чи дійшов пакет до приймача.</param>
/// <param name="ArrivalTimeSeconds">Час прибуття; null, якщо не доставлено.</param>
public readonly record struct TransmissionResult(bool Delivered, double? ArrivalTimeSeconds);

/// <summary>
/// Модель каналу зв'язку.
///
/// Інтерфейс існує не заради змінних реалізацій, а заради тестів: у тестах
/// тракту потрібен передбачуваний канал (<c>PerfectChannel</c>), який нічого
/// не втрачає. Сама модель втрат видана готовою і змінювати її не можна.
/// </summary>
public interface IChannel
{
    TransmissionResult Send(ReadOnlySpan<byte> packet);
    ChannelStatistics Statistics { get; }
}

/// <summary>Підсумок роботи каналу (відповідає stats() у channel.py).</summary>
/// <param name="Sent">Пакетів, що фактично зайняли смугу.</param>
/// <param name="Lost">Втрачено ланцюгом Маркова.</param>
/// <param name="OverBudget">Не передано взагалі: бюджет часу вичерпано.</param>
/// <param name="ChannelTimeSeconds">Час зайнятості каналу. У компоненту T метрики Q НЕ входить.</param>
public readonly record struct ChannelStatistics(
    int Sent,
    int Lost,
    int OverBudget,
    double ChannelTimeSeconds,
    double BudgetSeconds)
{
    public double LossRatio => Sent == 0 ? 0.0 : (double)Lost / Sent;

    public double UndeliveredRatio
    {
        get
        {
            int total = Sent + OverBudget;
            return total == 0 ? 0.0 : (double)(Lost + OverBudget) / total;
        }
    }
}
