using TelemetryLink.Core.Domain;

namespace TelemetryLink.Core.Scheduling;

/// <summary>
/// Точка підключення «порядок відправлення пакетів».
///
/// Окремих дедлайнів у пакетів немає. Недоставка настає через втрату в каналі
/// або через вичерпання transmission_budget_s. В аварійному сценарії бюджету
/// не вистачає приблизно на третину пакетів, тому порядок визначає, що взагалі
/// дійде.
/// </summary>
public interface ISendScheduler
{
    /// <summary>Ім'я для CLI і звітів: sequential, greedy.</summary>
    string Name { get; }

    /// <summary>
    /// Повертає ПЕРЕСТАНОВКУ вхідного списку: жоден пакет не зникає і не
    /// дублюється. packet_seq у заголовку при цьому не змінюється — приймач
    /// не залежить від порядку прибуття.
    /// </summary>
    IReadOnlyList<Packet> Order(IReadOnlyList<Packet> packets);
}
