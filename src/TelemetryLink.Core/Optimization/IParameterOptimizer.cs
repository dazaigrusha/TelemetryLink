using TelemetryLink.Core.Configuration;

namespace TelemetryLink.Core.Optimization;

/// <summary>Запис про одне покоління — рядок журналу еволюції.</summary>
public readonly record struct GenerationRecord(int Generation, double BestFitness, double MeanFitness);

/// <summary>Результат оптимізації.</summary>
public sealed record OptimizationResult(
    Tuning Best,
    double BestFitness,
    IReadOnlyList<GenerationRecord> History,
    int Evaluations);

/// <summary>
/// Точка підключення «підбір параметрів».
///
/// evaluationBudget — кількість викликів функції пристосованості. Це не
/// деталь реалізації, а умова чесного порівняння: генетичний алгоритм і
/// випадковий пошук мають витратити РІВНО стільки ж обчислень. Інакше
/// висновок «ГА кращий» нічого не означає.
/// </summary>
public interface IParameterOptimizer
{
    /// <summary>Ім'я для CLI і звітів: ga, random.</summary>
    string Name { get; }

    OptimizationResult Optimize(Func<Tuning, double> fitness, int evaluationBudget, int seed);
}
