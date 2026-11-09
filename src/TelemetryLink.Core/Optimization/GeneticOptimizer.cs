using TelemetryLink.Core.Configuration;
namespace TelemetryLink.Core.Optimization;
public sealed record GeneticOptions(int PopulationSize = 12, int TournamentSize = 3,
    double MutationProbability = 0.2, double MutationScale = 0.15, int EliteCount = 1);

/// <summary>Турнір, рівномірний кросовер, обмежена мутація та елітизм без переоцінки.</summary>
public sealed class GeneticOptimizer : IParameterOptimizer
{
    private readonly GeneticOptions _options;
    public GeneticOptimizer(GeneticOptions? options = null)
    {
        _options = options ?? new();
        if (_options.PopulationSize < 1 || _options.TournamentSize < 1 || _options.EliteCount < 1 ||
            _options.EliteCount > _options.PopulationSize ||
            (_options.PopulationSize > 1 && _options.EliteCount == _options.PopulationSize) ||
            !double.IsFinite(_options.MutationProbability) || _options.MutationProbability < 0 || _options.MutationProbability > 1 ||
            !double.IsFinite(_options.MutationScale) || _options.MutationScale < 0)
            throw new ArgumentException("Некоректні параметри генетичного алгоритму.");
    }
    public string Name => "ga";
    public OptimizationResult Optimize(Func<Tuning, double> fitness, int evaluationBudget, int seed)
    {
        ArgumentNullException.ThrowIfNull(fitness);
        if (evaluationBudget < 1) throw new ArgumentOutOfRangeException(nameof(evaluationBudget));
        var random = new Random(seed); var population = new List<(Tuning Tuning, double Score)>();
        int evaluations = 0;
        (Tuning Tuning, double Score) Evaluate(Tuning tuning)
        { double score = RandomSearchOptimizer.Evaluate(fitness, tuning); evaluations++; return (tuning, score); }
        for (int i = 0; i < Math.Min(_options.PopulationSize, evaluationBudget); i++) population.Add(Evaluate(RandomSearchOptimizer.RandomTuning(random)));
        var history = new List<GenerationRecord>();
        void Record(int generation) => history.Add(new(generation, population.Max(x => x.Score), population.Average(x => x.Score)) { Evaluations = evaluations });
        Record(0); int generation = 0;
        while (evaluations < evaluationBudget)
        {
            var ranked = population.OrderByDescending(x => x.Score).ToArray();
            // Population=1 is a (1+1) strategy: retain the incumbent and evaluate one child.
            if (_options.PopulationSize == 1)
            {
                var child = Evaluate(Mutate(Crossover(ranked[0].Tuning, ranked[0].Tuning, random), random));
                if (child.Score > ranked[0].Score) population[0] = child;
            }
            else
            {
                var next = ranked.Take(Math.Min(_options.EliteCount, ranked.Length)).ToList();
                double[] scores = population.Select(x => x.Score).ToArray();
                while (next.Count < _options.PopulationSize && evaluations < evaluationBudget)
                {
                    var first = population[SelectByTournament(scores, random)].Tuning;
                    var second = population[SelectByTournament(scores, random)].Tuning;
                    next.Add(Evaluate(Mutate(Crossover(first, second, random), random)));
                }
                population = next;
            }
            Record(++generation);
        }
        var best = population.OrderByDescending(x => x.Score).First();
        return new(best.Tuning, best.Score, history.AsReadOnly(), evaluations);
    }
    public int SelectByTournament(double[] scores, Random random)
    {
        ArgumentNullException.ThrowIfNull(scores); ArgumentNullException.ThrowIfNull(random);
        if (scores.Length == 0 || scores.Any(x => !double.IsFinite(x))) throw new ArgumentException("Потрібні скінченні оцінки популяції.");
        int best = random.Next(scores.Length);
        for (int i = 1; i < _options.TournamentSize; i++) { int candidate = random.Next(scores.Length); if (scores[candidate] > scores[best]) best = candidate; }
        return best;
    }
    public static Tuning Crossover(Tuning first, Tuning second, Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        var a = first.ToGenes(); var b = second.ToGenes();
        for (int i = 0; i < a.Length; i++) if (random.Next(2) == 1) a[i] = b[i];
        return Tuning.FromGenes(a);
    }
    public Tuning Mutate(Tuning tuning, Random random)
    {
        ArgumentNullException.ThrowIfNull(random); var genes = tuning.ToGenes();
        for (int i = 0; i < genes.Length; i++)
            if (random.NextDouble() < _options.MutationProbability)
                genes[i] = Tuning.Clamp(genes[i] + (random.NextDouble() * 2 - 1) * _options.MutationScale * (Tuning.Bounds[i].High - Tuning.Bounds[i].Low), i);
        return Tuning.FromGenes(genes);
    }
}
