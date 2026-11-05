using TelemetryLink.Core.Domain;

namespace TelemetryLink.Core.Packing;

/// <summary>
/// Повний перебір усіх 2^n підмножин.
///
/// У тракті НЕ використовується — він існує заради тестів. Це найпростіший
/// спосіб довести, що ДП реалізовано правильно: на маленьких наборах точний
/// перебір і ДП мають давати ОДНАКОВУ цінність.
///
/// КРОК S4, пишеться ПЕРШИМ із трьох пакувальників.
/// </summary>
public sealed class BruteForcePacker : IFramePacker
{
    /// <summary>Більше — уже мільйони підмножин; для тестів достатньо 20.</summary>
    public const int MaxItems = 20;

    public string Name => "brute";

    public IReadOnlyList<int> Select(IReadOnlyList<FrameCandidate> candidates, int capacityBits)
    {
        // TODO(S4): перебрати маски від 1 до 2^n − 1, відкинути ті,
        // що не вкладаються в місткість, запам'ятати найціннішу.
        // Понад MaxItems кадрів — ArgumentException.
        throw new NotImplementedException("BruteForcePacker.Select");
    }
}
