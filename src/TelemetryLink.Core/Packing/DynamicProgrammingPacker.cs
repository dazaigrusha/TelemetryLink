using TelemetryLink.Core.Domain;

namespace TelemetryLink.Core.Packing;

/// <summary>
/// Точний розв'язок рюкзака 0/1 динамічним програмуванням.
///
/// КРОК S4. Писати ПІСЛЯ BruteForcePacker: без еталона правильність ДП
/// доводиться очима.
/// </summary>
public sealed class DynamicProgrammingPacker : IFramePacker
{
    public string Name => "dp";

    public IReadOnlyList<int> Select(IReadOnlyList<FrameCandidate> candidates, int capacityBits)
    {
        // TODO(S4). Рекурсія:
        //
        //   best[i][w] = max( best[i-1][w],                        кадр i не беремо
        //                     best[i-1][w - вага_i] + цінність_i )  кадр i беремо
        //
        // Складність O(n·W). Для значень достатньо одновимірного масиву
        // best[w], якщо йти по w У ЗВОРОТНОМУ порядку — інакше один кадр
        // можна буде взяти двічі.
        //
        // Для ВІДНОВЛЕННЯ набору запам'ятовуйте taken[i][w] — чи взято кадр i.
        // Потім ідіть від останнього кадру назад, зменшуючи залишок місткості.
        //
        // Повертати індекси у ЗРОСТАЮЧОМУ порядку.
        // Порожній список кандидатів або місткість ≤ 0 → порожній результат.
        throw new NotImplementedException("DynamicProgrammingPacker.Select");
    }
}
