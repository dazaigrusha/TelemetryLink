using TelemetryLink.Core.Configuration;

namespace TelemetryLink.Core.Optimization;

/// <summary>
/// Випадковий пошук: рівномірно кидає точки в простір параметрів і запам'ятовує
/// найкращу.
///
/// Писати його треба ПЕРШИМ, до генетичного алгоритму:
/// 1) він перевіряє, що функція пристосованості взагалі працює і що простір
///    параметрів заданий правильно — без селекції й мутацій, які можуть
///    сховати помилку;
/// 2) без нього немає з чим порівнювати ГА, а порівняння з базовою лінією —
///    обов'язкова частина завдання.
///
/// Цілком нормальний результат — коли ГА не перевершує випадковий пошук.
/// Провал настає тоді, коли студент не може пояснити, чому так сталося.
///
/// КРОК S8.
/// </summary>
public sealed class RandomSearchOptimizer : IParameterOptimizer
{
    public string Name => "random";

    public OptimizationResult Optimize(Func<Tuning, double> fitness, int evaluationBudget, int seed)
    {
        // TODO(S8): рівно evaluationBudget викликів fitness, не більше і не менше.
        // Історію заповнюйте найкращим значенням на кожному кроці — це дасть
        // криву для порівняння з ГА.
        throw new NotImplementedException("RandomSearchOptimizer.Optimize");
    }

    /// <summary>Рівномірна випадкова точка в межах Tuning.Bounds.</summary>
    public static Tuning RandomTuning(Random random)
    {
        // TODO(S8)
        throw new NotImplementedException("RandomSearchOptimizer.RandomTuning");
    }
}
