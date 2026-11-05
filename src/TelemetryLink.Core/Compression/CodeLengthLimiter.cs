namespace TelemetryLink.Core.Compression;

/// <summary>
/// Обмеження довжини коду 15 бітами (FORMAT_SPEC §3.2).
///
/// Довгі коди з'являються, коли частоти дуже перекошені: класичний приклад —
/// частоти, близькі до чисел Фібоначчі, де дерево вироджується в ланцюжок.
///
/// Базовий спосіб: підняти найрідкісніші частоти до спільної нижньої межі
/// й побудувати дерево заново. Код лишається префіксним і побайтово
/// відновлюваним; він лише трохи гірший за оптимальний.
/// Просунутий рівень — package-merge.
///
/// КРОК S3.
/// </summary>
public static class CodeLengthLimiter
{
    /// <summary>
    /// Піднімає всі частоти до межі max(частот) >> (maxLength − 1).
    /// Після цього відношення найбільшої частоти до найменшої не перевищує
    /// 2^(maxLength−1), і дерево не може стати глибшим за maxLength.
    /// </summary>
    public static void RaiseFloor(IReadOnlyList<int> symbols, long[] frequencies, int maxLength)
    {
        // TODO(S3)
        throw new NotImplementedException("CodeLengthLimiter.RaiseFloor");
    }
}
