using TelemetryLink.Core.Domain;

namespace TelemetryLink.Core.Packing;

/// <summary>
/// Точка підключення «вибір кадрів у пакет» — задача про рюкзак 0/1.
///
/// Реалізація бачить лише трійки (індекс, вага в бітах, цінність) і місткість.
/// Ні про Гаффмана, ні про телеметрію вона не знає, тому тестується на
/// маленьких наборах чисел.
/// </summary>
public interface IFramePacker
{
    /// <summary>Ім'я для CLI і звітів: dp, greedy, brute.</summary>
    string Name { get; }

    /// <summary>
    /// Максимізує сумарну цінність за обмеження суми ваг.
    /// </summary>
    /// <returns>Індекси у списку candidates, у зростаючому порядку.</returns>
    IReadOnlyList<int> Select(IReadOnlyList<FrameCandidate> candidates, int capacityBits);
}
