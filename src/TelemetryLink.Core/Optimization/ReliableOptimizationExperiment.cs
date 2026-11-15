using TelemetryLink.Core.Configuration;
namespace TelemetryLink.Core.Optimization;

public sealed record ReliabilityTrial(string Optimizer, int Evaluation, ReliabilityEvaluation Result);
public sealed record ReliableSearchResult(string Optimizer, OptimizationResult Search,
    ReliabilityEvaluation Training, ReliabilityEvaluation Validation);
public sealed record ReliableOptimizationResult(ReliabilityEvaluation BaselineTraining, ReliabilityEvaluation BaselineValidation,
    IReadOnlyList<ReliableSearchResult> Results, IReadOnlyList<ReliabilityTrial> Trials);

/// <summary>Усі переможці фіксуються за search-grid до першого виклику validation.</summary>
public static class ReliableOptimizationExperiment
{
    public static ReliableOptimizationResult Run(IReadOnlyList<IParameterOptimizer> optimizers, int budget, int seed,
        Tuning baseline, Func<Tuning, ReliabilityEvaluation> train, Func<Tuning, ReliabilityEvaluation> validate,
        Action<ReliabilityTrial>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(train); ArgumentNullException.ThrowIfNull(validate);
        if (budget < 1 || optimizers.Count == 0 || optimizers.Select(o => o.Name).Distinct().Count() != optimizers.Count)
            throw new ArgumentException("Потрібні додатний бюджет та різні оптимізатори.");
        var baselineTraining = train(baseline);
        var trials = new List<ReliabilityTrial>();
        var selected = new List<(string Name, OptimizationResult Search, ReliabilityEvaluation Training)>();
        foreach (var optimizer in optimizers)
        {
            int calls = 0;
            double Fitness(Tuning tuning)
            {
                var evaluation = train(tuning);
                var trial = new ReliabilityTrial(optimizer.Name, ++calls, evaluation);
                trials.Add(trial); progress?.Invoke(trial);
                return evaluation.Assessment.Score;
            }
            var result = optimizer.Optimize(Fitness, budget, seed);
            if (calls != budget || result.Evaluations != budget) throw new InvalidOperationException("Порушено бюджет оцінок.");
            var winner = trials.Last(t => t.Optimizer == optimizer.Name && t.Result.Tuning == result.Best
                && t.Result.Assessment.Score == result.BestFitness).Result;
            selected.Add((optimizer.Name, result, winner));
        }
        var baselineValidation = validate(baseline);
        var results = selected.Select(x => new ReliableSearchResult(x.Name, x.Search, x.Training, validate(x.Search.Best))).ToArray();
        return new(baselineTraining, baselineValidation, Array.AsReadOnly(results), trials.AsReadOnly());
    }
}
