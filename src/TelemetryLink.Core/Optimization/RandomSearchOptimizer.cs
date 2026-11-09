using TelemetryLink.Core.Configuration;
namespace TelemetryLink.Core.Optimization;

/// <summary>Рівномірний пошук з рівно заданою кількістю викликів fitness.</summary>
public sealed class RandomSearchOptimizer : IParameterOptimizer
{
    public string Name => "random";
    internal static double Evaluate(Func<Tuning, double> fitness, Tuning tuning)
    {
        double score = fitness(tuning);
        if (!double.IsFinite(score)) throw new InvalidOperationException("Fitness має бути скінченним.");
        return score;
    }
    public OptimizationResult Optimize(Func<Tuning, double> fitness, int evaluationBudget, int seed)
    {
        ArgumentNullException.ThrowIfNull(fitness);
        if (evaluationBudget < 1) throw new ArgumentOutOfRangeException(nameof(evaluationBudget));
        var random = new Random(seed); Tuning? best = null; double bestScore = double.NegativeInfinity, sum = 0;
        var history = new List<GenerationRecord>();
        for (int i = 0; i < evaluationBudget; i++)
        {
            var candidate = RandomTuning(random); double score = Evaluate(fitness, candidate); sum += score;
            if (best is null || score > bestScore) { best = candidate; bestScore = score; }
            history.Add(new GenerationRecord(i, bestScore, sum / (i + 1)) { Evaluations = i + 1 });
        }
        return new(best!, bestScore, history.AsReadOnly(), evaluationBudget);
    }
    public static Tuning RandomTuning(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        return new(random.Next((int)Tuning.Bounds[0].Low, (int)Tuning.Bounds[0].High + 1),
            Tuning.Bounds[1].Low + random.NextDouble() * (Tuning.Bounds[1].High - Tuning.Bounds[1].Low),
            Tuning.Bounds[2].Low + random.NextDouble() * (Tuning.Bounds[2].High - Tuning.Bounds[2].Low));
    }
}
